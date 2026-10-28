using Distribuidora.API.Services;
using Xunit;

namespace Distribuidora.Tests;

public sealed class PlanoCorteLoteTests
{
    [Fact]
    public void MesmosPedidosAceitaIdsForaDeOrdem()
        => Assert.True(PlanoCorteLote.MesmosPedidos(new[] { 5, 1, 9 }, new[] { 9, 5, 1 }));

    [Fact]
    public void MesmosPedidosRejeitaPedidoAusente()
        => Assert.False(PlanoCorteLote.MesmosPedidos(new[] { 1, 2 }, new[] { 2 }));

    [Fact]
    public void MesmosPedidosRejeitaIdsRepetidos()
        => Assert.False(PlanoCorteLote.MesmosPedidos(new[] { 1, 2 }, new[] { 1, 1 }));

    [Fact]
    public void MesmosProdutosRejeitaProdutoExtra()
        => Assert.False(PlanoCorteLote.MesmosProdutos(new[] { 3 }, new[] { 3, 4 }));
}
