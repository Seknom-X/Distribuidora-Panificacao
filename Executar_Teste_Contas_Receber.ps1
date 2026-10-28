param(
    [ValidateRange(1,2147483647)][int]$VendaId = 1,
    [ValidateRange(0,2147483647)][int]$CondicaoId = 0,
    [switch]$RegistrarPagamentoTeste,
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
function CodigoErro([string]$Uri,[string]$Metodo,$Headers,$Corpo=$null) {
    try { $null=Chamar $Uri $Metodo $Headers $Corpo; return 200 }
    catch [System.Net.WebException] {
        if ($null -ne $_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}
$Api=$Api.TrimEnd('/')
if ($Api -ne 'http://localhost:5210' -and $Api -ne 'http://127.0.0.1:5210') {
    throw 'Script de testes aceita somente localhost na porta 5210.'
}
Write-Host 'Aula 13 - Contas a receber e pagamentos parciais' -ForegroundColor Cyan
$email=(Read-Host 'Email do Admin').Trim()
$senha=Read-Host 'Senha do Admin (oculta)' -AsSecureString
$cred=[PSCredential]::new('login',$senha)
$loginCorpo=@{email=$email; senha=$cred.GetNetworkCredential().Password}
$login=Chamar "$Api/api/auth/login" 'POST' @{} $loginCorpo
$loginCorpo=$null; $cred=$null; $senha=$null
Exigir ($login.Status -eq 200 -and $login.Dados.usuario.perfil -eq 'Admin') 'Login precisa ser Admin.'
$headers=@{Authorization="Bearer $($login.Dados.token)"}

Write-Host '1. Conferindo venda e condicao de pagamento...' -ForegroundColor Cyan
$venda=(Chamar "$Api/api/vendas/$VendaId" 'GET' $headers).Dados
Exigir ([int]$venda.id -eq $VendaId) 'Venda nao encontrada.'
$valor=[decimal]$venda.valorTotal
$condicoes=@((Chamar "$Api/api/condicoes-pagamento?ativa=true" 'GET' $headers).Dados | Where-Object {$null -ne $_})
Exigir ($condicoes.Count -gt 0) 'Sem condicoes ativas. Execute e valide a Aula 12 primeiro.'
if ($CondicaoId -eq 0) { $CondicaoId=[int]$condicoes[0].id }
Exigir (@($condicoes | Where-Object {[int]$_.id -eq $CondicaoId}).Count -eq 1) 'Condicao informada nao e ativa.'
$simulacao=(Chamar "$Api/api/vendas/$VendaId/parcelamento/simular?condicaoId=$CondicaoId" 'GET' $headers).Dados
Exigir ([decimal]$simulacao.totalParcelado -eq $valor) 'Simulacao nao fecha total.'
Write-Host "OK: venda $VendaId, condicao $CondicaoId, valor $valor." -ForegroundColor Green

Write-Host '2. Gerando conta a receber (ou consultando a existente)...' -ForegroundColor Cyan
$conta=Chamar "$Api/api/contas-receber/da-venda/$VendaId" 'POST' $headers @{condicaoPagamentoId=$CondicaoId}
Exigir ($conta.Status -eq 201 -or $conta.Status -eq 200) 'Gerar conta deveria ser 201 ou 200.'
$idConta=[int]$conta.Dados.id
$conta2=Chamar "$Api/api/contas-receber/da-venda/$VendaId" 'POST' $headers @{condicaoPagamentoId=$CondicaoId}
Exigir ($conta2.Status -eq 200 -and [int]$conta2.Dados.id -eq $idConta) 'Reenvio deveria manter mesmo ID.'
$detalhe=(Chamar "$Api/api/contas-receber/$idConta" 'GET' $headers).Dados
$parcelas=@($detalhe.parcelas | Where-Object { $null -ne $_ })
Exigir ($parcelas.Count -ge 1) 'Conta precisa ter parcelas.'
$soma=[decimal](($parcelas | Measure-Object -Property valorOriginal -Sum).Sum)
Exigir ($soma -eq $valor) 'Soma das parcelas nao bate com venda.'
Write-Host "OK: conta $idConta, valor $soma, reenvio com mesmo ID." -ForegroundColor Green
$parcelas | Format-Table id,numero,vencimento,valorOriginal,valorPago,saldoDevedor -AutoSize
$estoqueSemJwt=CodigoErro "$Api/api/contas-receber/$idConta" 'GET' @{}
Exigir ($estoqueSemJwt -eq 401) 'Sem JWT deve retornar 401.'
Write-Host 'OK: rota protegida => HTTP 401 sem JWT.' -ForegroundColor Green

if (-not $RegistrarPagamentoTeste) {
    Write-Host 'Aula 13: GERACAO DE CONTAS E CONSULTA APROVADAS.' -ForegroundColor Green
    Write-Host 'Nenhum pagamento foi registrado neste teste. Para testar pagamento de R$ 5,00 use -RegistrarPagamentoTeste.' -ForegroundColor Yellow
    return
}
$primeira=$parcelas[0]
$saldo=[decimal]$primeira.saldoDevedor
Exigir ($saldo -ge 5) 'A primeira parcela precisa ter saldo >= R$ 5,00 para este teste.'
Write-Host 'ATENCAO: sera registrado um pagamento ficticio de R$ 5,00 na primeira parcela.' -ForegroundColor Yellow
$confirmacao=Read-Host 'Digite SIM para registrar esse pagamento no MySQL de desenvolvimento'
if ($confirmacao -cne 'SIM') { Write-Host 'Cancelado. Nao foi registrado pagamento.'; return }

Write-Host '3. Registrando pagamento parcial e verificando idempotencia...' -ForegroundColor Cyan
$chave=[Guid]::NewGuid().ToString('D')
$corpo=@{chaveOperacao=$chave; valor=5.00; formaPagamento='Dinheiro'; observacao='Teste academico Aula 13'}
$url="$Api/api/contas-receber/parcelas/$($primeira.id)/recebimentos"
$primeiro=Chamar $url 'POST' $headers $corpo
Exigir ($primeiro.Status -eq 201) 'Primeiro pagamento deveria ser 201.'
$segundo=Chamar $url 'POST' $headers $corpo
Exigir ($segundo.Status -eq 200 -and [int]$segundo.Dados.id -eq [int]$primeiro.Dados.id) 'Repetir chave nao pode gerar recebimento novo.'
$novo=(Chamar "$Api/api/contas-receber/$idConta" 'GET' $headers).Dados
$novaPrimeira=@($novo.parcelas | Where-Object {[int]$_.id -eq [int]$primeira.id})[0]
Exigir ([decimal]$novaPrimeira.valorPago -eq ([decimal]$primeira.valorPago + 5)) 'Valor pago nao aumentou exatamente 5.'
Exigir ([decimal]$novo.saldoDevedor -eq ([decimal]$detalhe.saldoDevedor - 5)) 'Saldo devedor nao diminuiu exatamente 5.'
$erroChave=CodigoErro $url 'POST' $headers @{chaveOperacao=$chave;valor=4.00;formaPagamento='Dinheiro';observacao='Teste academico Aula 13'}
Exigir ($erroChave -eq 409) 'Reutilizar a chave com outro valor deveria dar HTTP 409.'
$erroExcesso=CodigoErro $url 'POST' $headers @{chaveOperacao=[Guid]::NewGuid().ToString('D');valor=10000000.00;formaPagamento='Dinheiro'}
Exigir ($erroExcesso -eq 409) 'Pagar acima do saldo deveria retornar HTTP 409.'
$erroZero=CodigoErro $url 'POST' $headers @{chaveOperacao=[Guid]::NewGuid().ToString('D');valor=0;formaPagamento='Dinheiro'}
Exigir ($erroZero -eq 400) 'Valor zero deveria retornar HTTP 400.'
Write-Host "Aula 13: PAGAMENTO PARCIAL APROVADO. Conta $idConta; recebimento $($primeiro.Dados.id); saldo $($novo.saldoDevedor)." -ForegroundColor Green
Write-Host 'Nao execute este teste de pagamento repetidamente sem necessidade: cada nova execucao confirmada utiliza nova chave e abate R$ 5.' -ForegroundColor Yellow
