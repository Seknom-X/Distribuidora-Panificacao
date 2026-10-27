# Aula 15.1 — Painel Administrativo Web MVC (login e consultas)

> **Estado em 26/10/2026:** pacote criado e conferido estruturalmente. **Aguardando compilação e execução no Windows do desenvolvedor.** A Aula 14.1 foi confirmada; não recebemos log dos testes da Aula 14.2.

## Objetivo

Criar o primeiro painel visual do projeto **Distribuidora de Panificação**, consumindo a API ASP.NET Core .NET 10 já existente, sem alteração do MySQL ou das regras de negócio.

**Escopo desta subetapa:** login do Administrador, dashboard, listagens de Vendedores/Clientes/Produtos/Vendas e logout. Somente leitura após o login. CRUD de vendedores/clientes/produtos e telas para pedidos/estoque/financeiro serão entregues nas Aulas 15.2–16.

## Arquitetura e segurança

Browser -> Web MVC (http://localhost:5220) -> API JWT (http://localhost:5210) -> MySQL (127.0.0.1:3307).

- Web envia email/senha para `POST /api/auth/login` do backend. Somente perfil `Admin` pode entrar no painel.
- MVC usa **ASP.NET Core CookieAuthentication**; JWT é guardado no ticket protegido por **Data Protection**, cookie HttpOnly, não em localStorage nem HTML.
- A cada consulta MVC -> API, o servidor envia `Authorization: Bearer <JWT>`, extraído do ticket autenticado.
- Cookie expira 1 minuto antes do JWT da API (60 minutos). Resposta API 401 encerra sessão e retorna à tela de login.
- Login e logout POST utilizam antiforgery token. URL de retorno é validada como local.
- `CookieSecurePolicy.SameAsRequest` é **somente para localhost em desenvolvimento**. Para produção são necessários HTTPS, `SecurePolicy.Always`, chaves Data Protection persistentes/protegidas e revisão de segurança.
- O MVC **não precisa CORS**, pois não envia requisições diretamente do JavaScript à API; são chamadas HTTP servidor-servidor.
- A API continua fonte da verdade. Não duplicar DbContext nem criar conexão MySQL no projeto Web.

## Arquivos e mudanças

ZIP: `Etapa15_1_Painel_MVC_Login_Dashboard_DistribuidoraPanificacao.zip` (18 arquivos). Inclui Controllers `AcessoController`, `PainelController`, serviço `ApiDistribuidoraCliente`, ViewModels, views Razor, layout próprio, CSS responsivo, `Aplicar_MVC_Program_Aula15.ps1` e `Executar_Teste_MVC_Aula15.ps1`.

**Nenhum `Program.cs` é substituído no ZIP**: o script insere a configuração mínima no **`Distribuidora.Web/Program.cs`** (após detectar marcadores do template MVC), faz backup em `%TEMP%` e recusa aplicar duas vezes.

**Não substitui nem altera qualquer arquivo de `Distribuidora.API`. Não há migration.** O painel lista até 100 vendas, porque este é o limite do endpoint da API.

## Instalação e testes

1. Fazer backup de `Distribuidora.Web/Program.cs`. Parar Web se estiver executando.
2. Extrair ZIP na raiz da solução `Distribuidora-Panificacao`.
3. Rodar **uma vez** `.\Aplicar_MVC_Program_Aula15.ps1`.
4. Compilar MVC: `dotnet build .\Distribuidora.Web\Distribuidora.Web.csproj`.
5. Terminal 1: `dotnet run --no-launch-profile --project .\Distribuidora.API --urls http://localhost:5210`.
6. Terminal 2: `dotnet run --no-launch-profile --project .\Distribuidora.Web --urls http://localhost:5220`.
7. Terminal 3: `.\Executar_Teste_MVC_Aula15.ps1`.
8. Navegador: `http://localhost:5220/acesso/entrar`; login Admin, visitar páginas, logout.

## Critérios de aceitação

| Cenário | Esperado |
|---|---|
| GET `/acesso/entrar` | HTTP 200, formulário com antiforgery token |
| GET `/painel` sem autenticação | Redireciona para login |
| Login Admin válido | Abre visão geral |
| Login Vendedor | Rejeita acesso administrativo |
| GET listas após login Admin | Consulta dados reais da API |
| API offline | Mensagem amigável, sem erro não tratado |
| Logout POST | Fecha cookie; próxima visita requer login |
| Alteração de banco | Nenhuma |

**Limitações:** Não há ainda formulários CRUD, sessões distribuídas, alta disponibilidade nem deploy HTTPS. Não armazenar senhas ou JWT no código/documentação. A API pode ainda precisar de melhorias de validação e integração antes de produção.

**Próxima subetapa:** Aula 15.2 — CRUD de Vendedores e Clientes pelo painel Web usando HTTP Admin + antiforgery.
