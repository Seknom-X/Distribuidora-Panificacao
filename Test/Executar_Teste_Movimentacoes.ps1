# Aula 8: altera saldos e registra movimentos no MySQL LOCAL (porta 3307).
# Cria grupo, produto, 2 lotes e movimentos; nao execute em producao.
$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5210/api'
$tag = [Guid]::NewGuid().ToString('N').Substring(0,8)

function PostJson([string]$uri, $corpo) {
    Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json' -Body ($corpo | ConvertTo-Json -Depth 6) -ErrorAction Stop
}
function EsperarErro([scriptblock]$bloco, [int]$statusEsperado, [string]$descricao) {
    try {
        & $bloco | Out-Null
        throw "Falha: $descricao deveria retornar HTTP $statusEsperado."
    } catch {
        $recebido = $null
        if ($_.Exception.Response) { $recebido = [int]$_.Exception.Response.StatusCode }
        if ($recebido -ne $statusEsperado) { throw }
        Write-Host "OK: $descricao => HTTP $recebido"
    }
}

Write-Host '1. Preparando produto novo e lotes de teste...'
$grupo = PostJson "$api/grupos-produto" @{ nome = "Teste Movimentacoes $tag"; descricao = 'Exercicio em Docker local' }
$produto = PostJson "$api/produtos" @{
    nome = "Fermento Mov $tag"; unidadeMedida = 'UN'; precoVenda = 12.50; grupoProdutoId = [int]$grupo.id
}
$loteA = PostJson "$api/produtos/$($produto.id)/lotes" @{
    codigoLote = "MOV-$tag-A"; dataValidade = (Get-Date).ToUniversalTime().AddDays(30).ToString('yyyy-MM-dd'); quantidadeDisponivel = 12
}
$loteB = PostJson "$api/produtos/$($produto.id)/lotes" @{
    codigoLote = "MOV-$tag-B"; dataValidade = (Get-Date).ToUniversalTime().AddDays(60).ToString('yyyy-MM-dd'); quantidadeDisponivel = 8
}
$baseA = "$api/produtos/$($produto.id)/lotes/$($loteA.id)/movimentacoes"
$baseB = "$api/produtos/$($produto.id)/lotes/$($loteB.id)/movimentacoes"

Write-Host '2. Registrando saida de 3 no lote A (12 -> 9)...'
$saida = @{ tipo = 2; quantidade = 3; chaveOperacao = [Guid]::NewGuid().ToString('D'); observacao = 'Saida didatica de teste' }
$m1 = PostJson $baseA $saida
if ([int]$m1.saldoAposMovimentacao -ne 9) { throw 'Saldo apos saida deveria ser 9.' }
Write-Host "OK: movimento $($m1.id), novo saldo = $($m1.saldoAposMovimentacao)"

Write-Host '3. Reenviando a mesma operacao (nao pode baixar novamente)...'
$m1Repetido = PostJson $baseA $saida
if ([int]$m1Repetido.id -ne [int]$m1.id) { throw 'Reenvio criou outra movimentacao!' }
$loteVerificado = Invoke-RestMethod "$api/produtos/$($produto.id)/lotes/$($loteA.id)"
if ([int]$loteVerificado.quantidadeDisponivel -ne 9) { throw 'Reenvio duplicou a saida de estoque!' }
Write-Host "OK: reenvio idempotente, saldo continua 9"

Write-Host '4. Registrando entrada de 5 no lote B (8 -> 13)...'
$entrada = @{ tipo = 1; quantidade = 5; chaveOperacao = [Guid]::NewGuid().ToString('D'); observacao = 'Entrada complementar didatica' }
$m2 = PostJson $baseB $entrada
if ([int]$m2.saldoAposMovimentacao -ne 13) { throw 'Saldo apos entrada deveria ser 13.' }
Write-Host "OK: saldo do lote B = $($m2.saldoAposMovimentacao)"

Write-Host '5. Consultando estoque do produto (9 + 13 = 22)...'
$estoque = Invoke-RestMethod "$api/produtos/$($produto.id)/estoque"
if ([long]$estoque.estoqueFisico -ne 22 -or [long]$estoque.estoqueValido -ne 22 -or [long]$estoque.estoqueVencido -ne 0) {
    throw 'Saldo fisico ou valido incorreto; esperado 22.'
}
Write-Host 'OK: estoque fisico 22, valido 22, vencido 0.'

Write-Host '6. Testando regras de seguranca de saldo...'
$excesso = @{ tipo = 2; quantidade = 1000; chaveOperacao = [Guid]::NewGuid().ToString('D'); observacao = 'Teste sem saldo' }
EsperarErro { PostJson $baseA $excesso } 409 'Saida maior que saldo'
$zero = @{ tipo = 2; quantidade = 0; chaveOperacao = [Guid]::NewGuid().ToString('D'); observacao = 'Quantidade zero' }
EsperarErro { PostJson $baseA $zero } 400 'Quantidade zero'
$mesmaChaveOutroPedido = @{ tipo = 1; quantidade = 2; chaveOperacao = $saida.chaveOperacao; observacao = 'Outra operacao' }
EsperarErro { PostJson $baseA $mesmaChaveOutroPedido } 409 'Reutilizacao da chave em outro movimento'

Write-Host '7. Historico dos lotes...'
Invoke-RestMethod -Uri $baseA | Format-Table id,tipo,quantidade,saldoAposMovimentacao,dataUtc
Invoke-RestMethod -Uri $baseB | Format-Table id,tipo,quantidade,saldoAposMovimentacao,dataUtc
Write-Host "`nAula 8: TESTES APROVADOS no banco local. Produto ID=$($produto.id), Lote A=$($loteA.id), Lote B=$($loteB.id)."
Write-Host 'Os registros ficam disponiveis para inspecao no MySQL Workbench porta 3307.'
