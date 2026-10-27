# Aula 14.1 — Histórico de clientes e conferência financeira

> **Status:** implementação preparada, ainda sem compilação nem testes no Windows. **Aula 13:** o desenvolvedor informou que funcionou, mas não forneceu o log completo nem confirmou se executou o pagamento opcional de R$ 5,00.

## Objetivo e segurança

Completar as consultas necessárias para o futuro painel Web e aplicativo MAUI, **sem migrar ou alterar o banco**.

- Vendedor JWT consulta pedidos, compras efetivas e saldo devedor somente de clientes **ativos pertencentes à sua carteira**.
- Administrador JWT consulta conferência da consistência entre venda, conta, parcelas e recebimentos, apontando divergências sem corrigi-las automaticamente.
- Não confundir **auditoria de consistência** com **trilha de auditoria persistente**: o log de ações administrativas, cancelamentos e estornos ainda não foi implementado.

## Arquivos incluídos no pacote

1. `Distribuidora.API/DTOs/ConsultasAula14Dtos.cs`.
2. `Distribuidora.API/Services/ConferenciaFinanceiraRegras.cs`.
3. `Distribuidora.API/Controllers/HistoricoClienteVendedorController.cs`.
4. `Distribuidora.API/Controllers/AuditoriaFinanceiraController.cs`.
5. `Distribuidora.Tests/ConferenciaFinanceiraRegrasTests.cs`.
6. `Executar_Teste_Historico_Auditoria.ps1`.
7. `LEIA-ME_Aula14.md`.

**Somente arquivos novos.** Não substituir `Program.cs`, `DistribuidoraDbContext.cs` nem Controllers anteriores. **Não gerar nem aplicar migration**.

## Endpoints

| Método | Rota | Perfil |
|---|---|---|
| GET | `/api/me/clientes/{clienteId}/historico` | Vendedor JWT, cliente da carteira ativa |
| GET | `/api/admin/auditoria/contas/{contaId}` | Admin JWT |
| GET | `/api/admin/auditoria/financeiro?limite=20` | Admin JWT |

Histórico do vendedor: quantidades totais de pedidos e vendas, até 20 pedidos recentes, até 20 compras recentes, agregado de todos os títulos do cliente e saldo vencido. Valores e históricos só são consultados depois que a API valida o vínculo ao vendedor autenticado. A rota não recebe `vendedorId` fornecido pelo cliente.

Conferência financeira: usa soma dos recebimentos para verificar `ParcelaReceber.ValorPago`, valida `Soma(ValorOriginal das parcelas)=ContaReceber.ValorOriginal` e `ContaReceber.ValorOriginal=Venda.ValorTotal`. Sinaliza pagamentos negativos, acima do saldo e números de parcela inválidos/duplicados. A visão geral confere somente as últimas **1 a 100 contas**, conforme `limite`; não promete auditar todo o banco.

## Roteiro de validação

Com a API parada, extrair os arquivos na raiz da solução. Executar:

```powershell
cd C:\Users\diego\source\repos\Distribuidora-Panificacao
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet build .\Distribuidora.API\Distribuidora.API.csproj
dotnet test .\Distribuidora.Tests\Distribuidora.Tests.csproj
dotnet run --project .\Distribuidora.API --urls http://localhost:5210
```

Em **outro** PowerShell:

```powershell
.\Executar_Teste_Historico_Auditoria.ps1 -VendaId 1
```

O script pede login do Admin e do Vendedor sem exibir as senhas. Precisa existir uma conta a receber vinculada à venda 1 (Aula 13), e o vendedor autenticado precisa ter um cliente ativo (Aula 10.2). O teste consulta a conta, sua consistência, histórico e proteção: sem token **401**, papel errado **403**, cliente fora da carteira **404**.

**Não altera nenhum registro e não baixa estoque**. Se aparecer `Consistente = false`, investigar os alertas: não forçar o teste a passar editando manualmente o MySQL.

## Limitações

Não produz log permanente de ações; não concilia extratos bancários externos nem boletos/Pix reais. Concorrência, estornos, regras de juros/descontos e fechamento das janelas exigem testes posteriores. Não expor a API fora de localhost até completar a revisão de segurança.

**Aguardando `Build succeeded`, resultado xUnit e script para marcar concluída.**
