# Validação da revisão de 27/10/2026

- MVC: build aprovado, zero avisos/erros.
- API e biblioteca Offline: compiladas durante a execução dos testes.
- xUnit: 60 aprovados, zero falhas ou ignorados.
- GET /Conta/Login e /acesso/entrar: HTTP 200.
- Raiz e /Painel sem sessão: HTTP 302 para /Conta/Login.
- POST de login sem antiforgery: HTTP 400.
- API dashboard/clientes sem JWT: HTTP 401.
- API login vazio: HTTP 400.
- MVC com antiforgery e credenciais vazias: mensagem de rejeição retornada pela integração com a API.

As sondagens HTTP desta revisão usaram 15210/15220; os endereços de execução habitual são 5210/5220. Não foram interrompidos os servidores preexistentes.

## Pendências

Não foi homologado o login com Admin real, consultas consolidadas e fluxo completo de cadastros/pedidos/vendas/recebimentos contra MySQL. Os testes cobrem regras e sincronização com API simulada; não substituem integração HTTP completa, FEFO concorrente ou isolamento entre perfis em todos os módulos. Não foram executadas migrações, criados usuários ou alterados dados para obter credenciais.

Android: fontes preservadas, sem recompilação nem testes em aparelho nesta revisão. O relatório histórico de entrega registra a compilação anterior, que não comprova a experiência no dispositivo.

O painel usa formulários simples com IDs, algumas consultas limitadas e doze linhas no formulário de pedidos. A fila offline exige revisão manual em conflitos. SQLite depende da proteção do armazenamento do sistema e não tem criptografia própria. Auditoria original é posterior à operação, não transacional/inviolável. Não há integração bancária/fiscal real.
