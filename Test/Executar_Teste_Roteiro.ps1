# Aula 6: este script insere DUAS empresas ficticias e altera a ordem de
# TODOS os clientes ativos do primeiro vendedor ativo encontrado.
# Execute apenas num banco LOCAL DE DESENVOLVIMENTO.
$ErrorActionPreference = "Stop"
$api = "http://localhost:5210/api"

Write-Host "1. Consultando vendedores ativos..."
$vendedores = @(Invoke-RestMethod "$api/vendedores?ativo=true")
if ($vendedores.Count -eq 0) {
    throw "Nao ha vendedores ativos. Cadastre um no endpoint POST /api/vendedores."
}
$vendedorId = [int]$vendedores[0].id
Write-Host "Vendedor: $($vendedores[0].nome) (ID $vendedorId)"

function NovoCnpjFicticio {
    return (-join (1..14 | ForEach-Object { Get-Random -Minimum 0 -Maximum 10 }))
}

$nomes = @("Confeitaria Doce Sabor", "Mercado Bom Preco")
foreach ($nome in $nomes) {
    $clienteNovo = @{
        razaoSocial = $nome
        cnpj = NovoCnpjFicticio
        telefone = "11988887777"
        endereco = "Rua Exemplo, 200"
        cidade = "Sao Paulo"
        estado = "SP"
        ordemVisita = 0
        vendedorId = $vendedorId
    }
    $cadastrado = Invoke-RestMethod -Method Post -Uri "$api/clientes" `
        -ContentType "application/json" -Body ($clienteNovo | ConvertTo-Json)
    Write-Host "Criado cliente $($cadastrado.id): $($cadastrado.razaoSocial)"
}

Write-Host "`n2. Roteiro antes da reorganizacao:"
$antes = Invoke-RestMethod "$api/vendedores/$vendedorId/roteiro"
$antes.clientes | Format-Table id, razaoSocial, ordemVisita

$ids = @($antes.clientes | ForEach-Object { [int]$_.id })
# Inverte a ordem de TODOS os clientes ativos apenas para fins de demonstracao.
[array]::Reverse($ids)
$corpo = @{ clienteIds = $ids } | ConvertTo-Json -Depth 5

Write-Host "`n3. Atualizando a ordem de visita..."
$depois = Invoke-RestMethod -Method Put -Uri "$api/vendedores/$vendedorId/roteiro" `
    -ContentType "application/json" -Body $corpo
$depois.clientes | Format-Table id, razaoSocial, ordemVisita

Write-Host "`n4. Confirmando persistencia..."
$conferencia = Invoke-RestMethod "$api/vendedores/$vendedorId/roteiro"
$idsBanco = @($conferencia.clientes | ForEach-Object { [int]$_.id })
if (($ids -join ',') -ne ($idsBanco -join ',')) {
    throw "ERRO: a ordem retornada nao corresponde ao que foi salvo!"
}
Write-Host "OK: roteiro gravado e lido novamente com a nova ordem."

Write-Host "`n5. Testando lista repetida (deve retornar HTTP 400)..."
$duplicados = @{ clienteIds = @($ids[0], $ids[0]) } | ConvertTo-Json
try {
    Invoke-RestMethod -Method Put -Uri "$api/vendedores/$vendedorId/roteiro" `
        -ContentType "application/json" -Body $duplicados -ErrorAction Stop | Out-Null
    throw "ERRO: a API aceitou IDs repetidos."
} catch {
    $status = $null
    if ($_.Exception.Response) {
        $status = [int]$_.Exception.Response.StatusCode
    }
    if ($status -ne 400) { throw }
    Write-Host "OK: requisicao rejeitada com HTTP 400."
}

Write-Host "`nFim. Veja os dados no MySQL Workbench (porta 3307)."
