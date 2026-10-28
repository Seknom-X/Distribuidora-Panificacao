$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$projeto = Join-Path $raiz 'Ferramentas.RecuperarAcesso\Ferramentas.RecuperarAcesso.csproj'
$api = Join-Path $raiz 'Distribuidora.API\Distribuidora.API.csproj'
if (-not (Test-Path -LiteralPath $projeto)) {
    throw 'Ferramenta nao encontrada. Extraia o ZIP inteiro na raiz da solucao.'
}
if (-not (Test-Path -LiteralPath $api)) {
    throw 'Projeto da API nao encontrado. Extraia o ZIP na pasta da solucao.'
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
Write-Host 'Conectando ao banco local da DistribuidoraPanificacao...' -ForegroundColor Cyan
dotnet run --project $projeto
if ($LASTEXITCODE -ne 0) {
    throw "Falha ao executar o utilitario .NET (codigo $LASTEXITCODE)."
}
