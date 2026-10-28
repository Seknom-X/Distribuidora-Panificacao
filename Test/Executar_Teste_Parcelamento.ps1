param(
    [ValidateRange(1,2147483647)][int]$VendaId = 1,
    [string]$Api = 'http://localhost:5210'
)
$ErrorActionPreference = 'Stop'

function Exigir([bool]$Condicao, [string]$Mensagem) {
    if (-not $Condicao) { throw "FALHA: $Mensagem" }
}
function Chamar([string]$Uri, [string]$Metodo, $Cabecalhos, $Corpo = $null) {
    $parametros = @{ Uri = $Uri; Method = $Metodo; Headers = $Cabecalhos; UseBasicParsing = $true; ErrorAction = 'Stop' }
    if ($null -ne $Corpo) {
        $parametros['ContentType'] = 'application/json; charset=utf-8'
        $parametros['Body'] = [System.Text.Encoding]::UTF8.GetBytes($Corpo)
    }
    $resposta = Invoke-WebRequest @parametros
    $conteudo = if ([string]::IsNullOrWhiteSpace($resposta.Content)) { $null } else { $resposta.Content | ConvertFrom-Json }
    return [pscustomobject]@{ Status = [int]$resposta.StatusCode; Dados = $conteudo }
}
function StatusErro([string]$Uri, [string]$Metodo, $Cabecalhos, $Corpo = $null) {
    try {
        $null = Chamar -Uri $Uri -Metodo $Metodo -Cabecalhos $Cabecalhos -Corpo $Corpo
        return 200
    }
    catch [System.Net.WebException] {
        if ($null -ne $_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        throw
    }
}

$Api = $Api.TrimEnd('/')
if ($Api -ne 'http://localhost:5210' -and $Api -ne 'http://127.0.0.1:5210') {
    throw 'O script de desenvolvimento aceita somente API local na porta 5210.'
}
Write-Host 'Aula 12 - Condicoes de pagamento e simulacao de parcelas' -ForegroundColor Cyan
Write-Host 'AVISO: cria duas condicoes ficticias no MySQL local. Nao cria contas a receber nem altera estoque.' -ForegroundColor Yellow
$email = (Read-Host 'Email do Admin').Trim()
$senha = Read-Host 'Senha do Admin (oculta)' -AsSecureString
$credencial = [PSCredential]::new('login', $senha)
$loginCorpo = @{ email=$email; senha=$credencial.GetNetworkCredential().Password } | ConvertTo-Json -Compress
$credencial = $null; $senha = $null
$login = Chamar "$Api/api/auth/login" 'POST' @{} $loginCorpo
$loginCorpo = $null
Exigir ($login.Status -eq 200 -and $login.Dados.usuario.perfil -eq 'Admin') 'Login deve retornar perfil Admin.'
$headers = @{ Authorization = "Bearer $($login.Dados.token)" }

Write-Host "1. Consultando venda $VendaId..." -ForegroundColor Cyan
$venda = (Chamar "$Api/api/vendas/$VendaId" 'GET' $headers).Dados
Exigir ([int]$venda.id -eq $VendaId) 'Venda nao encontrada.'
$valorVenda = [decimal]$venda.valorTotal
Exigir ($valorVenda -gt 0) 'Venda nao possui valor positivo.'
Write-Host "OK: venda encontrada, valor = $valorVenda." -ForegroundColor Green

$tag = [Guid]::NewGuid().ToString('N').Substring(0, 10)
$condicaoNome = "Teste 7-14 dias $tag"
$condicaoCorpo = @{
    nome = $condicaoNome
    parcelas = @(
        @{ diasAposVenda = 7; percentual = 50.00 },
        @{ diasAposVenda = 14; percentual = 50.00 }
    )
} | ConvertTo-Json -Depth 8 -Compress

Write-Host '2. Cadastrando condicao 7/14 dias, 50%/50%...' -ForegroundColor Cyan
$criada = Chamar "$Api/api/condicoes-pagamento" 'POST' $headers $condicaoCorpo
Exigir ($criada.Status -eq 201) 'Cadastro da condicao deveria retornar HTTP 201.'
$idCondicao = [int]$criada.Dados.id
Exigir ($idCondicao -gt 0) 'Condicao cadastrada sem ID.'
$detalhe = (Chamar "$Api/api/condicoes-pagamento/$idCondicao" 'GET' $headers).Dados
Exigir (@($detalhe.parcelas | Where-Object { $null -ne $_ }).Count -eq 2) 'Detalhes nao tem duas parcelas.'

Write-Host '3. Simulando parcelamento da venda...' -ForegroundColor Cyan
$sim = (Chamar "$Api/api/vendas/$VendaId/parcelamento/simular?condicaoId=$idCondicao" 'GET' $headers).Dados
$parcelas = @($sim.parcelas | Where-Object { $null -ne $_ })
Exigir ($parcelas.Count -eq 2) 'Simulacao nao retornou duas parcelas.'
Exigir ([decimal]$sim.totalParcelado -eq $valorVenda) 'Total parcelado nao fecha com o valor da venda.'
$soma = [decimal](($parcelas | Measure-Object -Property valor -Sum).Sum)
Exigir ($soma -eq $valorVenda) 'Soma dos valores das parcelas nao fecha.'
$esperadoPrimeiro = [DateTime]::Parse([string]$venda.dataEmissaoUtc).Date.AddDays(7).ToString('yyyy-MM-dd')
$esperadoSegundo = [DateTime]::Parse([string]$venda.dataEmissaoUtc).Date.AddDays(14).ToString('yyyy-MM-dd')
Exigir ([DateTime]::Parse([string]$parcelas[0].vencimento).ToString('yyyy-MM-dd') -eq $esperadoPrimeiro) 'Vencimento da primeira parcela incorreto.'
Exigir ([DateTime]::Parse([string]$parcelas[1].vencimento).ToString('yyyy-MM-dd') -eq $esperadoSegundo) 'Vencimento da segunda parcela incorreto.'
$parcelas | Format-Table numero, diasAposVenda, percentual, vencimento, valor -AutoSize
Write-Host "OK: soma de parcelas = $soma, igual ao valor da venda." -ForegroundColor Green

Write-Host '4. Testando tres parcelas com percentuais 33.33/33.33/33.34...' -ForegroundColor Cyan
$triplas = @{
    nome = "Teste 7-14-21 dias $tag"
    parcelas = @(
        @{ diasAposVenda = 7; percentual = 33.33 },
        @{ diasAposVenda = 14; percentual = 33.33 },
        @{ diasAposVenda = 21; percentual = 33.34 }
    )
} | ConvertTo-Json -Depth 8 -Compress
$resTres = Chamar "$Api/api/condicoes-pagamento" 'POST' $headers $triplas
Exigir ($resTres.Status -eq 201) 'Condicao de tres parcelas deveria retornar HTTP 201.'
$simTres = (Chamar "$Api/api/vendas/$VendaId/parcelamento/simular?condicaoId=$($resTres.Dados.id)" 'GET' $headers).Dados
Exigir (@($simTres.parcelas | Where-Object { $null -ne $_ }).Count -eq 3) 'Simulacao nao gerou tres parcelas.'
Exigir ([decimal]$simTres.totalParcelado -eq $valorVenda) 'Arredondamento nao fechou o total da venda.'
Write-Host "OK: tres parcelas; soma = $($simTres.totalParcelado)." -ForegroundColor Green

Write-Host '5. Validando rejeicao de percentuais invalidos...' -ForegroundColor Cyan
$invalida = @{
    nome = "Teste percentual errado $tag"
    parcelas = @(
        @{ diasAposVenda = 7; percentual = 40 },
        @{ diasAposVenda = 14; percentual = 50 }
    )
} | ConvertTo-Json -Depth 8 -Compress
$httpInvalido = StatusErro "$Api/api/condicoes-pagamento" 'POST' $headers $invalida
Exigir ($httpInvalido -eq 400) 'Soma 90% deve retornar HTTP 400.'
Write-Host 'OK: soma percentual de 90% => HTTP 400.' -ForegroundColor Green

Write-Host '6. Confirmando autenticacao...' -ForegroundColor Cyan
$httpSemToken = StatusErro "$Api/api/condicoes-pagamento" 'GET' @{}
Exigir ($httpSemToken -eq 401) 'Consulta sem JWT deveria retornar HTTP 401.'
Write-Host 'OK: sem JWT => HTTP 401.' -ForegroundColor Green

Write-Host 'Aula 12: TESTES PRINCIPAIS APROVADOS. Nenhuma conta a receber foi gerada.' -ForegroundColor Green
Write-Host "Condicoes criadas: $idCondicao, $($resTres.Dados.id). Os registros permanecem no MySQL local." -ForegroundColor Yellow
