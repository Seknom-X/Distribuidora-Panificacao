# Aula 13 — Contas a Receber e Pagamentos Parciais

> **Status (26/10/2026): pacote preparado; compilação, migration e testes ainda NÃO executados pelo desenvolvedor.** **Aula 12** também permanece pendente de confirmação dos testes e da migration.

## Objetivo

Evoluir a simulação de parcelamento da Aula 12 para contas a receber persistidas no MySQL, com saldo por parcela e histórico de pagamentos parciais. **Nenhuma baixa de estoque, nova emissão de venda ou pagamento bancário real**.

## Modelo

- **ContaReceber**: cabeçalho de dívida com `VendaId` único, `CondicaoPagamentoId`, data UTC e valor original.
- **ParcelaReceber**: número e vencimento, valor original imutável e valor pago acumulado; índice único para número por conta.
- **Recebimento**: pagamento parcial com `ChaveOperacao` UUID única, valor, forma de pagamento descritiva, observação e data UTC.

Na primeira emissão, a API aplica `ParcelamentoRegras.Simular` (Aula 12) e copia os valores e vencimentos para os títulos definitivos. Reenviar a mesma venda com a mesma condição retorna a conta existente (HTTP 200). Uma condição diferente para uma venda que já possui conta retorna conflito (409). O índice UNIQUE em `ContaReceber.VendaId` protege contra emissão dupla.

## Endpoints exclusivos do Admin JWT

| Método | Rota | Resultado |
|---|---|---|
| POST | `/api/contas-receber/da-venda/{vendaId}` | Gerar conta (body `{"condicaoPagamentoId":1}`) |
| GET | `/api/contas-receber/{id}` | Consultar saldos e parcelas |
| GET | `/api/contas-receber/por-venda/{vendaId}` | Consultar conta da venda |
| GET | `/api/contas-receber/cliente/{clienteId}/resumo` | Somar saldos da carteira do cliente para Admin |
| GET | `/api/contas-receber/parcelas/{parcelaId}/recebimentos` | Histórico de recebimentos |
| POST | `/api/contas-receber/parcelas/{parcelaId}/recebimentos` | Registrar recebimento parcial |

Exemplo de recebimento:

```json
{
  "chaveOperacao": "cdd26d2a-391c-4b74-b7cb-b57d4ea17b44",
  "valor": 5.00,
  "formaPagamento": "Dinheiro",
  "observacao": "Teste academico Aula 13"
}
```

- Pagamento acima do saldo da parcela = **409**.
- Valor zero, negativo ou com mais de duas casas decimais = **400**.
- Repetição idêntica da mesma chave = **200** com mesmo recibo; dados diferentes com a mesma chave = **409**.
- Novo pagamento = **201**.
- Atualização condicional de `ParcelaReceber.ValorPago` e inserção de `Recebimento` na mesma transação MySQL `ReadCommitted`, com índice único para proteger concorrência. Testes simultâneos agressivos ainda pendentes.
- Acesso sem JWT = **401**, Vendedor na API administrativa = **403** (depende de validação).

## Pacote e migração

`Etapa13_Contas_Receber_Pagamentos_DistribuidoraPanificacao.zip` contém 10 arquivos: três Models novos; Controller; DTOs; regras financeiras; DbContext atualizado **baseado na Aula 12**; testes xUnit; script PowerShell e instruções.

**Atenção:** concluir a Aula 12 antes de instalar. Fazer backup de `DistribuidoraDbContext.cs`; nenhuma customização local deve ser sobrescrita sem comparar.

Compilar e testar localmente:

```powershell
dotnet build .\Distribuidora.API\Distribuidora.API.csproj
dotnet test .\Distribuidora.Tests\Distribuidora.Tests.csproj
```

Criar migration:

```powershell
dotnet tool run dotnet-ef migrations add ContasReceberPagamentos --project Distribuidora.API --startup-project Distribuidora.API --context DistribuidoraDbContext --output-dir Data/Migrations
```

Revisar `Up()`: **somente cria** `ContasReceber`, `ParcelasReceber` e `Recebimentos` com FKs/índices. **Não aplicar** se excluir/recriar tabelas existentes. Se estiver correta, usar `dotnet tool run dotnet-ef database update --project Distribuidora.API --startup-project Distribuidora.API --context DistribuidoraDbContext`.

## Testes preparados (ainda não executados)

Com API iniciada em `localhost:5210`:

```powershell
.\Executar_Teste_Contas_Receber.ps1 -VendaId 1
```

Testa conta única por venda, reenvio idempotente, soma das parcelas e JWT 401. Cria a conta, **mas não registra pagamento**.

Somente mediante decisão consciente, executar:

```powershell
.\Executar_Teste_Contas_Receber.ps1 -VendaId 1 -RegistrarPagamentoTeste
```

Solicita confirmação `SIM` e então grava recebimento acadêmico de R$ 5,00 na primeira parcela, confere a redução de saldo e os HTTP 400/409. **Cada execução confirmada usa nova chave e faz outro pagamento de R$ 5,00; não repita desnecessariamente.**

O teste utiliza condições ativas pré-existentes. Se a Aula 12 não foi aplicada ou não gerou condições, o script interromperá.

## Limitações e próximas entregas

- Fuso comercial para vencimentos, dias úteis, estornos, multa, juros, descontos e integração com Pix/boleto reais **não implementados**.
- As contas de cliente são consultáveis apenas por Admin nesta etapa; disponibilizar consulta restrita ao Vendedor exige rota baseada no JWT em aula posterior.
- Confirmação em Workbench e log de testes devem ser fornecidos antes de marcar a aula como concluída.
- Segurança e testes de integração continuam pendentes para o MVP. Utilizar apenas localhost.


## Retorno de validação do desenvolvedor — 26/10/2026

O desenvolvedor informou `deu certinho` após o roteiro da Aula 13. Isso sustenta que conseguiu avançar no fluxo local, mas **não foi fornecido o log** para identificar precisamente se foram realizados apenas os testes de geração/consulta da conta ou também os pagamentos parciais com `-RegistrarPagamentoTeste`. A documentação não deve declarar o pagamento opcional validado sem essa evidência. Próxima etapa: [Aula 14.1 — Histórico e conferência financeira](14-1-HISTORICO-AUDITORIA.md).
