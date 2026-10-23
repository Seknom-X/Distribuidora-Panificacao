# Aula 9.4 — Fechamento manual das janelas de corte

> **Status:** pacote preparado em 23/10/2026; aguardando compilação, migration e testes no Windows. **Aula 9.3:** usuário confirmou que resolveu o erro de build, mas ainda falta log dos testes xUnit e PowerShell.

## Objetivo

Registrar uma fotografia dos IDs de **pedidos pendentes** de uma janela comercial que já terminou, respeitando os cortes das 12h e 19h no fuso `America/Sao_Paulo`. **Fechar não corta pedidos, não muda status e não movimenta estoque**. A próxima etapa processará os pedidos vinculados a um lote fechado.

## Entidades e banco

- `LotesProcessamento`: Id, DataCorte, HoraCorte, InicioUtc, FimUtc, DataFechamentoUtc; índice único em (DataCorte, HoraCorte).
- `LotesProcessamentoPedidos`: Id, LoteProcessamentoId, PedidoId; índice único em PedidoId e relacionamentos restritos.
- Tabelas `Pedidos`, `ItensPedido`, `LotesEstoque`, `AlocacoesLotePedido` permanecem intactas.

A migration `FechamentoJanelas` deve **somente criar essas duas tabelas e seus índices/chaves estrangeiras**. Revisar o `Up()` antes de aplicar.

## Endpoints previstos

| Método | Caminho | Efeito |
|---|---|---|
| POST | `/api/lotes-processamento/fechar` | Registra a fotografia; novo = HTTP 201, repetido = HTTP 200 |
| GET | `/api/lotes-processamento/{id}` | Busca lote e pedidos associados |
| GET | `/api/lotes-processamento?data=2026-10-22&hora=19` | Consulta por janela |

Exemplo de POST: `{"dataCorte":"2026-10-22","horaCorte":19}`.

A API não fecha uma janela ainda aberta (HTTP 409) e rejeita horas diferentes de 12 e 19 (HTTP 400). O fechamento usa transação `ReadCommitted` e leitura dos pedidos `FOR UPDATE`, com limite explícito de 1000 pedidos por janela. Há índice único para impedir o fechamento duplicado; em disputa concorrente, pode ser necessário repetir a operação.

**Importante:** o vínculo dos IDs é imutável; `statusAtual` é uma consulta viva aos pedidos e poderá mudar após o processamento.

## Como validar

1. Parar a API; copiar os arquivos do pacote da Aula 9.4, preservando backup de `DistribuidoraDbContext.cs`.
2. `dotnet build .\Distribuidora.API\Distribuidora.API.csproj`.
3. `dotnet test .\Distribuidora.Tests\Distribuidora.Tests.csproj`.
4. Criar migration: `dotnet tool run dotnet-ef migrations add FechamentoJanelas --project Distribuidora.API --startup-project Distribuidora.API --context DistribuidoraDbContext --output-dir Data/Migrations`.
5. Inspecionar e aplicar: `dotnet tool run dotnet-ef database update --project Distribuidora.API --startup-project Distribuidora.API --context DistribuidoraDbContext`.
6. Iniciar API: `dotnet run --project .\Distribuidora.API --urls http://localhost:5210`.
7. Em outro terminal: `.\Executar_Teste_Fechamento_Janelas.ps1`.

O script seleciona janela já encerrada, fecha, repete para verificar que o ID não muda, consulta por ID e data/hora e rejeita horário inválido e janela futura. **Cria registro permanente de lote no banco local**; não modifica pedido nem estoque.

Se o teste escolher uma janela com **zero pedidos**, apenas o fechamento vazio terá sido validado. A associação de pedidos reais ainda exigirá outra janela com pedidos pendentes, ou um teste de integração controlado.

## Limitações

JWT/Admin ainda não foi implementado. Usar somente em localhost. O processamento automático, o FEFO por lote fechado, a retomada após falhas e os testes de concorrência agressivos continuam pendentes. A prévia da Aula 9.3 continuará exibindo pedidos pendentes, inclusive os já associados a um lote fechado, até a futura evolução do fluxo. O fechamento não é um lote físico de mercadoria.

**Aula 9.4 ainda não testada no ambiente do usuário.**
