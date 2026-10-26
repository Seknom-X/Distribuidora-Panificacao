# Aula 10.2 — Pedido autenticado pelo vendedor

> **Status em 26/10/2026:** patch preparado; aguardando `dotnet build`, xUnit e script PowerShell no Windows do desenvolvedor. **Não há migration nova.**

## Contexto

A Aula 10.1 já permite login JWT de Admin e Vendedor. O usuário confirmou o script `Executar_Teste_JWT_Vendedor.ps1` com o seguinte resultado: login com perfil Vendedor aprovado, claim vinculada ao vendedor ativo, **1 cliente e 1 pedido da própria carteira** e HTTP **403** ao tentar acessar CRUD administrativo de produtos, lista global de vendedores e usuários. Nenhum estoque foi movimentado. A execução específica do script `Executar_Teste_JWT_Admin.ps1` não foi apresentada.

## Objetivo desta aula

O vendedor precisa criar um pedido **sem informar ou controlar `VendedorId`**. O servidor usará somente a claim `vendedorId` de um JWT validado, verificará que o cliente pertence à sua carteira e aplicará as mesmas regras da Aula 9.1: pedido **Pendente**, itens com `QuantidadeAtendida = 0`, preço histórico e chave de idempotência UUID. Nenhuma reserva ou baixa de estoque.

## Arquivos do pacote

- `Distribuidora.API/Controllers/PedidosVendedorController.cs`: novas rotas POST/GET específicas do vendedor.
- `Distribuidora.API/DTOs/PedidoVendedorCriarDto.cs`: não possui propriedade `VendedorId`.
- `Distribuidora.Tests/PedidoVendedorCriarDtoTests.cs`: testes de contrato e validação.
- `Executar_Teste_Pedido_JWT_Vendedor.ps1`: teste de integração da API autenticada.
- `LEIA-ME_Aula10_2.md`: passo a passo.

**Nenhum arquivo existente é substituído.** As rotas antigas `/api/pedidos` continuam administrativas pela configuração padrão JWT da Aula 10.1, e a lista existente `GET /api/me/pedidos` não é alterada.

## Endpoints

| Método | Rota | Restrição |
|---|---|---|
| POST | `/api/me/pedidos` | Vendedor autenticado, cliente da própria carteira |
| GET | `/api/me/pedidos/{id}` | Vendedor autenticado, somente seu pedido |
| GET | `/api/me/pedidos` | Já existia desde a Aula 10.1; lista os pedidos do próprio vendedor |

Exemplo JSON de POST:

```json
{
  "chaveRequisicao": "f61c4399-751f-45a3-81bb-7fbb8df9b184",
  "clienteId": 7,
  "itens": [
    { "produtoId": 4, "quantidadeSolicitada": 2 }
  ]
}
```

Os IDs são apenas exemplos; consultar os clientes do vendedor e os produtos ativos antes do envio. Um campo extra `vendedorId` no JSON **não altera o vendedor usado pelo servidor**.

## Regras e retorno esperado

- Primeiro cadastro HTTP 201, status `Pendente=1`, atendimento zero.
- Reenvio idêntico com mesma chave: HTTP 200 e **mesmo ID**.
- Reutilização da chave com dados diferentes ou vendedor diferente: HTTP 409, sem revelar pedido alheio.
- Cliente inativo ou de outra carteira: HTTP 404; quantidade inválida: HTTP 400.
- Sem token: HTTP 401; vendedor na rota global administrativa: HTTP 403.
- Buscar por ID de outro vendedor: HTTP 404.
- Não há consumo de estoque ou corte nesta etapa.

## Validação local

1. Parar a API; extrair pacote na raiz da solução.
2. `dotnet build .\Distribuidora.API\Distribuidora.API.csproj`.
3. `dotnet test .\Distribuidora.Tests\Distribuidora.Tests.csproj`.
4. `dotnet run --project .\Distribuidora.API --urls http://localhost:5210`.
5. Em outro terminal: `.\Executar_Teste_Pedido_JWT_Vendedor.ps1`.

O script pede credenciais do Vendedor sem exibi-las, usa cliente ativo da própria carteira, produto ativo, testa reenviar a mesma chave, valida 409 para payload diferente e 401/403 nas rotas. **Cria um pedido fictício por execução, e ele permanece no banco.** Para testar cliente de outro vendedor, opcionalmente informar `-ClienteOutraCarteiraId ID_REAL`, obtido em MySQL apenas para desenvolvimento.

## Limitações

- **Ainda não testado no Windows do desenvolvedor.** Gerador do patch não possui SDK .NET nem PowerShell.
- Os primeiros testes automatizados de DTO não substituem testes com requisições concorrentes ou banco separado de integração.
- JWT e autorização por papéis não tornam a API pronta para produção: faltam HTTPS, limitação de requisições, auditoria e revisão de segurança.
- O envio de pedidos offline no MAUI será implementado nas Aulas 17–18.


## Correção do teste de carteira vazia — 26/10/2026

Na execução do script `Executar_Teste_Pedido_JWT_Vendedor.ps1`, o desenvolvedor recebeu `FALHA: A carteira nao tem clientes ativos`. Na Aula 10.1, o script de teste antigo informou um cliente e um pedido, mas usava `@(Invoke-RestMethod ...)`, cuja contagem de listas vazias pode ser enganosa no Windows PowerShell. O script da Aula 10.2 filtrou os itens não nulos e por isso identificou carteira vazia antes de criar pedido.

**Diagnóstico definitivo exige consulta:** `SELECT Id, RazaoSocial, VendedorId, Ativo FROM Clientes WHERE VendedorId=5;` no banco local `DistribuidoraDb`. A API `GET /api/me/clientes` retorna somente `Ativo=1` e `VendedorId` da claim JWT.

Pacote de recuperação `Correcao_Aula10_2_Carteira_e_Teste_JWT.zip` inclui dois scripts: `Executar_Teste_Pedido_JWT_Vendedor_CORRIGIDO.ps1` (contagem por JSON bruto, diagnóstico, demais verificações) e `Cadastrar_Cliente_Teste_JWT_Admin.ps1` (login Admin, consulta carteira, criação opcional de padaria fictícia para vendedor ativo, somente com confirmação S, somente localhost). Nenhum arquivo C# é alterado e nenhuma migration é necessária. A criação de cliente/pedido de teste modifica apenas dados do MySQL de desenvolvimento, sem baixa de estoque.

**Status:** correção entregue; cadastro do cliente e aprovação da Aula 10.2 ainda precisam ser confirmados pelo desenvolvedor.
