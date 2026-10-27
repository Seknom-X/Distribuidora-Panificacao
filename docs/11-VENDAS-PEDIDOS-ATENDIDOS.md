# Aula 11 — Vendas dos pedidos atendidos

> **Status em 26/10/2026:** pacote preparado; aguardando compilação, migration `VendasItensVenda` e testes locais. Esta etapa **ainda não foi aprovada no Windows**.

## Objetivo

Depois do corte manual FEFO (Aula 9.2) ou do processamento de uma janela fechada (Aula 9.5), registrar a venda comercial das **quantidades efetivamente atendidas**, sem baixar mercadorias uma segunda vez.

Exemplo validado nas aulas anteriores: pedido ID 1, produto ID 4, 8 unidades solicitadas, 3 atendidas a R$ 12,50 = **R$ 37,50**. Criar venda deve preservar o saldo que já foi reduzido no processamento de pedidos.

## Modelo

- `Venda`: ID, `PedidoId` único, data UTC de emissão e `ValorTotal`.
- `ItemVenda`: ID, `VendaId`, `ItemPedidoId` único, quantidade atendida e preço unitário histórico.
- Mapeamento via `DistribuidoraDbContext`; entidades anteriores preservadas.
- Chaves estrangeiras restritas; índices únicos evitam duplicar venda ou associar o mesmo item a vendas distintas.

## Regras principais

1. Acesso apenas a **Admin autenticado** via JWT.
2. Pedido precisa estar `Atendido` ou `AtendidoParcialmente` (status 3 ou 4).
3. Cada item com quantidade atendida positiva gera um `ItemVenda`. Itens zerados não são vendidos. Preço histórico vem de `ItemPedido.PrecoUnitario`, não do preço atual em `Produtos`.
4. A API confere se as alocações por lote representam exatamente as quantidades atendidas de cada item.
5. Mesmo pedido só gera **uma Venda**. Repetir `POST` retorna HTTP 200 com o mesmo ID; primeira criação retorna HTTP 201. Índice único protege concorrência.
6. **Nenhuma chamada de movimentação de estoque** na geração de venda. Não gravar `MovimentacoesEstoque`, não atualizar `LotesEstoque`.
7. O valor comercial é calculado da soma de `QuantidadeAtendida × PrecoUnitario`.

## Endpoints

| Método | Caminho | Descrição |
|---|---|---|
| POST | `/api/vendas/do-pedido/{pedidoId}` | Emite venda a partir de pedido atendido |
| GET | `/api/vendas/{id}` | Detalhe da venda |
| GET | `/api/vendas/por-pedido/{pedidoId}` | Recupera venda por pedido |
| GET | `/api/vendas?clienteId=N` | Lista até 100 vendas, filtro de cliente opcional |

## Instalação e aceite

1. Parar API, fazer backup de `Distribuidora.API/Data/DistribuidoraDbContext.cs` e extrair o pacote da Aula 11 na raiz da solução.
2. Compilar API, rodar xUnit.
3. Criar migration `VendasItensVenda` pelo `dotnet-ef` da solução e **inspecionar antes de aplicar**. O `Up()` deve apenas criar `Vendas` e `ItensVenda`, com índices/FKs, sem remover tabelas existentes.
4. Aplicar migration ao MySQL local na porta 3307.
5. Iniciar API em `http://localhost:5210` e, em outro PowerShell, rodar `Executar_Teste_Vendas.ps1 -PedidoId 1`.
6. Esperado: primeira chamada 201, ou 200 se venda já existe; segunda chamada 200 com mesmo ID; valor atendido R$ 37,50; consulta pelo pedido funciona; estoque antes/depois idêntico; sem JWT rota retorna 401. Para um pedido ainda pendente, uma venda deve ser recusada com 409.
7. No Workbench, consultar `Vendas` e `ItensVenda` e conferir índices `UNIQUE`.

O script pode **criar uma venda permanente no banco de desenvolvimento**. Não cria saldo financeiro, NF-e, boleto, Pix ou recebimentos; pagamentos/parcelas são a Aula 12. Nenhum teste automatizado de concorrência ou estorno foi apresentado. A API ainda é destinada a localhost e necessita de revisão de segurança antes de produção.

**Próxima etapa:** condições e parcelas de pagamento da venda.


## Correção do script de teste PowerShell — 26/10/2026

A execução do `Executar_Teste_Vendas.ps1` original apresentou `ParserError` na linha 56: interpolação de `$PedidoId:` dentro da string. O PowerShell interpreta os dois-pontos como parte do identificador da variável. A correção é `"Pedido ${PedidoId}: unidades atendidas = $quantidade; estoque consultado."`. Foi entregue um script avulso `Executar_Teste_Vendas_CORRIGIDO.ps1` e um ZIP da Aula 11 atualizado, ambos com a linha corrigida. Nenhum arquivo C#, migration ou banco precisa ser alterado. **O teste de emissão de venda continua aguardando execução no Windows do desenvolvedor.**
