# Aula 10.1 — JWT e separação de acessos Admin/Vendedor

> **Estado:** pacote preparado em 26/10/2026; **ainda não compilado nem testado no Windows do desenvolvedor**. Não usar em produção antes da validação.

## Motivação

As APIs de vendedores, clientes, pedidos, cortes e estoque das aulas 1–9.5 ainda foram implementadas para desenvolvimento local sem identificação de usuário. Nesta etapa, o sistema passa a exigir um token **JWT assinado** e restringe por padrão os Controllers antigos ao perfil **Admin**. O vendedor usa apenas rotas de leitura `/api/me/...` que extraem seu `vendedorId` de um token validado no servidor; nenhuma dessas rotas aceita `vendedorId` arbitrário.

## Arquitetura implementada no pacote

- `Models/UsuarioSistema.cs`: Id, Nome, Email, SenhaHash, Perfil (Admin/Vendedor), Ativo, VendedorId opcional; relacionamento com Vendedor (FK restrita).
- `Data/DistribuidoraDbContext.cs`: `DbSet<UsuarioSistema> Usuarios`, restrições e índices únicos em Email e VendedorId; mantido o conteúdo da Aula 9.5.
- `Security/SegurancaDistribuidora.cs`: configuração JwtBearer com validação de emissor, audiência, assinatura, validade e chave Base64 >=32 bytes, `DefaultPolicy` e `FallbackPolicy` Admin; verificação de usuário ativo e vínculo com vendedor a cada requisição; bootstrap de Admin somente pelo terminal em Development.
- `Controllers/AutenticacaoController.cs`: `POST /api/auth/login` público; `GET /api/auth/me` autenticado; `GET /api/auth/usuarios` e `POST /api/auth/usuarios/vendedor` só Admin.
- `Controllers/MinhaCarteiraController.cs`: `GET /api/me/clientes`, `GET /api/me/clientes/{id}`, `GET /api/me/pedidos`, `GET /api/me/produtos` — só Vendedor, filtrados pelo token onde aplicável.
- `DTOs/AutenticacaoDtos.cs`.
- `Aplicar_JWT_Program.ps1` com backup em `%TEMP%`, recusa mutação se Program.cs já possui configuração e insere as chamadas necessárias.
- `Executar_Teste_JWT_Admin.ps1`, `Cadastrar_Usuario_Vendedor.ps1` e `Executar_Teste_JWT_Vendedor.ps1`, solicitando senhas sem ecoar texto no terminal.

## Configuração local (sem salvar segredos no Git)

Dependência NuGet `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.13` para projeto `net10.0`. Preservar EF Core/Pomelo **9.x**.

1. Parar API e salvar backup do DbContext e Program.cs. Extrair ZIP da Aula 10.1 na raiz da solução.
2. Instalar dependência:
```powershell
dotnet add .\Distribuidora.API\Distribuidora.API.csproj package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.13
.\Aplicar_JWT_Program.ps1
```
3. Criar chave aleatória Base64 de 64 bytes e configurar `Jwt:Chave` (segredo), `Jwt:Emissor=Distribuidora.API`, `Jwt:Audiencia=Distribuidora.Clientes` via `dotnet user-secrets set ... --project .\Distribuidora.API`. Usar o User Secrets existente, que também contém `ConnectionStrings:DistribuidoraDb`. Não substituir segredos de outro projeto.
4. Compilar, criar migration `UsuariosAutenticacao` e inspecionar. **Deve apenas criar a tabela `Usuarios` com índices e FK**, sem remover tabelas existentes.
5. Aplicar a migration. Criar primeiro Admin pelo terminal, **sem endpoint público**: `dotnet run --project .\Distribuidora.API -- --criar-admin` (somente Development, banco inicialmente sem usuários).
6. Iniciar API em `localhost:5210`; executar `Executar_Teste_JWT_Admin.ps1` e `Cadastrar_Usuario_Vendedor.ps1`, seguido de `Executar_Teste_JWT_Vendedor.ps1`.

Mais instruções na raiz do pacote: `LEIA-ME_Aula10.md`.

## Evidências esperadas

| Cenário | HTTP |
|---|---|
| GET /api/produtos sem JWT | 401 |
| GET /api/produtos com JWT falso | 401 |
| POST /api/auth/login válido | 200, token Bearer |
| GET /api/produtos com Admin | 200 |
| GET /api/me/clientes com Admin | 403 |
| GET /api/me/clientes com Vendedor | 200, somente carteira vinculada |
| GET /api/produtos com Vendedor | 403 |
| GET /api/auth/usuarios com Vendedor | 403 |
| GET /api/me/clientes/{id} para cliente de outro vendedor | 404 |

## Compatibilidade e limitações

Esta etapa **altera intencionalmente** os retornos dos scripts das aulas anteriores sem token: eles passarão a falhar com **401**. Para continuar os testes antigos, adaptar suas chamadas com Bearer Admin, sem voltar a deixar as rotas públicas.

O bootstrap do Admin pede senha localmente sem exibição e usa `PasswordHasher<UsuarioSistema>`; não há cadastro público de administradores. Cada Vendedor possui uma única conta associada a `VendedorId`, com índice único.

**A Aula 10.1 entrega acesso de leitura ao vendedor.** O envio de pedidos pelo vendedor autenticado ainda precisa de um endpoint que derive o vínculo do token, sem aceitar o `VendedorId` do corpo. Esse fluxo fica para uma subetapa posterior. Também faltam rate limiting, HTTPS de produção, política de bloqueio, refresh/revogação, auditoria detalhada e testes completos de isolamento com múltiplos usuários.

A implementação foi revisada estaticamente, mas o ambiente de criação do pacote não possui SDK .NET nem PowerShell; somente o Windows do desenvolvedor pode comprovar compilação e execução local.


## Correção do instalador do Program.cs

O instalador original `Aplicar_JWT_Program.ps1` exige que a declaração `var app = builder.Build();` ocupe toda a linha e não aceita comentário após o `;` nem a forma `WebApplication app = builder.Build();`. O desenvolvedor encontrou a mensagem `Nao foi possivel localizar um unico var app = builder.Build();` durante a Aula 10.1. **Não é uma falha confirmada do código C#** e o instalador interrompe antes de modificar o arquivo.

Foi fornecido um script local substituto `Aplicar_JWT_Program_CORRIGIDO.ps1`, que suporta espaços flexíveis, comentários ao fim da linha e declaração `WebApplication app`. Ele confere se há um único ponto de inserção, evita duplicar configuração JWT, cria backup antes de gravar e recusa executar se a estrutura real for diferente. Se a execução ainda falhar, revisar o `Program.cs` real e integrar manualmente, sem remover DbContext, Controllers ou outros serviços. **Correção ainda não validada no Windows do desenvolvedor.**
