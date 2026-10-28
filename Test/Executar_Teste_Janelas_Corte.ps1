$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5210'

function Exigir($condicao, $mensagem) {
    if (-not $condicao) { throw "FALHA: $mensagem" }
}

Write-Host '1. Consultando a proxima janela comercial (12h ou 19h)...'
$atual = Invoke-RestMethod -Uri "$api/api/janelas-corte/atual"
Exigir ($atual.horaCorte -eq 12 -or $atual.horaCorte -eq 19) 'Horario nao e 12 nem 19.'
Exigir ($atual.fusoHorario -eq 'America/Sao_Paulo') 'Fuso horario inesperado.'
Write-Host "OK: proximo corte em $($atual.dataCorte) as $($atual.horaCorte)h, fuso $($atual.fusoHorario)"
Write-Host "Periodo UTC: $($atual.inicioUtc) ate $($atual.fimUtc)"

Write-Host '2. Consultando exatamente a mesma janela por data e hora...'
$url = "${api}/api/janelas-corte?data=$($atual.dataCorte)&hora=$($atual.horaCorte)"
$detalhe = Invoke-RestMethod -Uri $url
Exigir ($detalhe.dataCorte -eq $atual.dataCorte) 'Datas diferentes.'
Exigir ($detalhe.horaCorte -eq $atual.horaCorte) 'Horas diferentes.'
Exigir ($detalhe.inicioUtc -eq $atual.inicioUtc) 'Inicio do intervalo diferente.'
Exigir ($detalhe.fimUtc -eq $atual.fimUtc) 'Fim do intervalo diferente.'

$pedidos = @($detalhe.pedidos)
$produtos = @($detalhe.produtos)
Exigir ($pedidos.Count -eq [int]$detalhe.pedidosPendentes) 'Contagem de pedidos diferente da lista.'

$unidadesNosPedidos = 0L
$valorNosPedidos = [decimal]0
foreach ($pedido in $pedidos) {
    $unidadesNosPedidos += [long]$pedido.unidadesSolicitadas
    $valorNosPedidos += [decimal]$pedido.valorSolicitado
}
Exigir ($unidadesNosPedidos -eq [long]$detalhe.unidadesSolicitadas) 'Soma das unidades incorreta.'
Exigir ($valorNosPedidos -eq [decimal]$detalhe.valorSolicitado) 'Soma de valores incorreta.'

foreach ($produto in $produtos) {
    $esperada = [Math]::Max([long]0, [long]$produto.quantidadeSolicitada - [long]$produto.estoqueAtualValidoNaDataDoCorte)
    Exigir ($esperada -eq [long]$produto.faltaEstimada) 'Falta estimada divergente.'
}
Write-Host "OK: $($detalhe.pedidosPendentes) pedidos pendentes, $($detalhe.unidadesSolicitadas) unidades, total R$ $($detalhe.valorSolicitado)"
if ($produtos.Count -gt 0) {
    $produtos | Format-Table produtoId, nomeProduto, quantidadeSolicitada, estoqueAtualValidoNaDataDoCorte, faltaEstimada
} else {
    Write-Host 'Nenhum pedido pendente nesta janela. Isso nao representa falha.'
}

Write-Host '3. Conferindo janelas de 12h e 19h na mesma data...'
foreach ($hora in @(12, 19)) {
    $j = Invoke-RestMethod -Uri "${api}/api/janelas-corte?data=$($atual.dataCorte)&hora=$hora"
    $inicio = [datetime]$j.inicioUtc
    $fim = [datetime]$j.fimUtc
    Exigir ($inicio -lt $fim) "Intervalo da janela $hora invalido."
    Write-Host "OK: janela $hora valida, $($j.pedidosPendentes) pedidos pendentes."
}

Write-Host '4. Testando rejeicao de horario invalido (15h)...'
$http400 = $false
try {
    Invoke-RestMethod -Uri "${api}/api/janelas-corte?data=$($atual.dataCorte)&hora=15" | Out-Null
} catch {
    $resposta = $_.Exception.Response
    if ($null -ne $resposta -and [int]$resposta.StatusCode -eq 400) { $http400 = $true }
    else { throw }
}
Exigir $http400 'Era esperado HTTP 400 para o corte das 15h.'
Write-Host 'OK: horario invalido => HTTP 400.'
Write-Host 'Aula 9.3: PREVIA DE JANELAS APROVADA. Nenhum pedido ou lote foi alterado.'
