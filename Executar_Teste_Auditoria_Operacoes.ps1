param(
    [ValidateRange(1, 2147483647)][int]$PedidoId = 1,
    [string]$Api = 'http://localhost:5210'
)
$ErrorActionPreference = 'Stop'
if ($Api -notin @('http://localhost:5210','http://127.0.0.1:5210')) {
    throw 'Apenas localhost:5210 e permitido neste teste.'
}
function Exigir([bool]$ok,[string]$mensagem) {
    if (-not $ok) { throw "FALHA: $mensagem" }
}
function Requisicao([string]$url,[string]$metodo,$cabecalhos,$corpo=$null) {
    $p = @{Uri=$url;Method=$metodo;Headers=$cabecalhos;UseBasicParsing=$true;ErrorAction='Stop'}
    if ($null -ne $corpo) {
        $p['ContentType']='application/json; charset=utf-8'
        $p['Body']=[Text.Encoding]::UTF8.GetBytes(($corpo | ConvertTo-Json -Depth 10 -Compress))
    }
    $r=Invoke-WebRequest @p
    $dados=if ([string]::IsNullOrWhiteSpace($r.Content)) { $null } else { $r.Content | ConvertFrom-Json }
    return [pscustomobject]@{Status=[int]$r.StatusCode;Dados=$dados}
}
function StatusSemToken([string]$url) {
    try { $null=Requisicao $url 'GET' @{}; return 200 }
    catch [System.Net.WebException] {
        if ($null -ne $_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}
Write-Host 'Aula 14.2 - Trilha persistente de operacoes' -ForegroundColor Cyan
$email=(Read-Host 'Email do Admin').Trim()
$senha=Read-Host 'Senha do Admin (oculta)' -AsSecureString
$cred=[PSCredential]::new('login', $senha)
$login=Requisicao "$Api/api/auth/login" 'POST' @{} @{email=$email;senha=$cred.GetNetworkCredential().Password}
$cred=$null; $senha=$null
Exigir ($login.Status -eq 200 -and $login.Dados.usuario.perfil -eq 'Admin') 'Login precisa ser Admin.'
$cab=@{Authorization="Bearer $($login.Dados.token)"}

Write-Host '1. Confirmando que a venda ja existe (evita criar nova venda no teste)...' -ForegroundColor Cyan
$venda=(Requisicao "$Api/api/vendas/por-pedido/$PedidoId" 'GET' $cab).Dados
Exigir ([int]$venda.pedidoId -eq $PedidoId) 'Venda inexistente. Conclua primeiro Aula 11.'

Write-Host '2. Consultando o ultimo ID registrado...' -ForegroundColor Cyan
$anteriores = @((Requisicao "$Api/api/admin/registros-auditoria?limite=100" 'GET' $cab).Dados | Where-Object { $null -ne $_ })
$maior=0L
foreach($registro in $anteriores) { if ([long]$registro.id -gt $maior) { $maior=[long]$registro.id } }

Write-Host '3. Reenviando uma venda JA EXISTENTE (idempotente, sem baixa)...' -ForegroundColor Cyan
$resposta=Requisicao "$Api/api/vendas/do-pedido/$PedidoId" 'POST' $cab
Exigir ($resposta.Status -eq 200) 'Venda ja existente deveria retornar 200 sem gerar outra.'
Exigir ([int]$resposta.Dados.id -eq [int]$venda.id) 'ID da venda nao pode mudar.'

Write-Host '4. Buscando o novo registro da operacao autenticada...' -ForegroundColor Cyan
$depois=@((Requisicao "$Api/api/admin/registros-auditoria?limite=100" 'GET' $cab).Dados | Where-Object { $null -ne $_ })
$esperado="/api/vendas/do-pedido/$PedidoId"
$achados=@($depois | Where-Object { [long]$_.id -gt $maior -and $_.metodo -eq 'POST' -and $_.caminho -eq $esperado -and [int]$_.statusHttp -eq 200 })
Exigir ($achados.Count -gt 0) 'Auditoria nao registrou o POST. Confira se middleware esta no Program.cs e migration foi aplicada.'
$achados | Format-Table id,usuarioId,perfil,metodo,caminho,statusHttp,dataUtc -AutoSize

Write-Host '5. Conferindo protecao do historico...' -ForegroundColor Cyan
$sem=StatusSemToken "$Api/api/admin/registros-auditoria"
Exigir ($sem -eq 401) 'Consulta sem JWT deve retornar HTTP 401.'
Write-Host 'OK: 401 sem JWT. Nenhum estoque, venda ou pagamento foi alterado pelo teste.' -ForegroundColor Green
Write-Host 'Aula 14.2: TESTE BASICO DE AUDITORIA APROVADO.' -ForegroundColor Green
Write-Host 'A trilha e informativa, nao atomica com a operacao original.' -ForegroundColor Yellow
