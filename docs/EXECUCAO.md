> Revisão atual: leia primeiro [LEIA-PRIMEIRO.md](../LEIA-PRIMEIRO.md). Os scripts locais usam HTTP 5210/5220 em Development. Os perfis HTTPS abaixo continuam disponíveis.

# Roteiro de execução

## Pré-requisitos

.NET SDK 10, Docker Desktop com Linux containers e MySQL 8.4. Para Android: Visual Studio com MAUI/Android, SDK Android, JDK e aparelho/emulador. O APK fornecido é de depuração, sem finalidade de publicação em loja.

Extraia o ZIP em uma pasta NOVA. Compare arquivos antes de incorporar ao seu diretório atual. Não execute scripts antigos de aplicação de aulas sobre esta entrega; eles foram preservados apenas como histórico de trabalho.

## Banco e configuração

O Compose original mantém `DistribuidoraDb` na porta `127.0.0.1:3307`, container `distribuidora-mysql` e volume `mysql_data`. Se já estiver funcionando, reutilize essa instância. **Não execute `docker compose down -v`, não recrie volumes e não execute EnsureDeleted.** Nenhum desses comandos é necessário.

Para uma instalação nova, crie `.env` localmente com `MYSQL_ROOT_PASSWORD` e `MYSQL_PASSWORD`, escolhidos por você. O arquivo é ignorado. Inicie com `docker compose up -d`. Não troque senhas em uma instalação existente sem planejar a alteração no próprio MySQL.

Configure pela gestão de User Secrets do Visual Studio ou pelas variáveis de ambiente, sem gravá-las no repositório:

| Configuração API | Variável de ambiente | Valor esperado |
|---|---|---|
| ConnectionStrings:DistribuidoraDb | ConnectionStrings__DistribuidoraDb | Server=localhost;Port=3307;Database=DistribuidoraDb;User=distribuidora_dev;Password=(sua senha local) |
| Jwt:Chave | Jwt__Chave | Base64 de pelo menos 32 bytes aleatórios |
| Jwt:Emissor | Jwt__Emissor | Identificador local escolhido para a API |
| Jwt:Audiencia | Jwt__Audiencia | Identificador local escolhido para os clientes |

O `UserSecretsId` original da API foi mantido para reaproveitar sua configuração local. Não copie `secrets.json` para o projeto. O aplicativo nunca recebe a chave de assinatura JWT.

Antes de iniciar, confirme o certificado local com `dotnet dev-certs https --check` e, se necessário, configure um certificado de desenvolvimento confiável. Não desabilite a validação de certificados no código.

Na raiz da entrega:

```powershell
dotnet restore DistribuidoraPanificacao.sln
dotnet build DistribuidoraPanificacao.sln
dotnet test Distribuidora.Tests/Distribuidora.Tests.csproj
dotnet tool restore
```

Só se houver migrações pendentes, após backup e revisão do banco de destino:

```powershell
dotnet ef migrations list --project Distribuidora.API --startup-project Distribuidora.API
dotnet ef database update --project Distribuidora.API --startup-project Distribuidora.API
```

A entrega não adiciona migrações. As migrações existentes vão de `InitialCreate` a `RegistrosAuditoria`. Se seu banco já estiver atualizado, nenhuma atualização é necessária.

## API e painel

Em um terminal:

```powershell
dotnet run --project Distribuidora.API --launch-profile https
```

API: `https://localhost:7080`. Em outro terminal:

```powershell
dotnet run --project Distribuidora.Web --launch-profile https
```

Painel: `https://localhost:7253`. O endereço da API é configurável por `Api:BaseUrl` / `Api__BaseUrl`; o padrão corresponde a `https://localhost:7080/`.

Use seu Admin existente. Em banco novo SEM usuários, o bootstrap exclusivamente local pode ser executado em Development:

```powershell
dotnet run --project Distribuidora.API --launch-profile https -- --criar-admin
```

Ele pede nome, e-mail e senha oculta com mínimo de 12 caracteres. Não há endpoint HTTP público para criar Admin. Se já existem usuários, o bootstrap se recusa a recriar o administrador. A ferramenta de recuperação original foi preservada, mas não foi executada.

## Demonstração acadêmica

1. Acesse o painel como Admin. Confira os totais consolidados do dashboard.
2. Cadastre vendedor, grupo, produto com preço positivo e cliente vinculado ao vendedor. As operações de edição aparecem nas listagens. Desativar preserva os registros históricos.
3. Cadastre um lote de produto com validade futura e quantidade positiva. Consulte o resumo do estoque.
4. Crie o acesso do vendedor pelo painel (e-mail único, senha com pelo menos 12 caracteres).
5. Crie pedidos no painel ou no Android. Cada pedido possui identificador UUID; em caso de falha de resposta, preserve esse identificador ao tentar novamente.
6. Consulte as janelas existentes; feche uma janela válida pelo painel, informando data e hora de corte previstas pela regra da API. Processe o lote resultante. O atendimento segue as regras FEFO existentes.
7. Gere a venda a partir de pedido atendido, integral ou parcialmente. Gere a conta a receber escolhendo uma condição cadastrada. Para a condição, informe dias após venda e percentuais somando 100.
8. Consulte contas e parcelas e registre recebimento pelo código da parcela. A chave de operação protege o reenvio; o registro não movimenta dinheiro real.
9. Confira auditoria e conferência financeira. Consulte/reordene o roteiro fornecendo TODOS os clientes ativos da carteira na ordem pretendida.

O formulário de pedidos mostra 12 linhas por operação; a API suporta até 100 itens. Os formulários básicos usam códigos numéricos: consulte as listagens antes de informar vínculos. As listas originais da API têm seus limites de consulta; contas possuem consulta paginada pelo painel. Regras e erros de negócio são aplicados pela API.

## Android

```powershell
dotnet build Distribuidora.App/Distribuidora.App.csproj -f net10.0-android
```

Instale o APK de depuração em um aparelho de teste ou execute o projeto no emulador pelo Visual Studio. A instalação e a experiência em aparelho não foram validadas nesta entrega.

A API deve ser acessível ao aparelho por **HTTPS com certificado confiável no Android**. `localhost` no aparelho aponta para o próprio aparelho. No emulador, `10.0.2.2` normalmente aponta para o computador, mas o certificado deve incluir e ser confiável para o endereço usado. Configure o endereço de rede da API, firewall e certificado apropriados ao seu ambiente. Não exponha a porta MySQL e não remova JWT, HTTPS ou autorização para fazer a demonstração.

1. Informe URL HTTPS, e-mail e senha de Vendedor. Faça o primeiro login online.
2. Toque em Atualizar carteira e catálogo e em Carregar seletores do cache.
3. Abra o roteiro. Ele usa a ordem de visita e os endereços da carteira em cache.
4. Fique offline, escolha cliente/produtos, adicione quantidades e salve o pedido. A confirmação local NÃO significa estoque reservado ou pedido enviado.
5. Reinicie o aplicativo e use Continuar offline: cache e fila permanecem no SQLite.
6. Retome a conexão e sincronize. Pedidos confirmados não são reenviados. Timeout/falha de rede mantém corpo e UUID; rejeições de negócio ficam em Revisar e não entram em loop.
7. Sessão expirada exige novo login online. Entre com a MESMA URL e conta para acessar a mesma base local.

O cache é particionado por URL/conta; tokens ficam no SecureStorage e não no SQLite. Sair remove o token e encerra o acesso, mantendo os pedidos locais para o próximo login da mesma conta. Faça sincronização antes de desinstalar ou limpar os dados do aplicativo. O cache SQLite não possui criptografia própria; depende da proteção de armazenamento do Android, sem backup automático.

Pedidos em Revisar exigem análise do erro e conferência com o Admin; não existe editor automático de conflitos nesta versão. Não crie outro pedido para substituir uma operação cujo resultado ainda seja incerto.
