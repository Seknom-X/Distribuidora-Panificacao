using Distribuidora.API.Services;
using Xunit;

namespace Distribuidora.Tests;

public sealed class ContasReceberRegrasTests
{
    [Fact]
    public void PagamentoParcialReduzSaldo()
    {
        Assert.True(ContasReceberRegras.PagamentoCabe(18.75m, 0m, 5m));
        Assert.Equal(13.75m, ContasReceberRegras.SaldoDevedor(18.75m, 5m));
    }

    [Fact]
    public void PagamentoRestantePodeZerar()
    {
        Assert.True(ContasReceberRegras.PagamentoCabe(18.75m, 5m, 13.75m));
        Assert.Equal(0m, ContasReceberRegras.SaldoDevedor(18.75m, 18.75m));
    }

    [Theory]
    [InlineData(18.75, 10.00, 9.00)]
    [InlineData(18.75, 0.00, 0.00)]
    [InlineData(18.75, 19.00, 1.00)]
    public void BloqueiaPagamentosInvalidos(double total, double pago, double novo)
    {
        Assert.False(ContasReceberRegras.PagamentoCabe(
            (decimal)total, (decimal)pago, (decimal)novo));
    }

    [Fact]
    public void RejeitaMaisDeDuasCasasDecimais()
    {
        Assert.False(ContasReceberRegras.ValorMonetarioPositivo(1.001m));
        Assert.True(ContasReceberRegras.ValorMonetarioPositivo(0.01m));
    }
}
