$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5210/api'
$tag = [Guid]::NewGuid().ToString('N').Substring(0,10)

function PostJson([string]$url, $objeto) {
    Invoke-RestMethod -Uri $url -Method Post -ContentType 'application/json; charset=utf-8' -Body ($objeto | ConvertTo-Json -Depth 12)
}
function PutJson([string]$url, $objeto) {
    Invoke-RestMethod -Uri $url -Method Put -ContentType 'application/json; charset=utf-8' -Body ($objeto | ConvertTo-Json -Depth 12)
}
function EsperarErro([scriptblock]$acao, [int]$codigo, [string]$descricao) {
    try {
        & $acao | Out-Null
        throw "Falha: $descricao deveria resultar em HTTP $codigo."
    } catch {
        $recebido = $null
        if ($_.Exception.Response) { $recebido = [int]$_.Exception.Response.StatusCode }
        if ($recebido -ne $codigo) { throw }
        Write-Host "OK: $descricao -> HTTP $recebido"
    }
}

Write-Host '1. Criando vendedor, cliente e dois produtos de teste...'
$vendedor = PostJson "$api/vendedores" @{
    nome = "Vendedor Corte $tag"; email = "corte.$tag@exemplo.com"; telefone = '11999999999'
}
$cnpj = '9' + (-join (1..13 | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 }))
$cliente = PostJson "$api/clientes" @{
    razaoSocial = "Padaria Corte $tag"; cnpj = $cnpj; telefone = '11999999999'
    endereco = 'Rua de Teste, 100'; cidade = 'Sao Paulo'; estado = 'SP'
    ordemVisita = 1; vendedorId = [int]$vendedor.id
}
$grupo = PostJson "$api/grupos-produto" @{ nome = "Grupo Corte $tag"; descricao = 'Teste academico Aula 9.2' }
$produtoA = PostJson "$api/produtos" @{
    nome = "Fermento Corte $tag"; unidadeMedida = 'UN'; precoVenda = 12.50
    grupoProdutoId = [int]$grupo.id
}
$produtoB = PostJson "$api/produtos" @{
    nome = "Margarina Corte $tag"; unidadeMedida = 'UN'; precoVenda = 20.00
    grupoProdutoId = [int]$grupo.id
}
$hoje = (Get-Date).ToUniversalTime()
$loteA = PostJson "$api/produtos/$($produtoA.id)/lotes" @{
    codigoLote = "CORTE-$tag-A"; dataValidade = $hoje.AddDays(35).ToString('yyyy-MM-dd')
    quantidadeDisponivel = 3
}
$loteB = PostJson "$api/produtos/$($produtoA.id)/lotes" @{
    codigoLote = "CORTE-$tag-B"; dataValidade = $hoje.AddDays(70).ToString('yyyy-MM-dd')
    quantidadeDisponivel = 2
}
$loteC = PostJson "$api/produtos/$($produtoB.id)/lotes" @{
    codigoLote = "CORTE-$tag-C"; dataValidade = $hoje.AddDays(80).ToString('yyyy-MM-dd')
    quantidadeDisponivel = 1
}

Write-Host '2. Testando rollback: dois produtos, um sem estoque suficiente...'
$pedidoRollback = PostJson "$api/pedidos" @{
    chaveRequisicao = [Guid]::NewGuid().ToString('D')
    clienteId = [int]$cliente.id; vendedorId = [int]$vendedor.id
    itens = @(
        @{ produtoId = [int]$produtoA.id; quantidadeSolicitada = 3 },
        @{ produtoId = [int]$produtoB.id; quantidadeSolicitada = 2 }
    )
}
$corteInsuficiente = @{
    itens = @(
        @{ produtoId = [int]$produtoA.id; quantidadeAtendida = 2 },
        @{ produtoId = [int]$produtoB.id; quantidadeAtendida = 2 }
    )
}
EsperarErro { PutJson "$api/pedidos/$($pedidoRollback.id)/cortar" $corteInsuficiente } 409 'Corte com estoque insuficiente deve reverter'
$estoqueA = Invoke-RestMethod "$api/produtos/$($produtoA.id)/estoque"
$estoqueB = Invoke-RestMethod "$api/produtos/$($produtoB.id)/estoque"
if ([int]$estoqueA.estoqueFisico -ne 5 -or [int]$estoqueB.estoqueFisico -ne 1) {
    throw 'Falha: rollback nao restaurou os saldos originais.'
}
$pedidoRollbackDepois = Invoke-RestMethod "$api/pedidos/$($pedidoRollback.id)"
if ([int]$pedidoRollbackDepois.status -ne 1 -or
    @($pedidoRollbackDepois.itens | Where-Object { $_.quantidadeAtendida -ne 0 }).Count -ne 0) {
    throw 'Falha: rollback modificou o pedido pendente.'
}
Write-Host 'OK: rollback preservou 5 + 1 unidades e pedido pendente.'

Write-Host '3. Registrando pedido de 8 fermentos com apenas 5 disponiveis...'
$pedido = PostJson "$api/pedidos" @{
    chaveRequisicao = [Guid]::NewGuid().ToString('D')
    clienteId = [int]$cliente.id; vendedorId = [int]$vendedor.id
    itens = @( @{ produtoId = [int]$produtoA.id; quantidadeSolicitada = 8 } )
}
if ([int]$pedido.status -ne 1) { throw 'Falha: pedido nao iniciou Pendente.' }
$estoqueAntes = Invoke-RestMethod "$api/produtos/$($produtoA.id)/estoque"
if ([int]$estoqueAntes.estoqueFisico -ne 5) { throw 'Falha: estoque mudou ao criar pedido.' }

Write-Host '4. Fazendo corte manual de 5 unidades (FEFO, 3 do lote A e 2 do B)...'
$corte = @{ itens = @( @{ produtoId = [int]$produtoA.id; quantidadeAtendida = 5 } ) }
$resultado = PutJson "$api/pedidos/$($pedido.id)/cortar" $corte
if ([int]$resultado.status -ne 4 -or [int]$resultado.totalAtendido -ne 5 -or
    [decimal]$resultado.valorTotalAtendido -ne [decimal]62.50) {
    throw 'Falha: corte deveria ficar AtendidoParcialmente, 5 unidades, valor 62,50.'
}
$alocacoes = @(Invoke-RestMethod "$api/pedidos/$($pedido.id)/alocacoes" | ForEach-Object { $_ })
if ($alocacoes.Count -ne 2 -or [int]$alocacoes[0].loteEstoqueId -ne [int]$loteA.id -or
    [int]$alocacoes[0].quantidade -ne 3 -or
    [int]$alocacoes[1].loteEstoqueId -ne [int]$loteB.id -or
    [int]$alocacoes[1].quantidade -ne 2) {
    $alocacoes | Format-Table | Out-Host
    throw 'Falha: alocacoes nao seguiram FEFO 3 + 2.'
}
$pedidoDepois = Invoke-RestMethod "$api/pedidos/$($pedido.id)"
$estoqueDepois = Invoke-RestMethod "$api/produtos/$($produtoA.id)/estoque"
if ([int]$pedidoDepois.itens[0].quantidadeAtendida -ne 5 -or [int]$estoqueDepois.estoqueFisico -ne 0) {
    throw 'Falha: corte nao foi persistido corretamente.'
}
Write-Host 'OK: parcial=5/8, valor=62,50, saldo=0, alocacoes FEFO 3+2.'

Write-Host '5. Confirmando que a repeticao do corte nao desconta novamente...'
EsperarErro { PutJson "$api/pedidos/$($pedido.id)/cortar" $corte } 409 'Pedido ja processado'
$estoqueRepetido = Invoke-RestMethod "$api/produtos/$($produtoA.id)/estoque"
if ([int]$estoqueRepetido.estoqueFisico -ne 0) { throw 'Falha: pedido duplicado alterou o estoque.' }

Write-Host '6. Validando corte integral em outro produto...'
$pedidoIntegral = PostJson "$api/pedidos" @{
    chaveRequisicao = [Guid]::NewGuid().ToString('D')
    clienteId = [int]$cliente.id; vendedorId = [int]$vendedor.id
    itens = @( @{ produtoId = [int]$produtoB.id; quantidadeSolicitada = 1 } )
}
$integral = PutJson "$api/pedidos/$($pedidoIntegral.id)/cortar" @{
    itens = @( @{ produtoId = [int]$produtoB.id; quantidadeAtendida = 1 } )
}
if ([int]$integral.status -ne 3 -or [int]$integral.totalAtendido -ne 1) {
    throw 'Falha: pedido deveria estar Atendido integralmente.'
}
$estoqueBFinal = Invoke-RestMethod "$api/produtos/$($produtoB.id)/estoque"
if ([int]$estoqueBFinal.estoqueFisico -ne 0) { throw 'Falha: pedido integral nao baixou estoque.' }
Write-Host 'OK: integral, quantidade=1 e estoque B=0.'

Write-Host '7. Conferindo validacao de quantidade maior que a solicitada...'
$excedente = PostJson "$api/pedidos" @{
    chaveRequisicao = [Guid]::NewGuid().ToString('D')
    clienteId = [int]$cliente.id; vendedorId = [int]$vendedor.id
    itens = @( @{ produtoId = [int]$produtoB.id; quantidadeSolicitada = 1 } )
}
EsperarErro { PutJson "$api/pedidos/$($excedente.id)/cortar" @{
    itens = @( @{ produtoId = [int]$produtoB.id; quantidadeAtendida = 2 } )
} } 400 'Corte acima do solicitado'
$excedenteDepois = Invoke-RestMethod "$api/pedidos/$($excedente.id)"
if ([int]$excedenteDepois.status -ne 1) { throw 'Falha: validacao modificou o status.' }

Write-Host "`nAULA 9.2: TESTES APROVADOS. Parcial=$($pedido.id), Integral=$($pedidoIntegral.id), Rollback=$($pedidoRollback.id)."
Write-Host 'Banco local do Docker, porta 3307. Registros de teste permanecem para consulta no Workbench.'
