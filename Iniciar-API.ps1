$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'Iniciando Distribuidora.API em http://localhost:5210. Use Ctrl+C para encerrar.'
dotnet run --no-launch-profile --project (Join-Path $PSScriptRoot 'Distribuidora.API') --urls http://localhost:5210
if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar. Confira a mensagem acima e se a porta esta ocupada.' }
