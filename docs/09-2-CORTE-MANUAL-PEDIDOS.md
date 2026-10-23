# Aula 9.2 — Corte manual de pedidos e FEFO

> **Estado:** implementação preparada, aguardando compilação, migration e testes no computador do desenvolvedor. **Ambiente:** .NET 10, EF Core 9, Pomelo 9, MySQL Docker 8.4 na porta 3307.

## Objetivo

Permitir que a distribuidora escolha manualmente a quantidade atendida de cada produto de um pedido **Pendente**, sem ultrapassar o solicitado, e efetive a baixa de lotes disponíveis por FEFO (menor validade primeiro). A baixa do estoque, a quantidade atendida, os movimentos e as alocações devem ser gravados na **mesma transação** MySQL.

Um pedido com 8 unidades solicitadas e apenas 5 válidas disponíveis pode ser cortado para 5; ficará com status **AtendidoParcialmente (4)** e movimentará 5 unidades de lotes válidos. Quando todos os itens forem atendidos, o status será **Atendido (3)**. O status **EmAnalise (2)** é transitório, dentro da transação, para proteger o pedido de processamento concorrente.

## Modelo de rastreabilidade

Nova entidade/tabela `AlocacoesLotePedido`: `Id`, `ItemPedidoId`, `LoteEstoqueId`, `MovimentacaoEstoqueId`, `Quantidade`. Registra qual lote atendeu qual item e associa a saída do estoque à movimentação. Índices únicos em `(ItemPedidoId, LoteEstoqueId)` e `MovimentacaoEstoqueId`. Os dados anteriores de `Pedidos`, `ItensPedido`, `LotesEstoque` e `MovimentacoesEstoque` serão preservados.

## Endpoints previstos

| Método | Endpoint | Retorno |
|---|---|---|
| PUT | `/api/pedidos/{id}/cortar` | HTTP 200 com quantidades, status, valor atendido e alocações; HTTP 400 para payload inválido, HTTP 404 para inexistente e HTTP 409 para estoque insuficiente ou pedido já processado |
| GET | `/api/pedidos/{id}/alocacoes` | Histórico dos lotes e movimentações utilizados |

Exemplo de payload: `{"itens":[{"produtoId":3,"quantidadeAtendida":5}]}`. Todos os itens do pedido devem aparecer exatamente uma vez; itens com atendimento zero são permitidos quando ao menos outro item for atendido.

## Consistência

A operação usa transação com isolamento `ReadCommitted`; primeiro faz um UPDATE condicional no pedido `Pendente -> EmAnalise`; em seguida consulta os itens, valida os valores, ordena os lotes não vencidos por `DataValidade` e ID, e desconta cada lote usando `ExecuteUpdateAsync` com condição de estoque suficiente. Os movimentos de saída, alocações, quantidades atendidas e status final só são confirmados juntos. Se o segundo produto não tiver estoque, as baixas feitas no primeiro também são revertidas.

## Migration

`CortePedidosAlocacoes` deve **apenas criar** `AlocacoesLotePedido`, índices e chaves estrangeiras para itens, lotes e movimentações. Não excluir/recriar tabelas existentes. Revisar o método `Up()` antes do `database update`.

## Testes planejados

1. Insuficiência no segundo produto retorna 409 e reverte todo o corte (status continua Pendente e saldos inalterados).
2. Corte parcial de 5 para pedido de 8: baixa 3 do lote A e 2 do lote B, saldo final 0; valor atendido 62,50 a 12,50 por unidade.
3. Repetição do corte concluído retorna 409 sem nova baixa.
4. Atendimento integral de um segundo produto retorna status 3.
5. Corte acima da quantidade solicitada retorna 400.
6. Consulta de alocações identifica lotes e movimentos.

O pacote de teste `Executar_Teste_Corte_Pedidos.ps1` cria dados fictícios no MySQL de desenvolvimento e mantém os registros para inspeção. **Validação informada pelo desenvolvedor em 23/10/2026:** Build succeeded, migration `CortePedidosAlocacoes` aplicada e script de testes de corte aprovado no MySQL local. A confirmação não substitui testes independentes de concorrência e segurança.

## Limitações e próximos passos

JWT/Admin ainda não está implementado. Endpoints são **apenas de desenvolvimento em localhost** e não devem ficar acessíveis publicamente. O corte efetua baixa real, não pré-reserva; faturamento futuro não deve baixar o estoque novamente. O fuso comercial da validade, estorno/cancelamento, corte automático proporcional, janelas 12h/19h e testes simultâneos independentes ainda serão especificados/implementados.
