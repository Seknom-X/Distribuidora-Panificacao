param(
    [ValidateRange(1,2147483647)][int]$VendaId = 1,
    [ValidateRange(1,100)][int]$Limite = 20,
    [string]$Api = 'http://localhost:5210'
)
$ErrorActionPreference = 'Stop'
function Exigir([bool]$Condicao, [string]$Mensagem) {
    if (-not $Condicao) { throw "FALHA: $Mensagem" }
}
function Chamar([string]$Uri,[string]$Metodo,$Headers,$Corpo=$null) {
    $p = @{ Uri=$Uri; Method=$Metodo; Headers=$Headers; UseBasicParsing=$true; ErrorAction='Stop' }
    if ($null -ne $Corpo) {
        $p['ContentType']='application/json; charset=utf-8'
        $p['Body']=[Text.Encoding]::UTF8.GetBytes(($Corpo | ConvertTo-Json -Depth 10 -Compress))
    }
    $resp = Invoke-WebRequest @p
    $obj = if ([string]::IsNullOrWhiteSpace($resp.Content)) { $null } else { $resp.Content | ConvertFrom-Json }
    return [pscustomobject]@{ Status=[int]$resp.StatusCode; Dados=$obj }
}
function CodigoErro([string]$Uri,[string]$Metodo,$Headers) {
    try { $null=Chamar $Uri $Metodo $Headers; return 200 }
    catch [System.Net.WebException] {
        if ($null -ne $_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}
function Login([string]$Perfil) {
    $email=(Read-Host "Email do $Perfil").Trim()
    $senha=Read-Host "Senha do $Perfil (oculta)" -AsSecureString
    $cred=[PSCredential]::new('login',$senha)
    try {
        $res=Chamar "$Api/api/auth/login" 'POST' @{} @{
            email=$email; senha=$cred.GetNetworkCredential().Password
        }
        Exigir ($res.Status -eq 200 -and $res.Dados.usuario.perfil -eq $Perfil) "Login precisa ser $Perfil."
        return @{Authorization="Bearer $($res.Dados.token)"}
    } finally { $cred=$null; $senha=$null }
}
$Api=$Api.TrimEnd('/')
if ($Api -ne 'http://localhost:5210' -and $Api -ne 'http://127.0.0.1:5210') {
    throw 'Testes somente em localhost na porta 5210.'
}
Write-Host 'Aula 14 - Historico do vendedor e auditoria financeira (SOMENTE LEITURA).' -ForegroundColor Cyan
$admin=Login 'Admin'
Write-Host '1. Verificando conta a receber e reconciliacao financeira...' -ForegroundColor Cyan
$conta=(Chamar "$Api/api/contas-receber/por-venda/$VendaId" 'GET' $admin).Dados
$idConta=[int]$conta.id
Exigir ($idConta -gt 0) 'A venda nao possui conta a receber. Conclua a Aula 13.'
$auditoria=(Chamar "$Api/api/admin/auditoria/contas/$idConta" 'GET' $admin).Dados
Exigir ([int]$auditoria.contaId -eq $idConta) 'Auditoria de conta retornou ID errado.'
Exigir ([decimal]$auditoria.valorConta -eq [decimal]$conta.valorOriginal) 'Valor da conta difere do cadastro.'
Exigir ([decimal]$auditoria.somaParcelas -eq [decimal]$conta.valorOriginal) 'Soma das parcelas deveria fechar valor original.'
Exigir ([decimal]$auditoria.saldoDevedor -eq [decimal]$conta.saldoDevedor) 'Saldo diverge do resumo da conta.'
Exigir ([bool]$auditoria.consistente) 'Auditoria encontrou divergencias; investigue antes de continuar.'
Write-Host "OK: conta $idConta consistente; saldo $($auditoria.saldoDevedor)." -ForegroundColor Green

Write-Host '2. Conferindo listagem limitada de auditoria...' -ForegroundColor Cyan
$resumo=(Chamar "$Api/api/admin/auditoria/financeiro?limite=$Limite" 'GET' $admin).Dados
Exigir ([int]$resumo.contasConferidas -ge 1) 'Esperavamos alguma conta a receber.'
$ids=@($resumo.contas | Where-Object { $null -ne $_ } | ForEach-Object { [int]$_.contaId })
Exigir ($ids -contains $idConta) 'Conta de teste nao esta entre as ultimas N contas; aumente -Limite.'
Write-Host "OK: $($resumo.contasConferidas) conta(s), alertas em $($resumo.contasComAlertas) conta(s)." -ForegroundColor Green

Write-Host '3. Validando acesso sem token...' -ForegroundColor Cyan
$semToken=CodigoErro "$Api/api/admin/auditoria/contas/$idConta" 'GET' @{}
Exigir ($semToken -eq 401) 'Auditoria sem JWT deve responder HTTP 401.'
Write-Host 'OK: Admin sem JWT => HTTP 401.' -ForegroundColor Green

Write-Host '4. Logando como Vendedor para conferir sua carteira...' -ForegroundColor Cyan
$vendedor=Login 'Vendedor'
$clientes=@((Chamar "$Api/api/me/clientes" 'GET' $vendedor).Dados | Where-Object { $null -ne $_ })
Exigir ($clientes.Count -gt 0) 'Vendedor sem cliente ativo; finalize a Aula 10.2.'
$idCliente=[int]$clientes[0].id
$historico=(Chamar "$Api/api/me/clientes/$idCliente/historico" 'GET' $vendedor).Dados
Exigir ([int]$historico.clienteId -eq $idCliente) 'Historico retornou outro cliente.'
Exigir ([decimal]$historico.saldoDevedor -ge 0) 'Saldo do cliente negativo.'
Write-Host "OK: cliente $idCliente, pedidos $($historico.quantidadeTotalPedidos), vendas $($historico.quantidadeTotalVendas), saldo $($historico.saldoDevedor)." -ForegroundColor Green

Write-Host '5. Validando a separacao de perfis...' -ForegroundColor Cyan
$negado=CodigoErro "$Api/api/admin/auditoria/contas/$idConta" 'GET' $vendedor
Exigir ($negado -eq 403) 'Vendedor nao deve acessar auditoria Admin.'
$negadoAdmin=CodigoErro "$Api/api/me/clientes/$idCliente/historico" 'GET' $admin
Exigir ($negadoAdmin -eq 403) 'Admin nao deve acessar rota exclusiva de Vendedor.'
$semAuth=CodigoErro "$Api/api/me/clientes/$idCliente/historico" 'GET' @{}
Exigir ($semAuth -eq 401) 'Historico sem JWT deveria retornar 401.'
$desconhecido=CodigoErro "$Api/api/me/clientes/2147483647/historico" 'GET' $vendedor
Exigir ($desconhecido -eq 404) 'Cliente fora da carteira deve retornar 404.'
Write-Host 'OK: 403 para perfil incorreto, 401 sem login e 404 fora da carteira.' -ForegroundColor Green
Write-Host 'AULA 14.1: TESTES DE HISTORICO E AUDITORIA APROVADOS. Nenhum registro foi alterado.' -ForegroundColor Green
