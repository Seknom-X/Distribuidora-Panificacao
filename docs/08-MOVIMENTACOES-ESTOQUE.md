# Aula 8 — Movimentações de estoque por lote

> **Status:** implementação preparada; aguardando compilação, migration e testes no Windows.  
> **Ambiente:** ASP.NET Core .NET 10, EF Core 9, Pomelo 9, MySQL 8.4 no Docker (porta 3307).

## 1. Objetivo

Registrar **entradas complementares e saídas manuais por lote**, preservando o histórico de movimentações de um produto perecível.

A criação de um lote na Aula 7 representa o **saldo inicial** e não gera retroativamente uma movimentação. A partir da Aula 8, as alterações de saldo serão registradas em `MovimentacoesEstoque`.

## 2. Modelo

| Entidade/campo | Finalidade |
|---|---|
| MovimentacaoEstoque.Id | Identificador da movimentação |
| LoteEstoqueId | Lote afetado |
| Tipo | Entrada (1) ou Saída (2) |
| Quantidade | Inteiro positivo |
| SaldoAposMovimentacao | Saldo resultante da operação |
| ChaveOperacao | UUID único para prevenir aplicações duplicadas de uma operação |
| Observacao | Justificativa descritiva |
| DataUtc | Instante da operação |

O relacionamento é **LoteEstoque 1:N MovimentacaoEstoque**, sem exclusão em cascata.

## 3. Endpoints planejados

| Método | Rota |
|---|---|
| POST | `/api/produtos/{produtoId}/lotes/{loteId}/movimentacoes` |
| GET | `/api/produtos/{produtoId}/lotes/{loteId}/movimentacoes` |
| GET | `/api/produtos/{produtoId}/lotes/{loteId}/movimentacoes/{id}` |

Um POST recebe o tipo, quantidade, chave UUID e observação. Uma operação nova retorna **201**, e um reenvio idêntico depois da conclusão retorna **200**, sem movimentar o saldo novamente.

## 4. Garantias previstas

- **Estoque não negativo:** saída utiliza `ExecuteUpdateAsync` condicional, dentro de transação.
- **Limite numérico:** entrada não ultrapassa `int.MaxValue`.
- **Auditoria:** gravação do movimento e atualização do saldo estão na mesma transação MySQL.
- **Idempotência:** chave UUID única, inclusive por índice no banco. Operação diferente com chave repetida é recusada.
- **Validade:** lote vencido ou produto inativo não aceita movimentação operacional normal.
- **Autorização:** endpoints ainda **sem JWT**; restritos a testes em `localhost`, jamais à internet.

A comparação de vencimento usa data UTC provisoriamente. Definir o fuso comercial antes de processar pedidos reais.

## 5. Critérios de aceite — próximos testes

Criar produto novo com lote A (12) e lote B (8); saída de 3 no A deve resultar em 9; reenvio com a mesma chave mantém 9; entrada de 5 no B resulta em 13. Estoque físico total final: **22** unidades. Saída maior que o saldo retorna **409**; quantidade zero retorna **400**; reuso da chave com parâmetros diferentes retorna **409**.

O pacote da Aula 8 inclui `Executar_Teste_Movimentacoes.ps1` para verificar esses resultados. **A execução real ainda precisa ser confirmada.**

## 6. Migração

Gerar `MovimentacoesEstoque`, criando apenas a nova tabela, seus índices e chave estrangeira para `LotesEstoque`. Não reconstruir nem excluir tabelas de aulas anteriores. Revisar a migration antes de aplicar `database update`.

## 7. Etapas futuras

Testes xUnit de concorrência e rollback, FEFO em pedidos, corte proporcional/manual e autorização por papéis. Este exercício não implementa baixa automática de pedidos.
