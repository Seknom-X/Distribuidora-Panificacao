# Aula 14.2 — Trilha persistente de operações HTTP

> **Estado em 26/10/2026:** pacote da aula preparado; sem compilação, migration ou execução de testes no Windows do desenvolvedor. A Aula 14.1 foi confirmada como funcionando pelo desenvolvedor.

## Objetivo

Persistir em `RegistrosAuditoria` os metadados de requisições de modificação autenticadas que retornam sucesso HTTP 2xx, permitindo que apenas o Admin consulte o histórico.

**Model:** `RegistroAuditoria` guarda `Id`, `DataUtc`, `UsuarioId`, `Perfil`, `Metodo`, `Caminho`, `StatusHttp`, `CorrelacaoId`. Não guardar corpo, senha, JWT, query string ou segredo. Por segurança, as requisições de login não entram nesta trilha.

**Middleware:** `AuditoriaOperacoesMiddleware` executado **após** `UseAuthorization` e **antes** de `MapControllers`. Registra somente POST/PUT/PATCH/DELETE em `/api/`, autenticados e com resposta 2xx; não registra consultas GET nem erros 400/401/403/409.

**Limitação decisiva:** gravação **posterior** da auditoria em transação MySQL separada. Se o registro falhar, a operação de negócio não é desfeita; middleware usa logger de aviso. Repetições idempotentes HTTP 200 também geram registros. Portanto a trilha **não é ACID com as operações, imutável ou prova de todas as mudanças efetivas**. Para operações críticas em produção, preferir uma estratégia transacional/outbox protegida contra manipulação.

## Arquivos

- `Distribuidora.API/Models/RegistroAuditoria.cs` (novo).
- `Distribuidora.API/Services/AuditoriaOperacoesRegras.cs` (novo).
- `Distribuidora.API/Middleware/AuditoriaOperacoesMiddleware.cs` (novo).
- `Distribuidora.API/Controllers/RegistrosAuditoriaController.cs` (novo).
- `Distribuidora.API/Data/DistribuidoraDbContext.cs` (atualizado, **fazer backup**).
- `Distribuidora.Tests/AuditoriaOperacoesRegrasTests.cs` (novo).
- `Aplicar_Auditoria_Program.ps1` (adiciona Middleware com backup, recusa estruturas inesperadas).
- `Executar_Teste_Auditoria_Operacoes.ps1` (reenvia venda pré-existente sem baixa de estoque, testa auditoria).
- `LEIA-ME_Aula14_2.md` (passo a passo completo).

## Endpoints (JWT Admin)

| Método | Rota | Objetivo |
|---|---|---|
| GET | `/api/admin/registros-auditoria?limite=30` | Consultar últimas 30 operações |
| GET | `/api/admin/registros-auditoria?limite=50&usuarioId=1` | Filtrar por usuário |
| GET | `/api/admin/registros-auditoria/{id}` | Detalhar operação |

`limite` aceita valores entre 1 e 100; filtro `usuarioId` precisa ser positivo.

## Roteiro de instalação

1. Parar a API; fazer cópia do `Program.cs` e `DistribuidoraDbContext.cs`. Extrair o ZIP na raiz da solução sem criar subpasta extra.
2. Rodar `.\Aplicar_Auditoria_Program.ps1` uma vez, ou inserir manualmente `app.UseMiddleware<Distribuidora.API.Middleware.AuditoriaOperacoesMiddleware>();` após `app.UseAuthorization();` e antes de `app.MapControllers();`.
3. Compilar e testar:

```powershell
cd C:\Users\diego\source\repos\Distribuidora-Panificacao
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet build .\Distribuidora.API\Distribuidora.API.csproj
dotnet test .\Distribuidora.Tests\Distribuidora.Tests.csproj
```

4. Criar migration `RegistrosAuditoria` e revisar `Up()`: **apenas criar** a nova tabela e índices, sem exclusões de tabelas ou dados existentes:

```powershell
dotnet tool run dotnet-ef migrations add RegistrosAuditoria --project Distribuidora.API --startup-project Distribuidora.API --context DistribuidoraDbContext --output-dir Data/Migrations
dotnet tool run dotnet-ef database update --project Distribuidora.API --startup-project Distribuidora.API --context DistribuidoraDbContext
```

5. Iniciar API `dotnet run --project .\Distribuidora.API --urls http://localhost:5210`. Em outro terminal, `.\Executar_Teste_Auditoria_Operacoes.ps1 -PedidoId 1`.

O script testa um POST que **reconsulta uma venda já existente** de maneira idempotente. Antes disso, faz GET da venda para impedir criação acidental. A emissão comercial não faz nova baixa. A trilha cria um registro no banco. Apenas ambiente de desenvolvimento local.

## Critérios de aceite

- POST Admin para uma venda já existente retorna HTTP 200 e mesmo ID.
- Novo registro de auditoria com `Metodo=POST`, `Caminho=/api/vendas/do-pedido/1`, `StatusHttp=200` e `UsuarioId` do Admin.
- GET do histórico sem JWT retorna 401.
- `dotnet test` aprova as regras do registro/não registro.
- Nenhum pedido, venda, pagamento ou estoque alterado pelo script (exceto a nova linha da auditoria).

**Ainda não testado pelo desenvolvedor.** Próxima etapa: Painel Web MVC (Aula 15).
