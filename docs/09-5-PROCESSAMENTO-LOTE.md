# Aula 9.5 — Processamento atômico de um lote fechado

> **Estado:** pacote preparado em 23/10/2026, aguardando build, migration e testes no Windows. **Não usar em produção.**

## Objetivo

O administrador informa a quantidade atendida de **cada item de cada pedido** vinculado ao lote de processamento fechado na Aula 9.4. A API valida todos os IDs, quantidades e estoques não vencidos antes de concluir uma transação única por janela. Usa lotes físicos por ordem de vencimento (**FEFO**), registra movimentações e alocações, e muda os pedidos para `Atendido` ou `AtendidoParcialmente`.

O lote fechado é uma fotografia de pedidos; o processamento real é outro passo. Um erro em qualquer pedido desfaz **todas** as saídas, registros e alterações de status daquela chamada.

## Código e migration

- Acrescenta `LoteProcessamento.DataProcessamentoUtc` (nullable).
- Atualiza `DistribuidoraDbContext` apenas para mapear essa coluna; não alterar tabelas existentes.
- Cria `ProcessamentoLotesController` com `GET /api/lotes-processamento/{id}/processamento` e `POST /api/lotes-processamento/{id}/processar`.
- Cria DTOs `ProcessamentoLoteDtos` e validação `PlanoCorteLote`, testes xUnit e script PowerShell.
- Migration `ProcessamentoLoteConcluido`: **apenas** coluna opcional `DataProcessamentoUtc` em `LotesProcessamento`.

## Regras de segurança e consistência

1. Somente pedidos pertencentes à fotografia da Aula 9.4.
2. Todos os IDs do lote exatamente uma vez, todos os itens do pedido exatamente uma vez.
3. Quantidade atendida nunca maior que solicitada; de 0 a 1.000.000 por item; **pelo menos 1 unidade atendida por pedido**. O caso de zero atendimento exige futura regra de cancelamento e não é permitido nesta versão.
4. Limite didático de **100 pedidos por janela**. Lotes vazios não podem ser processados.
5. Uso de `ExecuteUpdateAsync` para bloquear a linha do lote e as linhas dos pedidos; decrementos de estoque condicionais impedem saldo negativo.
6. Operações de estoque, status, auditoria e `DataProcessamentoUtc` confirmadas no mesmo `CommitAsync`.
7. Novo POST em lote já processado retorna **409**, sem baixa duplicada. Consultar o resultado pelo GET.

## Exemplo de envio

```json
{
  "pedidos": [
    {
      "pedidoId": 10,
      "itens": [
        { "produtoId": 3, "quantidadeAtendida": 5 }
      ]
    }
  ]
}
```

IDs **ilustrativos**: consultar os pedidos vinculados ao lote via `GET /api/lotes-processamento/{id}` e os produtos via `GET /api/pedidos/{pedidoId}` antes de preparar o plano.

## Testes sugeridos

Após compilar e aplicar a migration, executar `dotnet test` e o script `Executar_Teste_Processamento_Janela.ps1 -LoteId N`: faz GET do lote, GET de processamento e verifica HTTP 400 para corpo vazio, **sem alterar estoque**. Se houver um lote fechado não vazio, com pedidos pendentes e quantidades aprovadas, é possível validar o processamento real usando `-ArquivoPlano .\plano.json -ConfirmarProcessamento`.

**Cuidado:** o segundo comando grava baixas reais no MySQL. Não é executado por padrão. Se o lote estiver vazio, um smoke test bem-sucedido não comprova a baixa FEFO nem o rollback. A etapa ainda requer testes de concorrência e integração completos.

## Pendências

JWT/Admin, fuso de referência comercial para validade, cancelamento quando não for possível atender nenhuma unidade, devoluções, lógica de cortes automáticos e agendamento às 12h/19h. Até lá, disponibilizar endpoints somente em `localhost`.
