# AULA 7 - TESTES EM BANCO LOCAL. Gera um grupo, um produto e dois lotes a cada execucao.
# Pre-requisitos: Docker/MySQL 3307 e API rodando em http://localhost:5210.
$ErrorActionPreference = 'Stop'
$api = 'http://localhost:5210/api'
$tag = [Guid]::NewGuid().ToString('N').Substring(0,8)

function ChecarStatus($bloco, [int]$esperado, [string]$descricao) {
    try {
        & $bloco | Out-Null
        throw "Falha: $descricao deveria resultar em HTTP $esperado, mas a API aceitou."
    } catch {
        $status = $null
        if ($_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($status -ne $esperado) { throw }
        Write-Host "OK: $descricao -> HTTP $status"
    }
}

Write-Host '1. Criando grupo de produtos de panificacao...'
$grupoBody = @{nome="Fermentos $tag";descricao='Grupo ficticio de teste'} | ConvertTo-Json
$grupo = Invoke-RestMethod -Method Post -Uri "$api/grupos-produto" -ContentType 'application/json' -Body $grupoBody
Write-Host "Grupo criado: $($grupo.nome), ID=$($grupo.id)"

Write-Host '`n2. Cadastrando produto vinculado ao grupo...'
$produtoBody = @{
    nome="Fermento Biologico $tag"
    unidadeMedida='UN'
    precoVenda=12.50
    grupoProdutoId=[int]$grupo.id
} | ConvertTo-Json
$produto = Invoke-RestMethod -Method Post -Uri "$api/produtos" -ContentType 'application/json' -Body $produtoBody
Write-Host "Produto criado: $($produto.nome), ID=$($produto.id)"

Write-Host '`n3. Criando dois lotes com datas de validade diferentes...'
$codigo1="LOTE-$tag-A"
$codigo2="LOTE-$tag-B"
$corpo1 = @{codigoLote=$codigo1;dataValidade=(Get-Date).ToUniversalTime().AddDays(20).ToString('yyyy-MM-dd');quantidadeDisponivel=12} | ConvertTo-Json
$corpo2 = @{codigoLote=$codigo2;dataValidade=(Get-Date).ToUniversalTime().AddDays(60).ToString('yyyy-MM-dd');quantidadeDisponivel=8} | ConvertTo-Json
$lote1 = Invoke-RestMethod -Method Post -Uri "$api/produtos/$($produto.id)/lotes" -ContentType 'application/json' -Body $corpo1
$lote2 = Invoke-RestMethod -Method Post -Uri "$api/produtos/$($produto.id)/lotes" -ContentType 'application/json' -Body $corpo2
Write-Host "Lotes criados: $($lote1.codigoLote) e $($lote2.codigoLote)"

Write-Host '`n4. Consultando lote por validade (mais proximo primeiro)...'
$lotes = Invoke-RestMethod -Uri "$api/produtos/$($produto.id)/lotes"
$lotes | Format-Table id,codigoLote,dataValidade,quantidadeDisponivel,vencido
if ($lotes.Count -ne 2 -or $lotes[0].codigoLote -ne $codigo1) {
    throw 'Falha: esperavamos dois lotes ordenados por validade.'
}

Write-Host '`n5. Consultando saldo fisico e saldo nao vencido...'
$estoque = Invoke-RestMethod "$api/produtos/$($produto.id)/estoque"
$estoque | Format-List
if ([long]$estoque.estoqueFisico -ne 20 -or [long]$estoque.estoqueValido -ne 20 -or [long]$estoque.estoqueVencido -ne 0) {
    throw 'Falha: estoque diferente do esperado (20 validos, 0 vencidos).'
}
Write-Host 'OK: total fisico = 20, valido = 20, vencido = 0.'

Write-Host '`n6. Verificando protecoes...'
ChecarStatus { Invoke-RestMethod -Method Post -Uri "$api/produtos/$($produto.id)/lotes" -ContentType 'application/json' -Body $corpo1 -ErrorAction Stop } 409 'Lote duplicado'
$invalido = @{codigoLote="LOTE-$tag-INV";dataValidade=(Get-Date).ToUniversalTime().AddDays(10).ToString('yyyy-MM-dd');quantidadeDisponivel=0} | ConvertTo-Json
ChecarStatus { Invoke-RestMethod -Method Post -Uri "$api/produtos/$($produto.id)/lotes" -ContentType 'application/json' -Body $invalido -ErrorAction Stop } 400 'Quantidade zero'
ChecarStatus { Invoke-RestMethod -Method Patch -Uri "$api/grupos-produto/$($grupo.id)/desativar" -ErrorAction Stop } 409 'Grupo com produto ativo'

Write-Host "`nAula 7 validada no ambiente LOCAL. Grupo ID=$($grupo.id); Produto ID=$($produto.id)."
Write-Host 'Os registros de teste foram mantidos para consulta no MySQL Workbench (porta 3307).'
