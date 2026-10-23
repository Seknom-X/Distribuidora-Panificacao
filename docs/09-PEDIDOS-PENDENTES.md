# Aula 9.1 — Cadastro e consulta de pedidos pendentes

> **Estado:** implementação preparada em 23/10/2026; aguardando compilação, migration e testes na máquina do desenvolvedor.

## Objetivo

O vendedor registra pedidos de clientes de sua carteira sem reservar, descontar ou transferir o estoque. Por exemplo, é válido registrar pedido de 8 unidades de fermento mesmo havendo 5 unidades em estoque. A quantidade atendida é inicializada em **0**, e a decisão de corte é posterior.

## Regras de negócio

- Vendedor e cliente devem estar ativos; cliente pertence ao vendedor informado.
- Produto deve existir, estar ativo e ter preço positivo.
- Pedido tem de 1 a 100 itens diferentes, com quantidades de 1 a 1.000.000 por produto (limites provisórios para testes).
- O preço do produto é copiado para o item no momento do cadastro, preservando o histórico.
- Pedido começa com `Status = Pendente (1)` e `QuantidadeAtendida = 0`.
- Idempotência por `ChaveRequisicao` (UUID no formato padrão): requisição idêntica repetida retorna HTTP 200 com o mesmo pedido; dados diferentes com a mesma chave retornam HTTP 409; primeiro cadastro retorna 201.
- A lista retorna no máximo 100 pedidos mais recentes, com filtros opcionais.

## Endpoints

| Método | Endpoint | Objetivo |
|---|---|---|
| POST | `/api/pedidos` | Registrar pedido pendente |
| GET | `/api/pedidos/{id}` | Consultar pedido |
| GET | `/api/pedidos?vendedorId={id}&clienteId={id}&status=1` | Consultar carteira de pedidos |

Exemplo:

```json
{
  "chaveRequisicao": "b6f90e6b-d9b3-48c5-901e-a7342b820cbb",
  "clienteId": 1,
  "vendedorId": 2,
  "itens": [
    { "produtoId": 3, "quantidadeSolicitada": 8 }
  ]
}
```

## Estrutura e migration

- Atualização de `Models/Pedido.cs`: propriedade opcional `ChaveRequisicao` para compatibilidade com pedidos anteriores.
- Atualização de `Data/DistribuidoraDbContext.cs`: comprimento 36 e índice único da chave.
- Novos `DTOs/PedidoDtos.cs` e `Controllers/PedidosController.cs`.
- Reaproveitamento das tabelas `Pedidos` e `ItensPedido` existentes.
- Migration sugerida: `PedidoChaveRequisicao`, **somente** nova coluna nullable e índice único; não apagar dados ou gerar outra InitialCreate.

## Critérios de aceite

O teste PowerShell `Executar_Teste_Pedidos.ps1` prepara um vendedor, um cliente, um grupo, um produto e um lote de 5 unidades. Cadastra pedido de 8 unidades; confere status Pendente, atendido 0, total 100,00 (preço unitário 12,50), saldo inalterado em 5, reenvio idempotente com mesmo ID, conflito 409 para chave reutilizada com quantidade diferente, e 400 para produto duplicado.

Os registros do script **não são removidos** e devem ser criados apenas no MySQL local (Docker, porta 3307).

## Segurança e próximos passos

Os Controllers ainda não estão protegidos por JWT: usar somente em ambiente de desenvolvimento local, jamais expor em rede pública. O `VendedorId` enviado é conferido contra a carteira do cliente, mas ainda não identifica um usuário autenticado. Nesta etapa não há corte, FEFO, baixa, faturamento nem processamento automático em horários fixos; esses fluxos serão desenvolvidos em etapas posteriores.

**Testes ainda não executados na máquina do desenvolvedor.** Atualizar este documento com evidências de execução e o andamento no README quando forem fornecidas.
