# Aula 12 — Condições de pagamento e simulação de parcelas

> **Status em 26/10/2026:** pacote C# e PowerShell preparado, mas **ainda não compilado ou testado no Windows do desenvolvedor**. Aula 11: testes principais aprovados (venda 1, pedido 1, 3 unidades, R$ 37,50, nenhuma nova baixa).

## Escopo

Cadastrar condições reutilizáveis de pagamento compostas por parcelas com dias após a venda e percentual. Permitir simulação sobre uma **venda já emitida**, sem gerar contas a receber, sem modificar pedido e sem baixar estoque.

Novas entidades/tabelas: `CondicaoPagamento`/`CondicoesPagamento` e `ParcelaCondicao`/`ParcelasCondicao`. EF Core com índices UNIQUE em nome da condição, número da parcela e dias após a venda por condição. O `DbContext` atualizado preserva as configurações das aulas anteriores, a partir da versão da Aula 11.

## Contratos API (Admin JWT)

| Método | Rota | Ação |
|---|---|---|
| POST | `/api/condicoes-pagamento` | Cadastrar condição com percentuais |
| GET | `/api/condicoes-pagamento` | Listar até 100, com filtro `ativa` |
| GET | `/api/condicoes-pagamento/{id}` | Detalhe |
| PATCH | `/api/condicoes-pagamento/{id}/desativar` | Inativar preservando histórico |
| GET | `/api/vendas/{vendaId}/parcelamento/simular?condicaoId={id}` | Simulação sem persistência financeira |

Exemplo de `POST`:

```json
{
  "nome": "7 e 14 dias 50/50",
  "parcelas": [
    {"diasAposVenda": 7, "percentual": 50.00},
    {"diasAposVenda": 14, "percentual": 50.00}
  ]
}
```

## Regras

1. Uma condição tem de 1 a 12 parcelas; dias inteiros entre 0 e 365, estritamente crescentes, sem repetições.
2. Percentual positivo até duas casas decimais; soma exata de **100%**.
3. Parcelar `ValorTotal` da venda, baseando vencimentos na **data UTC de emissão da venda** (fuso comercial será especificado antes de produção).
4. Cada parcela intermediária arredondada a 2 casas com `MidpointRounding.AwayFromZero`; última parcela recebe diferença para fechar o total. Parcelamento que gere parcela de R$ 0,00 é rejeitado.
5. Apenas Admin autenticado; sem token = 401. Condição inativa não pode ser usada em nova simulação.
6. Nenhuma operação financeira é efetivada nesta aula: débito, formas de pagamento e recebíveis ficam para a Aula 13.

## Instalação e validação

1. Parar a API, fazer backup de `Distribuidora.API/Data/DistribuidoraDbContext.cs`; extrair pacote na raiz da solução.
2. `dotnet build .\Distribuidora.API\Distribuidora.API.csproj`.
3. `dotnet test .\Distribuidora.Tests\Distribuidora.Tests.csproj`.
4. Criar migration `CondicoesPagamentoParcelas` por `dotnet tool run dotnet-ef migrations add ...`.
5. Inspecionar `Up()`: **apenas criar** `CondicoesPagamento` e `ParcelasCondicao`; **não alterar tabelas existentes**.
6. Aplicar com `dotnet tool run dotnet-ef database update ...` no MySQL local, porta 3307.
7. Iniciar API em `localhost:5210` e executar `Executar_Teste_Parcelamento.ps1 -VendaId 1` com senha Admin.

## Critérios de aceite

- Venda 1, R$ 37,50, condição 7/14 dias, 50/50 => parcelas de **R$ 18,75** em duas datas.
- Uma condição 7/14/21, 33,33% + 33,33% + 33,34% => soma das parcelas igual a **R$ 37,50**.
- Percentual de soma 90% => HTTP 400.
- GET sem JWT => HTTP 401.
- Script cria duas condições fictícias no MySQL de desenvolvimento; simulações não criam contas a receber ou alteram estoque.

**Não declarar a Aula 12 concluída até que o desenvolvedor execute build, migration e testes.** Testes de concorrência/rollback de módulos anteriores continuam como pendências de qualidade.
