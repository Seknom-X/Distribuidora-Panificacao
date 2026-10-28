param(
    [string]$Api = 'http://localhost:5210',
    [ValidateSet(2, 12)][int]$SenhaMinima = 2
)

$ErrorActionPreference = 'Stop'

$enderecoApi = [Uri]$Api
if ($enderecoApi.Host -notin @('localhost', '127.0.0.1')) {
    throw 'Por seguranca, este script so aceita API em localhost ou 127.0.0.1.'
}

function Mostrar-ErroDaApi {
    param([System.Management.Automation.ErrorRecord]$Erro)
    $resposta = $Erro.Exception.Response
    $codigo = if ($null -ne $resposta) { [int]$resposta.StatusCode } else { 'desconhecido' }
    Write-Host "HTTP $codigo retornado pela API." -ForegroundColor Red

    $corpo = $null
    if ($Erro.ErrorDetails -and $Erro.ErrorDetails.Message) {
        $corpo = $Erro.ErrorDetails.Message
    } elseif ($resposta) {
        try {
            $stream = $resposta.GetResponseStream()
            if ($stream) {
                $leitor = New-Object System.IO.StreamReader($stream)
                $corpo = $leitor.ReadToEnd()
                $leitor.Dispose()
            }
        } catch {}
    }
    if ($corpo) {
        try {
            $erroJson = $corpo | ConvertFrom-Json
            if ($erroJson.mensagem) { Write-Host "Motivo: $($erroJson.mensagem)" -ForegroundColor Yellow }
            elseif ($erroJson.errors) {
                foreach ($prop in $erroJson.errors.PSObject.Properties) {
                    Write-Host "$($prop.Name): $($prop.Value -join '; ')" -ForegroundColor Yellow
                }
            } elseif ($erroJson.title) { Write-Host "Motivo: $($erroJson.title)" -ForegroundColor Yellow }
            else { Write-Host $corpo -ForegroundColor Yellow }
        } catch { Write-Host $corpo -ForegroundColor Yellow }
    } else {
        Write-Host 'A API nao forneceu o corpo do erro ao PowerShell.' -ForegroundColor Yellow
    }
    throw 'Cadastro interrompido. Corrija o problema indicado acima e execute novamente.'
}

Write-Host 'Aula 10.1 - Cadastro de usuario Vendedor' -ForegroundColor Cyan
$emailAdmin = Read-Host 'Email do Admin'
$senhaAdminSec = Read-Host 'Senha do Admin (oculta)' -AsSecureString
$senhaAdmin = (New-Object System.Management.Automation.PSCredential('x', $senhaAdminSec)).GetNetworkCredential().Password
try {
    $login = Invoke-RestMethod -Uri "$Api/api/auth/login" -Method Post -ContentType 'application/json; charset=utf-8' `
        -Body (@{ email = $emailAdmin; senha = $senhaAdmin } | ConvertTo-Json)
} catch { $senhaAdmin = $null; Mostrar-ErroDaApi $_ }
$senhaAdmin = $null
$senhaAdminSec = $null
if ($login.usuario.perfil -ne 'Admin') { throw 'A conta informada nao possui perfil Admin.' }
$headers = @{ Authorization = "Bearer $($login.token)" }

try {
    $resultado = Invoke-RestMethod -Uri "$Api/api/vendedores?ativo=true" -Headers $headers
} catch { Mostrar-ErroDaApi $_ }

# O Invoke-RestMethod pode retornar uma colecao como um unico objeto.
# Passar pelo pipeline expande a colecao antes da contagem e exibicao.
$vendedores = @($resultado | Where-Object { $null -ne $_ -and $_.PSObject.Properties['Id'] -and $_.Ativo -eq $true })

if ($vendedores.Count -gt 0) {
    Write-Host "Vendedores ativos encontrados: $($vendedores.Count)" -ForegroundColor Green
    $vendedores | Select-Object Id, Nome, Email, Ativo | Format-Table -AutoSize | Out-Host
    $idVendedor = 0
    $entrada = Read-Host 'ID de um vendedor ativo (ou N para cadastrar um novo)'
    if ($entrada -match '^\s*\d+\s*$') {
        $idVendedor = [int]$entrada.Trim()
        if (@($vendedores | Where-Object { [int]$_.Id -eq $idVendedor }).Count -ne 1) {
            throw "O vendedor ID $idVendedor nao consta entre os vendedores ativos listados. Escolha um ID valido."
        }
    } elseif ($entrada.Trim().ToUpperInvariant() -ne 'N') {
        throw 'Resposta invalida. Execute o script novamente e escolha um ID ou N.'
    }
} else {
    Write-Host 'Nenhum vendedor ATIVO foi retornado pela API.' -ForegroundColor Yellow
    Write-Host 'Vamos cadastrar o vendedor primeiro.' -ForegroundColor Cyan
    $idVendedor = 0
}

if ($idVendedor -eq 0) {
    $nomeVendedor = Read-Host 'Nome do vendedor (minimo 3 caracteres)'
    $emailVendedor = Read-Host 'Email de cadastro do vendedor (unico)'
    $telefoneVendedor = Read-Host 'Telefone do vendedor'
    $corpoVendedor = @{
        nome = $nomeVendedor
        email = $emailVendedor
        telefone = $telefoneVendedor
    } | ConvertTo-Json
    try {
        $vendedorNovo = Invoke-RestMethod -Uri "$Api/api/vendedores" -Method Post -Headers $headers `
            -ContentType 'application/json; charset=utf-8' -Body $corpoVendedor
    } catch { Mostrar-ErroDaApi $_ }
    $idVendedor = [int]$vendedorNovo.Id
    Write-Host "OK: vendedor criado. ID=$idVendedor; ativo=$($vendedorNovo.Ativo)." -ForegroundColor Green
}

$nomeUsuario = Read-Host 'Nome do usuario vendedor (minimo 2 caracteres)'
$emailUsuario = Read-Host 'Email de login do vendedor (nao pode ser email de outro USUARIO)'
$senha1Segura = Read-Host "Senha (minimo $SenhaMinima caracteres, oculta)" -AsSecureString
$senha2Segura = Read-Host 'Repita a senha' -AsSecureString
$senha1 = (New-Object System.Management.Automation.PSCredential('x', $senha1Segura)).GetNetworkCredential().Password
$senha2 = (New-Object System.Management.Automation.PSCredential('x', $senha2Segura)).GetNetworkCredential().Password
if ($senha1.Length -lt $SenhaMinima -or $senha1.Length -gt 128 -or $senha1 -ne $senha2) {
    $senha1 = $null; $senha2 = $null
    throw "Senha invalida ou confirmacao diferente. Minimo configurado no script: $SenhaMinima."
}
$corpoUsuario = @{
    nome = $nomeUsuario
    email = $emailUsuario
    senha = $senha1
    vendedorId = $idVendedor
} | ConvertTo-Json
$senha1 = $null; $senha2 = $null
$senha1Segura = $null; $senha2Segura = $null
try {
    $usuarioNovo = Invoke-RestMethod -Uri "$Api/api/auth/usuarios/vendedor" -Method Post `
        -Headers $headers -ContentType 'application/json; charset=utf-8' -Body $corpoUsuario
} catch { $corpoUsuario = $null; Mostrar-ErroDaApi $_ }
$corpoUsuario = $null
Write-Host "OK: Usuario Vendedor criado. ID=$($usuarioNovo.Id), email=$($usuarioNovo.Email), vendedorId=$($usuarioNovo.VendedorId)." -ForegroundColor Green
Write-Host 'Agora execute: .\Executar_Teste_JWT_Vendedor.ps1' -ForegroundColor Cyan
Write-Host 'OBS: se a API ainda exigir 12 caracteres, mantenha o padrao ou ajuste CriarUsuarioVendedorDto.' -ForegroundColor Yellow
