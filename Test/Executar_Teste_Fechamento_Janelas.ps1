$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5210'

function Exigir($condicao, $mensagem) {
    if (-not $condicao) { throw "FALHA: $mensagem" }
}

function StatusEsperado($url, $json, $status) {
    try {
        Invoke-WebRequest -Uri $url -Method Post -Body $json -ContentType 'application/json' -UseBasicParsing | Out-Null
        throw "FALHA: era esperado HTTP $status."
    } catch {
        $response = $_.Exception.Response
        if ($null -eq $response -or [int]$response.StatusCode -ne $status) { throw }
    }
}

$fuso = try { [TimeZoneInfo]::FindSystemTimeZoneById('America/Sao_Paulo') }
catch { [TimeZoneInfo]::FindSystemTimeZoneById('E. South America Standard Time') }
$agoraLocal = [TimeZoneInfo]::ConvertTimeFromUtc([datetime]::UtcNow, $fuso)
$hoje = $agoraLocal.Date

Write-Host '1. Procurando uma janela ENCERRADA com pedidos pendentes...'
# Escolhe uma janela passada recente para que o teste funcione sem aguardar 12h/19h.
$melhor = $null
$quantidade = -1
foreach ($dias in @(0, 1, 2, 3)) {
    $data = $hoje.AddDays(-$dias).ToString('yyyy-MM-dd')
    foreach ($hora in @(12, 19)) {
        $previa = Invoke-RestMethod -Uri "${api}/api/janelas-corte?data=$data&hora=$hora"
        if ([datetimeoffset]::Parse([string]$previa.fimUtc) -gt [datetimeoffset]::UtcNow) { continue }
        if ([int]$previa.pedidosPendentes -gt $quantidade) {
            $melhor = $previa
            $quantidade = [int]$previa.pedidosPendentes
        }
    }
}
Exigir ($null -ne $melhor) 'Nao ha janela encerrada para testar.'
Write-Host "OK: janela $($melhor.dataCorte) / $($melhor.horaCorte)h, $($melhor.pedidosPendentes) pedidos pendentes na previa."

Write-Host '2. Fechando a janela manualmente (sem baixar estoque)...'
$dados = @{ dataCorte = [string]$melhor.dataCorte; horaCorte = [int]$melhor.horaCorte } | ConvertTo-Json -Compress
$fechamento1 = Invoke-WebRequest -Uri "${api}/api/lotes-processamento/fechar" -Method Post `
    -Body $dados -ContentType 'application/json' -UseBasicParsing
Exigir ($fechamento1.StatusCode -eq 201 -or $fechamento1.StatusCode -eq 200) 'Esperavamos HTTP 201 ou 200 em reexecucao.'
$lote1 = $fechamento1.Content | ConvertFrom-Json
Exigir ($lote1.id -gt 0) 'Lote de processamento sem ID.'

# IMPORTANTE (Windows PowerShell 5.1): uma instrucao if devolvendo arrays
# pode transformar a colecao em um unico objeto ou em $null.
# Enumeramos os pedidos pelo pipeline e SEMPRE convertemos o resultado
# para [object[]], preservando corretamente os casos 0, 1 e N.
[object[]]$pedidosLote = @($lote1.pedidos | Where-Object { $null -ne $_ })
[int]$totalDoLote = $pedidosLote.Length
[int]$totalInformadoPelaApi = $lote1.pedidosIncluidos
Write-Host "DIAGNOSTICO: resposta da API: pedidosIncluidos=$totalInformadoPelaApi; pedidos recebidos=$totalDoLote; HTTP=$($fechamento1.StatusCode)"
if ($totalDoLote -ne $totalInformadoPelaApi) {
    Write-Host 'Resposta JSON para diagnostico:'
    Write-Host ($lote1 | ConvertTo-Json -Depth 6)
    throw "FALHA: Contagem divergente na API: pedidosIncluidos=$totalInformadoPelaApi, lista=$totalDoLote."
}
if ([int]$fechamento1.StatusCode -eq 201) {
    Exigir ($totalDoLote -eq [int]$melhor.pedidosPendentes) 'Janela nova deveria ter a mesma contagem da previa.'
}
Write-Host "OK: lote $($lote1.id) - $totalDoLote pedidos, HTTP $($fechamento1.StatusCode)."
if ($totalDoLote -eq 0) {
    Write-Host 'AVISO: janela sem pedidos pendentes; a logica de associacao nao foi exercitada.'
}

Write-Host '3. Repetindo a mesma requisicao: nao pode criar outro lote...'
$fechamento2 = Invoke-WebRequest -Uri "${api}/api/lotes-processamento/fechar" -Method Post `
    -Body $dados -ContentType 'application/json' -UseBasicParsing
$lote2 = $fechamento2.Content | ConvertFrom-Json
Exigir ($fechamento2.StatusCode -eq 200) 'Fechamento repetido deveria retornar HTTP 200.'
Exigir ($lote2.id -eq $lote1.id) 'Foi criado outro lote para a mesma janela.'
Exigir ($lote2.dataFechamentoUtc -eq $lote1.dataFechamentoUtc) 'Data de fechamento deveria ser imutavel.'
Write-Host "OK: mesmo lote $($lote1.id), sem novo fechamento."

Write-Host '4. Conferindo consultas por ID e por data/hora...'
$porId = Invoke-RestMethod -Uri "${api}/api/lotes-processamento/$($lote1.id)"
$porJanela = Invoke-RestMethod -Uri "${api}/api/lotes-processamento?data=$($melhor.dataCorte)&hora=$($melhor.horaCorte)"
Exigir ($porId.id -eq $lote1.id -and $porJanela.id -eq $lote1.id) 'Busca nao encontrou o mesmo lote.'
Exigir ($porId.pedidosIncluidos -eq $lote1.pedidosIncluidos) 'Quantidade de pedidos mudou.'
Write-Host 'OK: consultas retornaram a fotografia persistida.'

Write-Host '5. Testando regras da janela...'
$invalido = @{ dataCorte = [string]$melhor.dataCorte; horaCorte = 15 } | ConvertTo-Json -Compress
StatusEsperado "${api}/api/lotes-processamento/fechar" $invalido 400
Write-Host 'OK: hora 15 => HTTP 400.'
$amanha = $hoje.AddDays(1).ToString('yyyy-MM-dd')
$futuro = @{ dataCorte = $amanha; horaCorte = 19 } | ConvertTo-Json -Compress
StatusEsperado "${api}/api/lotes-processamento/fechar" $futuro 409
Write-Host 'OK: fechamento de janela futura => HTTP 409.'

Write-Host 'Aula 9.4: FECHAMENTO DE JANELAS APROVADO no banco local.'
Write-Host 'Nenhum estoque foi baixado e nenhum status de pedido foi alterado por este script.'
Write-Host "Lote de processamento $($lote1.id), janela $($melhor.dataCorte) / $($melhor.horaCorte)h."
