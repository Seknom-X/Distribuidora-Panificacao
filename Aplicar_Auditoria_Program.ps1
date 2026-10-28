$ErrorActionPreference = 'Stop'
$program = Join-Path $PSScriptRoot 'Distribuidora.API\Program.cs'
if (-not (Test-Path -LiteralPath $program -PathType Leaf)) {
    throw 'Program.cs nao encontrado. Execute o script na raiz da solucao.'
}
$conteudo = [IO.File]::ReadAllText($program)
$chamada = 'app.UseMiddleware<Distribuidora.API.Middleware.AuditoriaOperacoesMiddleware>();'
if ($conteudo.Contains($chamada)) {
    Write-Host 'OK: middleware de auditoria ja esta configurado no Program.cs.' -ForegroundColor Green
    exit 0
}
$padrao = '(?m)^[ \t]*app\.UseAuthorization\(\);[ \t]*(?://[^\r\n]*)?$'
$ocorrencias = [regex]::Matches($conteudo, $padrao)
if ($ocorrencias.Count -ne 1) {
    throw 'Nao localizei exatamente um app.UseAuthorization();. NENHUMA ALTERACAO feita. Integre manualmente seguindo LEIA-ME_Aula14_2.md.'
}
if ($conteudo.IndexOf('app.UseAuthentication();', [StringComparison]::Ordinal) -lt 0) {
    throw 'Nao encontrei app.UseAuthentication();. NENHUMA ALTERACAO feita.'
}
$mapa = $conteudo.IndexOf('app.MapControllers(', [StringComparison]::Ordinal)
if ($mapa -lt 0 -or $mapa -le $ocorrencias[0].Index) {
    throw 'Nao encontrei app.MapControllers(...) apos UseAuthorization. NENHUMA ALTERACAO feita.'
}
$posicao = $ocorrencias[0].Index + $ocorrencias[0].Length
$quebra = if ($conteudo.Contains("`r`n")) { "`r`n" } else { "`n" }
$novo = $conteudo.Insert($posicao, $quebra + $chamada)
$copia = "$program.bak-aula14-2-$(Get-Date -Format 'yyyyMMddHHmmss')"
Copy-Item -LiteralPath $program -Destination $copia -ErrorAction Stop
[IO.File]::WriteAllText($program, $novo, [Text.UTF8Encoding]::new($false))
Write-Host 'OK: middleware de auditoria inserido no Program.cs.' -ForegroundColor Green
Write-Host "Backup: $copia" -ForegroundColor Cyan
