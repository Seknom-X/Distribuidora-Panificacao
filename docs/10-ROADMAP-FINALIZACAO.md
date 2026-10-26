# Planejamento de conclusão — Distribuidora de Panificação

> **Atualizado em 26/10/2026.** Estimativa didática de **10 macroetapas** restantes (aulas 10 a 19) para o **MVP acadêmico** descrito no README. O número pode mudar conforme decisões do professor, resultados dos testes e detalhamento de interfaces. Não representa cronograma fechado.

## Entregas já validadas no ambiente local (segundo o desenvolvedor)

Aulas 1–9.5: estrutura .NET 10, MySQL Docker, EF Core e migrations; vendedores, clientes e roteiros; catálogo, lotes, validade e movimentações; pedidos pendentes e idempotência; corte FEFO manual; visualização de janelas às 12h e 19h; fechamento manual de janela; processamento real de um lote fechado com um pedido atendido parcialmente.

**Evidência Aula 9.5:** lote de processamento 1, pedido 1, produto 4, 8 solicitadas, 3 atendidas, valor R$ 37,50, status AtendidoParcialmente (4), `processado=True`; o desenvolvedor confirmou que a consulta SQL de rastreabilidade do débito no MySQL Workbench funcionou. Não declarar como realizados testes não apresentados de corrida/concorrência, rollback entre múltiplos pedidos ou autorização.

## Dez macroetapas sugeridas

| Aula | Entrega | Critério de saída |
|---|---|---|
| **10** | JWT: login, usuário Admin/Vendedor, autorização por papéis e carteira do vendedor | Admin pode gerir; vendedor só acessa própria carteira; 401/403 corretos |
| **11** | Geração de venda/itens de venda a partir do pedido processado, sem baixa de estoque duplicada | Venda reflete somente atendido; pedido não gera duas vendas |
| **12** | Condições de pagamento, formas e cálculo exato de parcelas | Parcelas somam o valor da venda; arredondamento validado |
| **13** | Contas a receber, pagamentos parciais/baixas, compras e débitos por cliente | Saldos e histórico consistentes; regras de encargos opcionais |
| **14** | Complementos de backend: municípios/UF, logs de auditoria, cancelamentos e regras de janela pendentes | Validações documentadas e testadas; fechar lacunas do MVP |
| **15** | MVC administrativo: login, layout, navegação e cadastros | Admin acessa e gerencia vendedores, clientes, grupos e produtos pelo navegador |
| **16** | MVC operacional: pedidos, janelas, corte, estoque, vendas e contas a receber | Fluxo operacional integral no navegador |
| **17** | Aplicativo .NET MAUI: navegação, login, carteira, roteiro, catálogo e SQLite | Vendedor consulta sua carteira e cria rascunhos locais no Android |
| **18** | MAUI: envio idempotente de pedidos e sincronização offline/online | Queda de conexão e reenvio não duplicam pedidos |
| **19** | Testes xUnit/integração MySQL, concorrência, autorização, documentação GitHub e apresentação acadêmica | Fluxo completo demonstrável, sem credenciais versionadas |

### MVP e evolução

O **MVP acadêmico** exige backend + web + app Android + SQLite offline + autenticação + venda + financeiro cadastral + testes. Integrações bancárias reais (Pix/boleto), integração fiscal, otimização geográfica, notificações push, rastreamento logístico e corte proporcional automático são **evolução**, fora destas dez macroetapas salvo exigência posterior.

**Observações de gestão:** não existe percentual rigoroso de conclusão sem estimar esforço por interface, persistência, testes e requisitos definidos pelo professor. O número "10" representa blocos didáticos, e alguns podem exigir mais de uma aula. Dar prioridade ao JWT, pois os endpoints de estoque e pedidos atuais não devem ser disponibilizados publicamente sem autorização. Proteger a API antes de integrar Web e Mobile.
