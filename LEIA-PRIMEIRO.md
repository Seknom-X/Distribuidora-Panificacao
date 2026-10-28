# Início rápido

Consulte o [README](README.md) para executar a API e o painel. Os scripts de início ficam na raiz e fazem restauração/compilação automaticamente.

Login: http://localhost:5220/Conta/Login. O endereço http://localhost:5220/acesso/entrar também é aceito.

Para atualizar uma instalação existente, compare o código em uma pasta separada antes de incorporar alterações. Preserve `.env`, User Secrets, alterações locais e dados MySQL. Não extraia pacotes antigos de aulas por cima desta revisão. Não há script de sobrescrita ou migração automática.

Configure conexão e JWT conforme [EXECUCAO.md](docs/EXECUCAO.md). O relatório [VALIDACAO.md](docs/VALIDACAO.md) diferencia verificações executadas de pendências de homologação.
