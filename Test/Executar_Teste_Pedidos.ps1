$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5210/api'
$tag = [Guid]::NewGuid().ToString('N').Substring(0,10)

function PostJson([string]$uri, $corpo) {
    Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json; charset=utf-8' -Body ($corpo | ConvertTo-Json -Depth 8)
}

function EsperarErro([scriptblock]$bloco, [int]$statusEsperado, [string]$descricao) {
    try {
        & $bloco | Out-Null
        throw "Falha: $descricao deveria retornar HTTP $statusEsperado."
    } catch {
        $recebido = $null
        if ($_.Exception.Response) { $recebido = [int]$_.Exception.Response.StatusCode }
        if ($recebido -ne $statusEsperado) { throw }
        Write-Host "OK: $descricao -> HTTP $recebido"
    }
}

Write-Host '1. Criando vendedor, cliente e produto de teste...'
$vendedor = PostJson "$api/vendedores" @{
    nome = "Vendedor Pedidos $tag"
    email = "vendedor.$tag@exemplo.com"
    telefone = '11999999999'
}

$cnpj = '9' + (-join (1..13 | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 }))
$cliente = PostJson "$api/clientes" @{
    razaoSocial = "Padaria Pedidos $tag"
    cnpj = $cnpj
    telefone = '11999999999'
    endereco = 'Rua das Padarias, 100'
    cidade = 'Sao Paulo'
    estado = 'SP'
    ordemVisita = 1
    vendedorId = [int]$vendedor.id
}

$grupo = PostJson "$api/grupos-produto" @{
    nome = "Grupo Pedidos $tag"
    descricao = 'Teste local da aula 9'
}
$produto = PostJson "$api/produtos" @{
    nome = "Fermento Pedidos $tag"
    unidadeMedida = 'UN'
    precoVenda = 12.50
    grupoProdutoId = [int]$grupo.id
}
$lote = PostJson "$api/produtos/$($produto.id)/lotes" @{
    codigoLote = "PED-$tag-A"
    dataValidade = (Get-Date).ToUniversalTime().AddDays(60).ToString('yyyy-MM-dd')
    quantidadeDisponivel = 5
}

$estoqueAntes = Invoke-RestMethod "$api/produtos/$($produto.id)/estoque"
if ([long]$estoqueAntes.estoqueValido -ne 5) {
    throw 'Falha: o produto deveria iniciar com 5 unidades validas.'
}

Write-Host '2. Criando pedido de 8 unidades com apenas 5 em estoque...'
$solicitacao = @{
    chaveRequisicao = [Guid]::NewGuid().ToString('D')
    clienteId = [int]$cliente.id
    vendedorId = [int]$vendedor.id
    itens = @(@{ produtoId = [int]$produto.id; quantidadeSolicitada = 8 })
}
$pedido = PostJson "$api/pedidos" $solicitacao
if ([int]$pedido.status -ne 1 -or $pedido.statusNome -ne 'Pendente') {
    throw 'Falha: o pedido nao ficou Pendente.'
}
if ([int]$pedido.itens[0].quantidadeSolicitada -ne 8 -or
    [int]$pedido.itens[0].quantidadeAtendida -ne 0) {
    throw 'Falha: quantidades incorretas no pedido.'
}
if ([decimal]$pedido.valorTotalSolicitado -ne [decimal]100.0) {
    throw 'Falha: total esperado = 8 * 12,50 = 100,00.'
}
Write-Host "OK: Pedido ID=$($pedido.id), Pendente, solicitado=8, atendido=0, total=100,00"

Write-Host '3. Confirmando que o pedido nao baixou estoque...'
$estoqueDepois = Invoke-RestMethod "$api/produtos/$($produto.id)/estoque"
if ([long]$estoqueDepois.estoqueFisico -ne 5 -or [long]$estoqueDepois.estoqueValido -ne 5) {
    throw 'Falha: cadastrar pedido alterou o estoque.'
}
Write-Host 'OK: estoque continuou em 5 unidades.'

Write-Host '4. Reenviando mesma solicitacao para testar idempotencia...'
$pedidoRepetido = PostJson "$api/pedidos" $solicitacao
if ([int]$pedidoRepetido.id -ne [int]$pedido.id) {
    throw 'Falha: reenvio criou outro pedido.'
}
Write-Host "OK: mesmo ID $($pedidoRepetido.id), sem pedido duplicado."

Write-Host '5. Testando conflito de chave e itens duplicados...'
$alterado = @{
    chaveRequisicao = $solicitacao.chaveRequisicao
    clienteId = [int]$cliente.id
    vendedorId = [int]$vendedor.id
    itens = @(@{ produtoId = [int]$produto.id; quantidadeSolicitada = 7 })
}
EsperarErro { PostJson "$api/pedidos" $alterado } 409 'Reutilizacao da chave com quantidade diferente'

$duplicado = @{
    chaveRequisicao = [Guid]::NewGuid().ToString('D')
    clienteId = [int]$cliente.id
    vendedorId = [int]$vendedor.id
    itens = @(
        @{ produtoId = [int]$produto.id; quantidadeSolicitada = 1 },
        @{ produtoId = [int]$produto.id; quantidadeSolicitada = 2 }
    )
}
EsperarErro { PostJson "$api/pedidos" $duplicado } 400 'Produto repetido no mesmo pedido'

Write-Host '6. Consultando o pedido por ID e listando a carteira...'
$detalhe = Invoke-RestMethod "$api/pedidos/$($pedido.id)"
if ([int]$detalhe.id -ne [int]$pedido.id -or [int]$detalhe.itens.Count -ne 1) {
    throw 'Falha: GET por ID nao retornou o pedido correto.'
}
$lista = Invoke-RestMethod "$api/pedidos?vendedorId=$($vendedor.id)&clienteId=$($cliente.id)&status=1"
$encontrados = @($lista | Where-Object { [int]$_.id -eq [int]$pedido.id })
if ($encontrados.Count -ne 1) {
    throw 'Falha: GET da carteira nao encontrou exatamente um pedido de teste.'
}
Write-Host "OK: pedido encontrado. Cliente=$($detalhe.clienteNome), Vendedor=$($detalhe.vendedorNome)."

Write-Host "`nAULA 9.1: TESTES APROVADOS. Pedido=$($pedido.id), Produto=$($produto.id), Cliente=$($cliente.id), Vendedor=$($vendedor.id)."
Write-Host 'Sem desconto de estoque. Os registros permanecem no MySQL Docker de desenvolvimento (porta 3307).'
