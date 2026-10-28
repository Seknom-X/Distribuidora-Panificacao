# Distribuidora de Panificação

MVP acadêmico B2B em .NET 10, ASP.NET Core MVC/API, EF Core/Pomelo e MySQL 8.4. Inclui fontes de aplicativo MAUI Android e biblioteca offline SQLite.

## Executar no Windows

Pré-requisitos: SDK .NET 10 e acesso ao MySQL existente. Na raiz do repositório, abra dois terminais PowerShell:

```powershell
powershell -File .\Iniciar-API.ps1
```

```powershell
powershell -File .\Iniciar-Painel.ps1
```

Os scripts executam `dotnet run`, que restaura dependências e compila automaticamente. Feche as instâncias antigas que ocupem as mesmas portas.

- API: http://localhost:5210/
- Login Admin: http://localhost:5220/Conta/Login
- Login compatível: http://localhost:5220/acesso/entrar
- Dashboard: http://localhost:5220/Painel

Antes de iniciar a API, configure conexão MySQL e JWT nos User Secrets ou em variáveis de ambiente, conforme [EXECUCAO.md](docs/EXECUCAO.md). O identificador de User Secrets da API foi preservado. Use seu Admin existente; não há senha fixa. Não execute os antigos scripts de aplicação de aulas sobre esta versão.

## Projetos e funcionalidades

- `Distribuidora.API`: JWT Admin/Vendedor, cadastros, pedidos, lotes/estoque, processamento de janelas, vendas, parcelas/recebimentos, roteiros e auditoria.
- `Distribuidora.Web`: autenticação Admin pela API, cookie protegido, antiforgery, dashboard e telas dos módulos.
- `Distribuidora.Offline`: carteira/catálogo em SQLite e fila persistente com UUID para reenvio de pedidos.
- `Distribuidora.App`: fontes MAUI Android; token no SecureStorage. Compilação separada, requer carga de trabalho Android.
- `Distribuidora.Tests`: testes de regras, validação e sincronização offline.

A solução principal não exige a carga Android. Para verificar:

```powershell
dotnet build DistribuidoraPanificacao.sln
dotnet test Distribuidora.Tests/Distribuidora.Tests.csproj
```

## Validação e limites

Nesta revisão, MVC compilou sem erros; API e Offline compilaram durante os testes e os 60 testes passaram. Testes HTTP verificaram os dois endereços de login, proteção do dashboard, antiforgery e respostas de autenticação da API. As sondagens usaram portas livres 15210/15220 porque as portas usuais estavam ocupadas.

Ainda faltam homologação com Admin real e MySQL, fluxo completo de negócio e testes em aparelho Android. Recebimentos são lançamentos acadêmicos, sem integração bancária/fiscal. Veja [VALIDACAO.md](docs/VALIDACAO.md).

## Dados e histórico

O repositório não inclui senhas, `.env`, User Secrets, bancos, volumes, ZIPs de aulas, APKs ou pastas de compilação. Não há migrações novas nem migração automática. Reutilize o banco existente e preserve seus backups; não remova volumes para atualizar o código.

O README anterior foi preservado em [HISTORICO-AULAS.md](docs/HISTORICO-AULAS.md). A documentação remota das aulas foi mantida; afirmações históricas de itens preparados ou pendentes devem ser lidas junto com a validação atual.
