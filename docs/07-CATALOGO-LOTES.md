# Aula 7 — Grupos, produtos e lotes de estoque

> **Status: implementação preparada; aguardando compilação, migration e testes no Windows.**
> **Ambiente:** .NET 10, EF Core 9, Pomelo 9, MySQL 8.4 no Docker, porta 3307.

## Objetivo

Modelar o catálogo de produtos de panificação com agrupamento e lotes individuais com data de validade. Distinguir estoque físico (todos os lotes), estoque não vencido e estoque vencido. Ainda **não** implementar saída/reserva de estoque ou FEFO automatizado.

## Modelo proposto

- **GrupoProduto:** Id, Nome, Descricao, Ativo. Um grupo pode conter vários produtos.
- **Produto:** Id, Nome, UnidadeMedida, PrecoVenda, Ativo, GrupoProdutoId. Para não bloquear uma migration com possíveis produtos antigos, GrupoProdutoId será *nullable* no banco; a API exige grupo para produtos novos.
- **LoteEstoque:** Id, ProdutoId, CodigoLote, DataValidade, DataEntradaUtc, QuantidadeDisponivel. Código único por produto, data sem horário e quantidade de entrada positiva.

O relacionamento Produto 1:N LoteEstoque permite controlar quantidades e datas de validade individualmente. Desativações preservam o histórico. Um grupo com produtos ativos não poderá ser desativado.

## Endpoints planejados para esta aula

| Verbo | Rota | Objetivo |
|---|---|---|
| GET/POST | `/api/grupos-produto` | Consultar e cadastrar grupos |
| GET/PUT | `/api/grupos-produto/{id}` | Consultar e editar grupo |
| PATCH | `/api/grupos-produto/{id}/desativar` | Desativar grupo, impedindo se houver produtos ativos |
| GET/POST | `/api/produtos` | Consultar e cadastrar produtos |
| GET/PUT | `/api/produtos/{id}` | Consultar e editar produto |
| PATCH | `/api/produtos/{id}/desativar` | Desativar sem apagar lotes |
| GET/POST | `/api/produtos/{produtoId}/lotes` | Listar por validade e cadastrar lote |
| GET | `/api/produtos/{produtoId}/lotes/{loteId}` | Consultar lote |
| GET | `/api/produtos/{produtoId}/estoque` | Resumo físico, não vencido e vencido |

## Migração e critérios de validação

1. Parar a API e salvar cópia de `Produto.cs` e `DistribuidoraDbContext.cs`.
2. Integrar os arquivos da Aula 7 na solução local. A migration anterior `ClientesAtivo` deve permanecer intacta.
3. Executar `dotnet build Distribuidora.API/Distribuidora.API.csproj`.
4. Criar migration `CatalogoLotes` com `dotnet tool run dotnet-ef migrations add CatalogoLotes --project Distribuidora.API --startup-project Distribuidora.API --context DistribuidoraDbContext --output-dir Data/Migrations`.
5. Inspecionar a migration gerada. Ela deve **criar** `GruposProduto` e `LotesEstoque` e **adicionar** uma FK nullable em `Produtos`, sem excluir tabelas existentes.
6. Aplicar `dotnet tool run dotnet-ef database update --project Distribuidora.API --startup-project Distribuidora.API --context DistribuidoraDbContext`.
7. Testar grupo, produto, dois lotes, saldo 20, conflito de lote duplicado (409) e quantidade zero (400). Conferir no Workbench, porta 3307.

**Observações:** datas de vencimento são comparadas à data UTC nesta primeira etapa; precisamos escolher formalmente o fuso da distribuidora antes de operar pedidos. Não há ainda movimentação de estoque, reserva, baixa, FEFO ou autenticação JWT. Os endpoints somente devem ser testados em localhost até a implantação das autorizações.

## Histórico e documentação viva

Aulas 1 a 6 foram desenvolvidas e o usuário confirmou o funcionamento do roteiro. Esta Aula 7 foi validada com os resultados enviados pelo desenvolvedor; veja os resultados de teste abaixo.


## Resultados comprovados pelo teste da Aula 7

**Status atualizado em 22/10/2026: concluída no ambiente local, segundo o retorno do PowerShell enviado pelo desenvolvedor.**

- Saldo físico = 20, estoque não vencido = 20 e vencido = 0.
- Lote duplicado: HTTP 409.
- Quantidade zero: HTTP 400.
- Desativação de grupo com produto ativo: HTTP 409.

O script precisou de um pequeno ajuste na leitura da coleção retornada pelo PowerShell antes de passar. Os resultados acima confirmam os testes citados, não uma revisão independente de todo o projeto. [Próxima etapa: movimentações por lote](08-MOVIMENTACOES-ESTOQUE.md).
