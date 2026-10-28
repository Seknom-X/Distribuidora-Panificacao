$ErrorActionPreference = 'Stop'

$arquivo = Join-Path (Join-Path $PSScriptRoot 'Distribuidora.API') 'Program.cs'
if (-not (Test-Path -LiteralPath $arquivo)) {
    throw "Arquivo nao encontrado: $arquivo"
}

$conteudo = [System.IO.File]::ReadAllText($arquivo)
$quebra = if ($conteudo.Contains("`r`n")) { "`r`n" } else { "`n" }

if ($conteudo -match 'AddSegurancaDistribuidora|CriarAdminLocalAsync') {
    throw 'Program.cs ja possui partes da configuracao JWT. Confira manualmente antes de tentar novamente; nao vamos duplicar registros.'
}

# Aceita espacos variados, WebApplication explicito e comentarios no fim da linha.
$regraBuild = '(?m)^[ \t]*(?:(?:var|WebApplication)\s+)?app\s*=\s*builder\s*\.\s*Build\s*\(\s*\)\s*;[ \t]*(?://[^\r\n]*)?\r?$'
$regraAuth = '(?m)^[ \t]*app\s*\.\s*UseAuthorization\s*\(\s*\)\s*;[ \t]*(?://[^\r\n]*)?\r?$'
$regraMap = '(?m)^[ \t]*app\s*\.\s*MapControllers\s*\(\s*\)\s*;[ \t]*(?://[^\r\n]*)?\r?$'
$regraAutenticacao = '(?m)^[ \t]*app\s*\.\s*UseAuthentication\s*\('

$builds = [regex]::Matches($conteudo, $regraBuild)
if ($builds.Count -ne 1) {
    Write-Host "A integracao automatica foi interrompida: localizei $($builds.Count) linha(s) de Build. Nenhum arquivo foi alterado." -ForegroundColor Yellow
    Write-Host 'Trechos relevantes do Program.cs:' -ForegroundColor Yellow
    Select-String -InputObject $conteudo -Pattern 'builder\s*\.\s*Build\s*\(|app\s*\.\s*UseAuthorization\s*\(|app\s*\.\s*MapControllers\s*\(' -AllMatches | ForEach-Object {
        foreach ($m in $_.Matches) { Write-Host "Encontrado: $($m.Value)" }
    }
    throw 'O Program.cs possui uma estrutura diferente. Nao tente repetir o script antigo; envie o texto do Program.cs sem segredos para fazermos a integracao manual.'
}

$authCount = [regex]::Matches($conteudo, $regraAuth).Count
$mapCount = [regex]::Matches($conteudo, $regraMap).Count
if ($authCount -gt 1) { throw 'Mais de um app.UseAuthorization() encontrado. Integracao manual necessaria.' }
if ($authCount -eq 0 -and $mapCount -ne 1) {
    throw 'Nao encontrei um unico app.MapControllers() para instalar a autenticacao. Integracao manual necessaria.'
}
if ([regex]::IsMatch($conteudo, $regraAutenticacao)) {
    throw 'Ja existe app.UseAuthentication(). Integracao manual necessaria para evitar duplicacao.'
}
if ($conteudo -match '(?m)^[ \t]*using\s+Distribuidora\.API\.Security\s*;') {
    throw 'O using Distribuidora.API.Security ja existe. Revise o arquivo para evitar configuracao parcial.'
}

$backup = Join-Path $env:TEMP ('DistribuidoraProgram_Aula10_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff') + '.cs')
Copy-Item -LiteralPath $arquivo -Destination $backup -ErrorAction Stop

$conteudo = [regex]::Replace($conteudo, $regraBuild, {
    param($m)
    $linhaOriginal = $m.Value.TrimEnd("`r")
    return 'builder.Services.AddSegurancaDistribuidora(builder.Configuration);' + $quebra + $quebra +
        $linhaOriginal + $quebra +
        'if (args.Contains("--criar-admin", StringComparer.OrdinalIgnoreCase))' + $quebra +
        '{' + $quebra +
        '    await app.CriarAdminLocalAsync();' + $quebra +
        '    return;' + $quebra +
        '}'
})

if ($authCount -eq 1) {
    $conteudo = [regex]::Replace($conteudo, $regraAuth, {
        param($m)
        return 'app.UseAuthentication();' + $quebra + $m.Value.TrimEnd("`r")
    })
} else {
    $conteudo = [regex]::Replace($conteudo, $regraMap, {
        param($m)
        return 'app.UseAuthentication();' + $quebra + 'app.UseAuthorization();' + $quebra + $m.Value.TrimEnd("`r")
    })
}

$conteudo = 'using Distribuidora.API.Security;' + $quebra + $conteudo

if ([regex]::Matches($conteudo, 'AddSegurancaDistribuidora\(').Count -ne 1 -or
    [regex]::Matches($conteudo, 'app\.UseAuthentication\(').Count -ne 1 -or
    [regex]::Matches($conteudo, 'app\.UseAuthorization\(').Count -ne 1 -or
    [regex]::Matches($conteudo, 'CriarAdminLocalAsync\(').Count -ne 1) {
    throw "Verificacao interna falhou. Program.cs ORIGINAL preservado em $arquivo. Backup em $backup"
}

[System.IO.File]::WriteAllText($arquivo, $conteudo, [System.Text.UTF8Encoding]::new($false))
Write-Host 'OK: Integracao JWT aplicada ao Program.cs.' -ForegroundColor Green
Write-Host "Backup: $backup" -ForegroundColor Cyan
Write-Host 'Verifique o Program.cs e execute dotnet build antes de criar migrations.'
