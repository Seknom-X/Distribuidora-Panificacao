param(
    [ValidateRange(1, 2147483647)]
    [int]$LoteId = 1,
    [string]$ArquivoSaida = ''
)

$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5210'

if ([string]::IsNullOrWhiteSpace($ArquivoSaida)) {
    $ArquivoSaida = ".\plano-lote-$LoteId.json"
}

if (Test-Path -LiteralPath $ArquivoSaida) {
    throw "Ja existe um arquivo em '$ArquivoSaida'. Escolha outro nome com -ArquivoSaida para nao sobrescrever um plano existente."
}

Write-Host "1. Buscando os pedidos do lote fechado $LoteId..." -ForegroundColor Cyan
$lote = Invoke-RestMethod -Uri "$api/api/lotes-processamento/$LoteId" -Method Get
$resumo = Invoke-RestMethod -Uri "$api/api/lotes-processamento/$LoteId/processamento" -Method Get

if ([bool]$resumo.processado) {
    throw 'Este lote ja foi processado. Nao deve ser enviado novamente.'
}

[object[]]$pedidosLote = @($lote.pedidos | Where-Object { $null -ne $_ })
if ($pedidosLote.Length -ne [int]$lote.pedidosIncluidos) {
    throw 'A contagem de pedidos nao coincide com a lista retornada pela API.'
}
if ($pedidosLote.Length -eq 0) {
    throw 'O lote esta vazio: nenhum pedido pode ser processado.'
}
if ($pedidosLote.Length -gt 100) {
    throw 'Esta aula suporta ate 100 pedidos por processamento.'
}

$planejados = New-Object 'System.Collections.Generic.List[object]'
$jaSelecionadoPorProduto = @{}

foreach ($referencia in $pedidosLote) {
    $pedidoId = [int]$referencia.pedidoId
    $pedido = Invoke-RestMethod -Uri "$api/api/pedidos/$pedidoId" -Method Get
    if ([int]$pedido.status -ne 1) {
        throw "O pedido ${pedidoId} nao esta Pendente. Status atual: $($pedido.statusNome)."
    }

    [object[]]$itensPedido = @($pedido.itens | Where-Object { $null -ne $_ })
    if ($itensPedido.Length -eq 0) {
        throw "O pedido ${pedidoId} nao possui itens."
    }

    Write-Host "`nPedido $pedidoId | Cliente: $($pedido.clienteNome) | Status: $($pedido.statusNome)" -ForegroundColor Yellow
    $itensPlanejados = New-Object 'System.Collections.Generic.List[object]'
    $totalPedido = 0

    foreach ($item in $itensPedido) {
        $produtoId = [int]$item.produtoId
        $solicitado = [int]$item.quantidadeSolicitada
        if ($solicitado -le 0) {
            throw "Pedido ${pedidoId}: quantidade solicitada invalida para produto $produtoId."
        }

        $estoque = Invoke-RestMethod -Uri "$api/api/produtos/$produtoId/estoque" -Method Get
        $disponivel = [long]$estoque.estoqueValido
        $reservadoNestePlano = 0L
        if ($jaSelecionadoPorProduto.ContainsKey("$produtoId")) {
            $reservadoNestePlano = [long]$jaSelecionadoPorProduto["$produtoId"]
        }
        $maximo = [long][Math]::Min([long]$solicitado, [Math]::Max(0L, $disponivel - $reservadoNestePlano))
        $preco = $item.precoUnitario

        Write-Host ("  Produto {0}: {1}" -f $produtoId, $item.produtoNome)
        Write-Host ("  Solicitado: {0} | Preco unitario: R$ {1} | Estoque valido atual: {2} | Limite para este item: {3}" -f $solicitado, $preco, $disponivel, $maximo)

        while ($true) {
            $texto = Read-Host "  Quanto deseja atender? Digite um inteiro entre 0 e $maximo"
            $quantidade = 0
            if ([int]::TryParse($texto, [ref]$quantidade) -and $quantidade -ge 0 -and [long]$quantidade -le $maximo) {
                break
            }
            Write-Host '  Valor invalido. Informe um numero inteiro dentro do limite.' -ForegroundColor Red
        }

        $jaSelecionadoPorProduto["$produtoId"] = $reservadoNestePlano + $quantidade
        $totalPedido += $quantidade
        $itensPlanejados.Add([pscustomobject]@{
            produtoId = $produtoId
            quantidadeAtendida = $quantidade
        })
    }

    if ($totalPedido -le 0) {
        throw "O pedido $pedidoId ficou com atendimento zero. Nesta etapa a API exige ao menos uma unidade atendida por pedido. Nenhum plano foi salvo."
    }

    $planejados.Add([pscustomobject]@{
        pedidoId = $pedidoId
        itens = @($itensPlanejados.ToArray())
    })
}

$plano = [pscustomobject]@{ pedidos = @($planejados.ToArray()) }
$json = $plano | ConvertTo-Json -Depth 12
$caminho = [System.IO.Path]::GetFullPath($ArquivoSaida)
[System.IO.File]::WriteAllText($caminho, $json, ([System.Text.UTF8Encoding]::new($false)))

Write-Host "`n2. Plano salvo em: $caminho" -ForegroundColor Green
Write-Host '3. Revise as quantidades e os IDs. NENHUM processamento foi enviado.' -ForegroundColor Yellow
Write-Host '   Veja o arquivo com:'
Write-Host "   Get-Content -LiteralPath '$caminho' -Raw"
Write-Host "`nSOMENTE depois de conferir, para processar NO BANCO LOCAL:" -ForegroundColor Yellow
Write-Host "   .\Executar_Teste_Processamento_Janela.ps1 -LoteId $LoteId -ArquivoPlano '$caminho' -ConfirmarProcessamento"
Write-Host 'ATENCAO: este ultimo comando faz baixas reais e altera os pedidos permanentemente no banco de desenvolvimento.' -ForegroundColor Red
