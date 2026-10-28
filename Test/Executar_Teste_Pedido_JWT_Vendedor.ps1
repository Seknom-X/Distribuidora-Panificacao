param(
    [string]$Api = 'http://localhost:5210',
    [int]$ClienteOutraCarteiraId = 0
)

$ErrorActionPreference = 'Stop'

function Confirmar([bool]$Condicao, [string]$Mensagem) {
    if (-not $Condicao) { throw "FALHA: $Mensagem" }
}

function ObterStatusDeErro([scriptblock]$Acao) {
    try {
        & $Acao | Out-Null
        return 0
    }
    catch {
        if ($null -ne $_.Exception.Response) {
            return [int]$_.Exception.Response.StatusCode
        }
        throw
    }
}

Write-Host 'Aula 10.2 - Cadastro de pedidos autenticado pelo vendedor' -ForegroundColor Cyan
$email = Read-Host 'E-mail do Vendedor'
$senhaSegura = Read-Host 'Senha do Vendedor (oculta)' -AsSecureString
$credencial = [PSCredential]::new('temporario', $senhaSegura)
$loginCorpo = @{
    email = $email
    senha = $credencial.GetNetworkCredential().Password
} | ConvertTo-Json

try {
    $login = Invoke-RestMethod -Uri "$Api/api/auth/login" -Method Post `
        -ContentType 'application/json' -Body $loginCorpo
}
finally {
    Remove-Variable loginCorpo,credencial,senhaSegura -ErrorAction SilentlyContinue
}

Confirmar ($login.usuario.perfil -eq 'Vendedor') 'O login precisa ser de Vendedor.'
$vendedorId = [int]$login.usuario.vendedorId
Confirmar ($vendedorId -gt 0) 'O token nao identifica um vendedor.'
$headers = @{ Authorization = "Bearer $($login.token)" }
$login = $null
Write-Host "OK: token vinculado ao vendedor ID=$vendedorId."

$clientesRecebidos = Invoke-RestMethod -Uri "$Api/api/me/clientes" -Headers $headers
[object[]]$clientes = @($clientesRecebidos | Where-Object { $null -ne $_ })
Confirmar ($clientes.Length -gt 0) 'A carteira nao tem clientes ativos. Cadastre um cliente para esse vendedor primeiro.'
$cliente = $clientes[0]
Write-Host "OK: cliente da propria carteira: ID=$($cliente.id)."

$produtosRecebidos = Invoke-RestMethod -Uri "$Api/api/me/produtos" -Headers $headers
[object[]]$produtos = @($produtosRecebidos | Where-Object { $null -ne $_ -and [decimal]$_.precoVenda -gt 0 })
Confirmar ($produtos.Length -gt 0) 'Nao foi encontrado produto ativo com preco positivo.'
$produto = $produtos[0]
Write-Host "OK: produto selecionado: ID=$($produto.id)."

$chave = [guid]::NewGuid().ToString('D')
$pedidoDados = @{
    chaveRequisicao = $chave
    clienteId = [int]$cliente.id
    vendedorId = 99999  # Campo indevido: o servidor DEVE ignora-lo, usando o JWT.
    itens = @(@{ produtoId = [int]$produto.id; quantidadeSolicitada = 2 })
}
$corpo = $pedidoDados | ConvertTo-Json -Depth 8

Write-Host '1. Criando pedido Pendente com o JWT do Vendedor...'
$pedido = Invoke-RestMethod -Uri "$Api/api/me/pedidos" -Headers $headers `
    -Method Post -ContentType 'application/json' -Body $corpo
Confirmar ([int]$pedido.id -gt 0) 'O pedido nao retornou ID.'
Confirmar ([int]$pedido.vendedorId -eq $vendedorId) 'A API aceitou um vendedorId arbitrario!'
Confirmar ([int]$pedido.status -eq 1) 'O pedido deveria ser Pendente (1).'
Confirmar ([int]$pedido.itens[0].quantidadeAtendida -eq 0) 'O pedido nao deve atender unidades no cadastro.'
Write-Host "OK: pedido ID=$($pedido.id), vendedorId=$($pedido.vendedorId), Pendente, atendido=0."

Write-Host '2. Reenviando o mesmo UUID e corpo...'
$repetido = Invoke-RestMethod -Uri "$Api/api/me/pedidos" -Headers $headers `
    -Method Post -ContentType 'application/json' -Body $corpo
Confirmar ([int]$repetido.id -eq [int]$pedido.id) 'Idempotencia falhou: ID diferente.'
Write-Host 'OK: reenvio nao criou outro pedido.'

Write-Host '3. Reutilizando a chave com quantidade diferente...'
$pedidoDados.itens = @(@{ produtoId = [int]$produto.id; quantidadeSolicitada = 3 })
$corpoConflito = $pedidoDados | ConvertTo-Json -Depth 8
$httpConflito = ObterStatusDeErro { Invoke-RestMethod -Uri "$Api/api/me/pedidos" -Headers $headers `
    -Method Post -ContentType 'application/json' -Body $corpoConflito }
Confirmar ($httpConflito -eq 409) "Esperavamos HTTP 409, recebido $httpConflito."
Write-Host 'OK: chave com dados diferentes => HTTP 409.'

Write-Host '4. Consultando detalhe do pedido da propria carteira...'
$detalhe = Invoke-RestMethod -Uri "$Api/api/me/pedidos/$($pedido.id)" -Headers $headers
Confirmar ([int]$detalhe.vendedorId -eq $vendedorId) 'O detalhe retornou pedido de outro vendedor.'
Write-Host 'OK: GET de pedido proprio => HTTP 200.'

Write-Host '5. Conferindo isolamento de rotas...'
$semToken = ObterStatusDeErro { Invoke-RestMethod -Uri "$Api/api/me/pedidos" -Method Post `
    -ContentType 'application/json' -Body $corpo }
Confirmar ($semToken -eq 401) "Esperavamos HTTP 401 sem token, recebido $semToken."
Write-Host 'OK: POST sem JWT => HTTP 401.'

$administrativa = ObterStatusDeErro { Invoke-RestMethod -Uri "$Api/api/pedidos" -Headers $headers }
Confirmar ($administrativa -eq 403) "Esperavamos HTTP 403 para GET administrativo, recebido $administrativa."
Write-Host 'OK: rota administrativa => HTTP 403.'

$inexistente = ObterStatusDeErro { Invoke-RestMethod -Uri "$Api/api/me/pedidos/2147483647" -Headers $headers }
Confirmar ($inexistente -eq 404) "Esperavamos HTTP 404 para pedido nao encontrado, recebido $inexistente."
Write-Host 'OK: pedido nao encontrado => HTTP 404.'

if ($ClienteOutraCarteiraId -gt 0) {
    Write-Host '6. Verificando cliente pertencente a outra carteira...'
    $pedidoDados.clienteId = $ClienteOutraCarteiraId
    $pedidoDados.chaveRequisicao = [guid]::NewGuid().ToString('D')
    $pedidoDados.itens = @(@{produtoId=[int]$produto.id; quantidadeSolicitada=2})
    $corpoOutro = $pedidoDados | ConvertTo-Json -Depth 8
    $httpOutro = ObterStatusDeErro { Invoke-RestMethod -Uri "$Api/api/me/pedidos" -Headers $headers `
        -Method Post -ContentType 'application/json' -Body $corpoOutro }
    Confirmar ($httpOutro -eq 404) "Esperavamos HTTP 404 para cliente de outra carteira, recebido $httpOutro."
    Write-Host 'OK: vendedor nao cadastra para cliente de outra carteira (404).'
}

Write-Host "AULA 10.2: TESTES PRINCIPAIS APROVADOS. Pedido ID=$($pedido.id)." -ForegroundColor Green
Write-Host 'O pedido ficticio PERMANECE Pendente no MySQL local. Nenhum estoque foi baixado.'
Write-Host 'Para testar outra carteira, informe -ClienteOutraCarteiraId NUMERO_REAL de outro vendedor.'
