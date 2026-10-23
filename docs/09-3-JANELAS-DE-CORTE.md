# Aula 9.3 — Prévia das janelas de corte às 12h e 19h

> **Estado:** pacote de implementação preparado em 23/10/2026; aguardando build, testes xUnit e validação HTTP no Windows. Nenhuma migration é necessária.

## Objetivo

Agrupar visualmente pedidos ainda **Pendentes** por janela comercial da distribuidora para apoiar a conferência operacional, sem executar o corte, sem alterar o status do pedido e sem baixar estoque.

**Definição inicial das janelas, no fuso `America/Sao_Paulo`:**
- **12h do dia D:** pedidos com `DataCriacao` após 19h de D−1 e até 12h de D;
- **19h do dia D:** pedidos com `DataCriacao` após 12h de D e até 19h de D.

A fronteira é **(início, fim]**, evitando que um pedido no horário exato pertença a duas janelas. Pedidos recebidos após as 19h irão para a janela do meio-dia seguinte.

## Arquivos novos

- `Distribuidora.API/Services/JanelaCorteService.cs`: horários e conversão entre hora comercial e UTC.
- `Distribuidora.API/DTOs/JanelaCorteDtos.cs`: resumo por produto e por pedido.
- `Distribuidora.API/Controllers/JanelasCorteController.cs`: consulta da próxima janela e consulta por data.
- `Distribuidora.Tests/JanelaCorteServiceTests.cs`: testes de fronteira determinísticos.
- `Executar_Teste_Janelas_Corte.ps1`: testes HTTP não destrutivos.

**Sem alteração do `DistribuidoraDbContext`, Models ou tabelas.** Nenhuma migration.

## Endpoints

| Método | Endpoint | Comportamento |
|---|---|---|
| GET | `/api/janelas-corte/atual` | Prévia da próxima janela das 12h ou 19h |
| GET | `/api/janelas-corte?data=2026-10-23&hora=12` | Prévia do corte de meio-dia da data informada |
| GET | `/api/janelas-corte?data=2026-10-23&hora=19` | Prévia do corte noturno da data informada |
| GET | `/api/janelas-corte?data=2026-10-23&hora=15` | HTTP 400: horário não permitido |

Resumo inclui pedidos pendentes, unidades solicitadas, valor solicitado e por produto quantidade solicitada, estoque atual com lotes válidos na data do corte e **falta estimada**.

A consulta de estoque é **estimativa baseada no saldo atual**, não é fotografia histórica, alocação, reserva ou compromisso de atendimento. Lotes vencidos ou produtos inativos não entram na estimativa. Os pedidos já cortados não aparecem por não estarem pendentes.

## Executar e validar

```powershell
cd C:\Users\diego\source\repos\Distribuidora-Panificacao
dotnet build .\Distribuidora.API\Distribuidora.API.csproj
dotnet test .\Distribuidora.Tests\Distribuidora.Tests.csproj
dotnet run --project .\Distribuidora.API --urls http://localhost:5210
```

Em outro terminal:

```powershell
.\Executar_Teste_Janelas_Corte.ps1
```

O script verifica consulta atual, consulta explícita, contagens, somas, horário inválido (400) e limites das janelas. **Não insere nem modifica dados**; retornos com zero pedidos também podem estar corretos. A API deve estar acessível no `localhost`.

## Limitações conhecidas

- Ainda **não** há fechamento/agendamento automático às 12h ou 19h, processamento coletivo, lote persistido ou tratamento definido de pedidos antigos pendentes.
- Os números de estoque refletem o saldo atual; para consulta histórica não é saldo histórico da data solicitada.
- Os endpoints ainda não possuem JWT: somente ambiente local. A futura etapa de fechamento exigirá autenticação Admin, trilha de auditoria, idempotência e concorrência.
- O fuso comercial foi adotado provisoriamente como São Paulo. Confirmar com a distribuidora/professor antes da implantação.

**Próxima etapa proposta:** fechar lotes de processamento de maneira segura e atribuir pedidos à janela sem remover as regras de corte FEFO da Aula 9.2.
