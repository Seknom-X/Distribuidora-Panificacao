$ErrorActionPreference = 'Stop'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Api__BaseUrl = 'http://localhost:5210/'
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'Iniciando Distribuidora.Web em http://localhost:5220. Use Ctrl+C para encerrar.'
dotnet run --no-launch-profile --project (Join-Path $PSScriptRoot 'Distribuidora.Web') --urls http://localhost:5220
if ($LASTEXITCODE -ne 0) { throw 'Falha ao iniciar. Confira a mensagem acima e se a porta esta ocupada.' }
