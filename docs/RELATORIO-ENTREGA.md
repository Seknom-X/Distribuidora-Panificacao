> Relatório histórico da entrega anterior. Consulte VALIDACAO.md e README.md para o estado atual e a publicação do código. Referências à ausência de envio remoto descrevem aquela entrega.

# Relatório de entrega — 27/10/2026

## Base inspecionada

Arquivo original `Distribuidora-Panificacao.rar` recuperado do anexo da conversa. O anexo contém API, EF/Pomelo, migrações, testes e um Web MVC ainda padrão; não continha aplicativo MAUI. O histórico Git embarcado indicava HEAD `f02176d0dcd8234e83553f0445fa64a08c7641ab`, com arquivos majoritariamente não rastreados e README ausente.

O GitHub `Seknom-X/Distribuidora-Panificacao` estava acessível, com permissão de escrita, e a main consultada apontava para `c32c3b10c23a0f03f27aa3ed6d04965efb86daeb`. Sua documentação mencionava uma etapa MVC preparada, sem comprovar a instalação no anexo. As bases divergem; não houve push, commit remoto ou substituição de arquivos remotos. A entrega local tem como base o conteúdo real do RAR, e não uma reconstrução do README remoto.

## Implementado nesta entrega

- Painel MVC: autenticação de Admin pela API, sessão em cookie protegido por Data Protection, logout POST, antiforgery global e rotas restritas a Admin.
- Dashboard com totais consolidados de vendedores/clientes/produtos ativos, pedidos pendentes, vendas, valor vendido, saldo a receber, saldo vencido e lotes vencidos com saldo. Consultas calculadas no banco, sem depender do limite das listagens.
- Formulários de cadastro, edição e desativação de vendedores/clientes/produtos/grupos; acesso de vendedor; criação de pedidos com múltiplos itens; entrada e movimentação de lotes; resumo de estoque; fechamento e processamento de janelas; geração de venda, condições, conta e recebimentos; consultas de auditoria, financeiro, contas paginadas e roteiros/reordenação.
- API: endpoints Admin de dashboard e contas paginadas, tratamento padronizado de exceções, limite de tentativas de login por IP, senha mínima corrigida para 12 caracteres na criação de Admin/Vendedor. MySQL 8.4 explícito no provider evita autodetecção do servidor durante a configuração da aplicação.
- Biblioteca offline: cache SQLite atualizado em transação, validação local da carteira/catálogo, fila persistente com UUID/corpo imutáveis, confirmação por ID remoto, proteção contra sincronizações simultâneas, reenvio após falha, bloqueio de rejeições de negócio, interrupção por sessão inválida.
- MAUI Android: login Vendedor, token em SecureStorage, base por URL/conta, continuidade offline após reiniciar, roteiro em cache, seleção de cliente/produto, carrinho, salvamento offline, consulta de filas e sincronização.
- Testes adicionais de senha e SQLite: persistência após reabertura, reenvio idêntico após falha, ausência de reenvio após sucesso, conflito sem loop e rejeição de cliente fora da carteira/produto duplicado.
- Documentação de instalação, configuração segura, demonstração e limitações. Histórico de scripts e documentos originais preservado no código, sem executar scripts de instalação das aulas.

## Evidências de validação

| Verificação | Resultado |
|---|---|
| Testes existentes antes das mudanças | 55 passaram |
| Testes após inclusão da camada offline e segurança | 60 passaram; 0 falhas; 0 ignorados |
| Build API | Sucesso, sem avisos ou erros |
| Build MVC | Sucesso, sem avisos ou erros |
| Build MAUI Android | Sucesso; APK de depuração gerado |
| NuGet Android | Aviso NU1900: serviço de consulta de vulnerabilidades indisponível; não equivale a auditoria de dependências aprovada |
| API GET clientes sem token | HTTP 401 |
| API GET dashboard sem token | HTTP 401 |
| API login com objeto vazio | HTTP 400 |
| MVC raiz sem sessão | HTTP 302 para login |
| MVC página login | HTTP 200, token antiforgery presente |
| MVC POST login sem antiforgery | HTTP 400 |
| Docker existente | `distribuidora-mysql` ativo em 127.0.0.1:3307 |

A primeira execução dos testes no ambiente restrito falhou ao conectar ao testhost; com a permissão necessária, os testes passaram. A primeira compilação Android restrita também não concluiu; a compilação com a permissão necessária concluiu e gerou o APK. Os certificados de desenvolvimento existentes estavam sem confiança estabelecida; as sondagens HTTP usaram exceção de certificado SOMENTE no cliente de teste para localhost. O código de produção não contém bypass de TLS.

## Pendências e limites — não declarar validação integral

1. Não foi executado o fluxo autenticado completo sobre o MySQL existente: login real, CRUD, FEFO concorrente, janelas, venda, parcelas e recebimentos pelas novas telas. Não foram criados usuários nem alterados dados para obter credenciais de teste. As novas consultas consolidadas precisam ser exercitadas contra o schema MySQL atualizado.
2. O APK não foi instalado nem testado visualmente em aparelho/emulador. Falta conferir navegação, teclado, conectividade HTTPS, confiança do certificado, SecureStorage e biblioteca SQLite nativa no dispositivo. Build e testes da biblioteca no Windows não comprovam o funcionamento no Android.
3. Faltam testes de integração HTTP de isolamento Admin/Vendedor, concorrência entre múltiplos clientes e toda a cadeia de negócio. Os testes entregues são de regras/DTOs e persistência/sincronização local com API simulada; não substituem esses testes.
4. Interfaces são funcionais e simples: vínculos no painel usam IDs, pedidos têm 12 linhas no formulário, auditoria usa consulta limitada e não há relatórios exportáveis nem filtros completos em todas as listas. Conflitos offline exigem análise manual, sem editor/reconciliação automática. Não há sincronização automática em segundo plano, GPS, otimização de rotas ou push.
5. SQLite sem criptografia própria; cache retido por conta para preservar pedidos ao sair. Não implementar uso com dados sensíveis de produção sem rever armazenamento, retenção e requisitos de proteção de dados.
6. Auditoria original permanece posterior e independente da operação, não transacional/inviolável. CNPJ mantém a validação numérica de 14 dígitos existente, sem cálculo dos verificadores. Não houve revisão exaustiva de todas as regras anteriores.
7. Pix, boleto, fiscal e distribuição em loja Android não fazem parte do MVP acadêmico. Recebimento é apenas um lançamento interno.
8. Atualização no GitHub permanece pendente de comparação e integração em branch própria; o remoto não foi modificado.

## Preservação

Nenhum código do projeto LojaLimpeza foi acessado ou alterado. Não houve remoção, recriação ou migração automática de MySQL, dados ou volumes. Não foram empacotados `.env`, User Secrets, tokens, chaves, bancos locais, bin/obj, backups ou `.git`. O ZIP contém código editável; o APK é um artefato de depuração separado.

Esta é uma implementação do MVP com build e testes parciais comprovados, **não uma homologação completa nem uma afirmação de que todas as lacunas do projeto foram eliminadas**. A revisão deve começar pelos itens 1–3 acima usando os passos de `EXECUCAO.md`.
