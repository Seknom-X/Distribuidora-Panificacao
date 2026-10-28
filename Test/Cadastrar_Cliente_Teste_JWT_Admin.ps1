param(
    [string]$Api = 'http://localhost:5210',
    [int]$VendedorId = 5
)

$ErrorActionPreference = 'Stop'

function Obter-ListaDaApi([string]$Url, [hashtable]$Autorizacao) {
    $r = Invoke-WebRequest -Uri $Url -Headers $Autorizacao -UseBasicParsing -ErrorAction Stop
    $json = [string]$r.Content
    if (-not $json.TrimStart().StartsWith('[')) { throw 'A API nao retornou uma lista JSON.' }
    $objetos = ConvertFrom-Json -InputObject $json
    return ,@($objetos | Where-Object { $null -ne $_ })
}

$enderecoApi = [Uri]$Api
if ($enderecoApi.Host -notin @('127.0.0.1','localhost')) {
    throw 'Por seguranca, este script so permite API em localhost.'
}
if ($VendedorId -le 0) { throw 'VendedorId deve ser positivo.' }

Write-Host "Conferindo carteira do vendedor ID $VendedorId na API local..." -ForegroundColor Cyan
$email = Read-Host 'E-mail do ADMIN'
$senha = Read-Host 'Senha do ADMIN (oculta)' -AsSecureString
$credencial = [PSCredential]::new('temporario',$senha)
$corpoLogin = @{ email = $email; senha = $credencial.GetNetworkCredential().Password } | ConvertTo-Json
try {
    $login = Invoke-RestMethod -Uri "$Api/api/auth/login" -Method Post -Body $corpoLogin `
        -ContentType 'application/json' -ErrorAction Stop
}
finally {
    Remove-Variable corpoLogin,credencial,senha -ErrorAction SilentlyContinue
}
if ($login.usuario.perfil -ne 'Admin') { throw 'A conta informada nao e Admin.' }
$cabecalhos = @{ Authorization = "Bearer $($login.token)" }
$login = $null

$vendedor = Invoke-RestMethod -Uri "$Api/api/vendedores/$VendedorId" -Headers $cabecalhos -ErrorAction Stop
if ($vendedor.ativo -ne $true) { throw "Vendedor ID $VendedorId nao esta ativo." }
Write-Host "OK: Vendedor ID $($vendedor.id), Nome=$($vendedor.nome), Ativo=$($vendedor.ativo)."

$carteira = Obter-ListaDaApi -Url "$Api/api/clientes?vendedorId=$VendedorId&ativo=true" -Autorizacao $cabecalhos
Write-Host "Clientes ativos vinculados no banco: $($carteira.Count)."
if ($carteira.Count -gt 0) {
    $carteira | Format-Table id,razaoSocial,vendedorId,ativo -AutoSize
    Write-Host 'Ja existe cliente ativo. Rode novamente o teste da Aula 10.2, logado no vendedor correto.' -ForegroundColor Green
    return
}

Write-Host 'Nenhum cliente ativo foi encontrado para este vendedor.' -ForegroundColor Yellow
$confirmar = Read-Host "Criar uma PADARIA FICTICIA de teste para Vendedor $VendedorId? (S/N)"
if ($confirmar.Trim().ToUpperInvariant() -ne 'S') {
    Write-Host 'Nenhum cadastro foi realizado.'
    return
}

$razao = Read-Host 'Nome da padaria ficticia (Enter = Padaria JWT Aula 10)'
if ([string]::IsNullOrWhiteSpace($razao)) { $razao = 'Padaria JWT Aula 10' }
$nome = $razao.Trim()
if ($nome.Length -lt 3 -or $nome.Length -gt 180) { throw 'Nome deve ter de 3 a 180 caracteres.' }

# DTO desta aula aceita exatamente 14 digitos; para teste local usa um CNPJ ficticio.
$digitos = '9' + (-join (1..13 | ForEach-Object { [string](Get-Random -Minimum 0 -Maximum 10) }))
$dados = @{
    razaoSocial = $nome
    cnpj = $digitos
    telefone = '11999999999'
    endereco = 'Rua Exemplo 100'
    cidade = 'Sao Paulo'
    estado = 'SP'
    ordemVisita = 1
    vendedorId = $VendedorId
} | ConvertTo-Json -Depth 4

try {
    $cliente = Invoke-RestMethod -Uri "$Api/api/clientes" -Headers $cabecalhos `
        -Method Post -ContentType 'application/json' -Body $dados -ErrorAction Stop
}
catch {
    if ($null -ne $_.Exception.Response) {
        Write-Host "HTTP $([int]$_.Exception.Response.StatusCode) no cadastro do cliente." -ForegroundColor Red
    }
    throw
}
Write-Host "OK: Cliente de TESTE cadastrado: ID=$($cliente.id), vendedorId=$($cliente.vendedorId), ativo=$($cliente.ativo)." -ForegroundColor Green
Write-Host 'Agora repita o script JWT de pedido com o login desse vendedor.'
Write-Host 'O cliente ficticio permanece no MySQL. Nao e necessario criar migration.'
