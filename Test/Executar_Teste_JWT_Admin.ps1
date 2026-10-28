param([string]$Api = 'http://localhost:5210', [string]$EmailAdmin = '')
$ErrorActionPreference='Stop'
function StatusHttp([scriptblock]$Executar) {
    try { & $Executar | Out-Null; return 200 }
    catch {
        if ($null -eq $_.Exception.Response) { throw }
        return [int]$_.Exception.Response.StatusCode
    }
}
function Garantir($ok,$texto){if(-not $ok){throw "FALHA: $texto"} Write-Host "OK: $texto" -ForegroundColor Green}
if (-not $EmailAdmin) { $EmailAdmin=Read-Host 'Email do Admin criado na Aula 10' }
$pw=Read-Host 'Senha do Admin (oculta)' -AsSecureString
$cred=New-Object System.Management.Automation.PSCredential('usuario',$pw)
$senha=$cred.GetNetworkCredential().Password
$dados=@{email=$EmailAdmin;senha=$senha}|ConvertTo-Json
$senha=$null; $cred=$null; $pw=$null
Write-Host '1. Sem token: tentando GET /api/produtos'
$s=StatusHttp { Invoke-RestMethod "$Api/api/produtos" -ErrorAction Stop }
Garantir ($s -eq 401) 'Sem JWT -> HTTP 401'
Write-Host '2. Token falso'
$s=StatusHttp { Invoke-RestMethod "$Api/api/produtos" -Headers @{Authorization='Bearer token-invalido'} -ErrorAction Stop }
Garantir ($s -eq 401) 'JWT falso -> HTTP 401'
Write-Host '3. Login de Admin'
$login=Invoke-RestMethod "$Api/api/auth/login" -Method Post -Body $dados -ContentType 'application/json; charset=utf-8'
Garantir (-not [string]::IsNullOrWhiteSpace($login.token)) 'Login Admin retorna token JWT'
Garantir ($login.usuario.perfil -eq 'Admin') 'Perfil do token: Admin'
$headers=@{Authorization="Bearer $($login.token)"}
Write-Host '4. Chamando rotas administrativas'
$null=Invoke-RestMethod "$Api/api/produtos" -Headers $headers
Garantir $true 'Admin acessa /api/produtos'
$me=Invoke-RestMethod "$Api/api/auth/me" -Headers $headers
Garantir ($me.perfil -eq 'Admin') 'GET /api/auth/me: Admin'
$s=StatusHttp { Invoke-RestMethod "$Api/api/me/clientes" -Headers $headers -ErrorAction Stop }
Garantir ($s -eq 403) 'Admin nao e vendedor: /api/me/clientes -> HTTP 403'
Write-Host 'TESTES JWT ADMIN APROVADOS. Nenhum pedido ou estoque foi alterado.' -ForegroundColor Green
