param(
    [ValidateRange(1, 2147483647)][int]$PedidoId = 1,
    [string]$Api = 'http://localhost:5210'
)
$ErrorActionPreference = 'Stop'

function Exigir([bool]$Condicao, [string]$Mensagem) {
    if (-not $Condicao) { throw "FALHA: $Mensagem" }
}
function Chamar([string]$Uri, [string]$Metodo, $Cabecalhos, $Corpo = $null) {
    $param = @{ Uri=$Uri; Method=$Metodo; Headers=$Cabecalhos; UseBasicParsing=$true }
    if ($null -ne $Corpo) {
        $param['ContentType'] = 'application/json'
        $param['Body'] = $Corpo
    }
    $res = Invoke-WebRequest @param
    $dados = if ([string]::IsNullOrWhiteSpace($res.Content)) { $null } else { $res.Content | ConvertFrom-Json }
    return [pscustomobject]@{ Status=[int]$res.StatusCode; Dados=$dados }
}
function CodigoErro([string]$Uri, [string]$Metodo, $Cabecalhos) {
    try {
        $null = Invoke-WebRequest -Uri $Uri -Method $Metodo -Headers $Cabecalhos -UseBasicParsing
        return 200
    }
    catch [System.Net.WebException] {
        if ($null -ne $_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}

$Api = $Api.TrimEnd('/')
if ($Api -ne 'http://localhost:5210' -and $Api -ne 'http://127.0.0.1:5210') {
    throw 'Este teste de desenvolvimento aceita somente http://localhost:5210.'
}
Write-Host 'Aula 11 - Venda a partir do pedido ja atendido' -ForegroundColor Cyan
Write-Host 'O teste CRIARA uma venda se ainda nao existir; NAO baixa estoque.' -ForegroundColor Yellow
$usuario = (Read-Host 'Email do Admin').Trim()
$senhaSegura = Read-Host 'Senha do Admin (oculta)' -AsSecureString
$cred = New-Object System.Management.Automation.PSCredential('login', $senhaSegura)
$corpoLogin = @{ email=$usuario; senha=$cred.GetNetworkCredential().Password } | ConvertTo-Json -Compress
$cred = $null
$senhaSegura = $null
$bytesLogin = [System.Text.Encoding]::UTF8.GetBytes($corpoLogin)
$corpoLogin = $null
try {
    $login = Invoke-RestMethod -Uri "$Api/api/auth/login" -Method Post -Body $bytesLogin -ContentType 'application/json; charset=utf-8' -ErrorAction Stop
} catch [System.Net.WebException] {
    $codigo = if ($null -ne $_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    if ($codigo -eq 401) {
        throw 'HTTP 401 no login do Admin: confira email, senha e se a conta Admin esta ativa. Execute Executar_Teste_JWT_Admin.ps1 com as mesmas credenciais. Nenhuma venda foi emitida.'
    }
    if ($codigo -gt 0) {
        throw "Login falhou com HTTP $codigo. Nenhuma venda foi emitida."
    }
    throw 'Nao foi possivel conectar ao endpoint de login. Confira se a API esta ligada na porta 5210.'
} finally {
    $bytesLogin = $null
}
Exigir ($login.usuario.perfil -eq 'Admin') 'A conta precisa ter perfil Admin.'
$headers = @{ Authorization="Bearer $($login.token)" }
$pedidoUrl = "$Api/api/pedidos/$PedidoId"
$pedido = (Chamar $pedidoUrl 'GET' $headers).Dados
Exigir (($pedido.status -eq 3) -or ($pedido.status -eq 4)) 'Pedido ainda nao foi atendido; nao gere venda.'
$items = @($pedido.itens | Where-Object { $null -ne $_ })
$quantidade = [int](($items | Measure-Object -Property quantidadeAtendida -Sum).Sum)
Exigir ($quantidade -gt 0) 'Pedido nao possui quantidades atendidas.'
$produtoId = [int]$items[0].produtoId
$estoqueUrl = "$Api/api/produtos/$produtoId/estoque"
$antes = (Chamar $estoqueUrl 'GET' $headers).Dados
Write-Host "Pedido ${PedidoId}: unidades atendidas = $quantidade; estoque consultado." -ForegroundColor Cyan

$uriVenda = "$Api/api/vendas/do-pedido/$PedidoId"
$primeira = Chamar $uriVenda 'POST' $headers
Exigir (($primeira.Status -eq 201) -or ($primeira.Status -eq 200)) 'Primeira chamada deveria retornar 201 ou 200.'
$idVenda = [int]$primeira.Dados.id
Exigir ($idVenda -gt 0) 'Resposta de venda sem ID.'
Write-Host "OK: primeira chamada HTTP $($primeira.Status), venda ID=$idVenda, valor=$($primeira.Dados.valorTotal)" -ForegroundColor Green

$segunda = Chamar $uriVenda 'POST' $headers
Exigir ($segunda.Status -eq 200) 'Reenvio deveria retornar HTTP 200.'
Exigir ([int]$segunda.Dados.id -eq $idVenda) 'Reenvio gerou outra venda!'
Write-Host 'OK: reenvio idempotente HTTP 200, mesmo ID.' -ForegroundColor Green

$porPedido = Chamar "$Api/api/vendas/por-pedido/$PedidoId" 'GET' $headers
Exigir ([int]$porPedido.Dados.id -eq $idVenda) 'Consulta por pedido retornou ID diferente.'
$itensVenda = @($porPedido.Dados.itens | Where-Object { $null -ne $_ })
$quantidadeVendida = [int](($itensVenda | Measure-Object -Property quantidade -Sum).Sum)
Exigir ($quantidadeVendida -eq $quantidade) 'Quantidade vendida difere da atendida.'
Write-Host "OK: venda contem $quantidadeVendida unidades, originadas do pedido." -ForegroundColor Green

$depois = (Chamar $estoqueUrl 'GET' $headers).Dados
Exigir (($antes | ConvertTo-Json -Depth 10 -Compress) -eq ($depois | ConvertTo-Json -Depth 10 -Compress)) 'Estoque mudou ao emitir venda!'
Write-Host 'OK: estoque inalterado antes/depois da emissao da venda.' -ForegroundColor Green

Exigir ((CodigoErro "$Api/api/vendas" 'GET' @{}) -eq 401) 'Sem JWT deveria ser HTTP 401.'
Write-Host 'OK: sem JWT => 401' -ForegroundColor Green
Write-Host "Aula 11: TESTES PRINCIPAIS APROVADOS. Venda ID=$idVenda; Pedido ID=$PedidoId" -ForegroundColor Green
Write-Host 'Obs.: teste 403 com Vendedor, corrida e rollback ainda requerem verificacoes adicionais.' -ForegroundColor Yellow
