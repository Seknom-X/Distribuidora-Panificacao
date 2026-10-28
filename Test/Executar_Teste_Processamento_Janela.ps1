param(
    [int]$LoteId = 0,
    [string]$ArquivoPlano = '',
    [switch]$ConfirmarProcessamento
)

$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5210'

function Exigir($condicao, $mensagem) {
    if (-not $condicao) { throw "FALHA: $mensagem" }
}

if ($LoteId -le 0) {
    Write-Host 'Informe o ID do lote da Aula 9.4: .\Executar_Teste_Processamento_Janela.ps1 -LoteId 1'
    exit 1
}

Write-Host "1. Consultando lote fechado $LoteId..."
$lote = Invoke-RestMethod -Uri "$api/api/lotes-processamento/$LoteId"
[object[]]$pedidos = @($lote.pedidos | Where-Object { $null -ne $_ })
Exigir ($pedidos.Length -eq [int]$lote.pedidosIncluidos) 'A lista nao corresponde a contagem do lote.'
Write-Host "OK: lote $LoteId, pedidos incluidos = $($pedidos.Length)."

Write-Host '2. Consultando situacao atual do processamento (sem alterar o banco)...'
$resumo = Invoke-RestMethod -Uri "$api/api/lotes-processamento/$LoteId/processamento"
Exigir ([int]$resumo.loteId -eq $LoteId) 'Resumo do lote diferente.'
Write-Host "OK: processado = $($resumo.processado); pedidos = $($resumo.pedidosIncluidos); unidades atendidas = $($resumo.totalUnidadesAtendidas)."

Write-Host '3. Conferindo validacao de requisicao vazia...'
try {
    Invoke-WebRequest -Uri "$api/api/lotes-processamento/$LoteId/processar" -Method Post `
        -ContentType 'application/json' -Body '{"pedidos":[]}' -UseBasicParsing | Out-Null
    throw 'FALHA: corpo vazio deveria retornar HTTP 400.'
}
catch {
    if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 400) { throw }
}
Write-Host 'OK: corpo vazio => HTTP 400, sem nenhuma baixa.'

if (-not $ConfirmarProcessamento) {
    Write-Host 'TESTES DE LEITURA APROVADOS: nenhum pedido ou estoque foi alterado.'
    if ($pedidos.Length -eq 0) { Write-Host 'AVISO: lote vazio. Para testar baixas reais sera necessario fechar uma janela contendo pedidos.' }
    else { Write-Host 'Para processar, crie um JSON com TODOS os pedidos e respectivos itens; reveja as quantidades antes de enviar.' }
    Write-Host 'O processamento real exige -ConfirmarProcessamento -ArquivoPlano .\plano.json e grava baixas no MySQL.'
    exit 0
}

Exigir (-not [string]::IsNullOrWhiteSpace($ArquivoPlano)) 'Informe -ArquivoPlano com o JSON dos cortes aprovados.'
Exigir (Test-Path -LiteralPath $ArquivoPlano) 'ArquivoPlano nao encontrado.'
Exigir ($pedidos.Length -gt 0) 'Lote fechado esta vazio; nao pode processar.'
Exigir (-not [bool]$resumo.processado) 'Lote ja processado. Nao enviar novamente.'

$plano = Get-Content -LiteralPath $ArquivoPlano -Raw -Encoding UTF8 | ConvertFrom-Json
[object[]]$envio = @($plano.pedidos | Where-Object { $null -ne $_ })
Exigir ($envio.Length -eq $pedidos.Length) 'Plano deve conter TODOS os pedidos deste lote.'
$idsLote = @($pedidos | ForEach-Object { [int]$_.pedidoId } | Sort-Object)
$idsPlano = @($envio | ForEach-Object { [int]$_.pedidoId } | Sort-Object)
Exigir ((@($idsLote) -join ',') -eq (@($idsPlano) -join ',')) 'IDs dos pedidos no arquivo nao coincidem com os IDs do lote.'

Write-Host "4. ENVIANDO PROCESSAMENTO REAL DO LOTE $LoteId (altera estoque e pedidos)..."
$json = Get-Content -LiteralPath $ArquivoPlano -Raw -Encoding UTF8
$resultado = Invoke-RestMethod -Uri "$api/api/lotes-processamento/$LoteId/processar" `
    -Method Post -Body $json -ContentType 'application/json'
Exigir ([bool]$resultado.processado) 'Processamento nao foi marcado como concluido.'
Write-Host "OK: Processamento concluido; unidades = $($resultado.totalUnidadesAtendidas); valor = $($resultado.valorAtendido)."
Write-Host 'ATENCAO: as saidas de estoque foram gravadas. Consulte o Workbench.'
