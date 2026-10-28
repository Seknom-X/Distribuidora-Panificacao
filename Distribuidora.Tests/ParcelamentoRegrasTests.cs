using Distribuidora.API.Services;
using Xunit;

namespace Distribuidora.Tests;

public sealed class ParcelamentoRegrasTests
{
    [Fact]
    public void DuasParcelasMetadeDaVenda()
    {
        var parcelas = new[] { new RegraParcela(7, 50m), new RegraParcela(14, 50m) };
        var resultado = ParcelamentoRegras.Simular(37.50m, new DateOnly(2026, 10, 23), parcelas);
        Assert.Equal(2, resultado.Count);
        Assert.Equal(18.75m, resultado[0].Valor);
        Assert.Equal(18.75m, resultado[1].Valor);
        Assert.Equal(new DateOnly(2026, 10, 30), resultado[0].Vencimento);
        Assert.Equal(new DateOnly(2026, 11, 6), resultado[1].Vencimento);
        Assert.Equal(37.50m, resultado.Sum(p => p.Valor));
    }

    [Fact]
    public void TresParcelasDeVendaComArredondamento()
    {
        var parcelas = new[] {
            new RegraParcela(7, 33.33m),
            new RegraParcela(14, 33.33m),
            new RegraParcela(21, 33.34m)
        };
        var resultado = ParcelamentoRegras.Simular(100m, new DateOnly(2026, 10, 23), parcelas);
        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, resultado.Select(p => p.Valor));
        Assert.Equal(100m, resultado.Sum(p => p.Valor));
    }

    [Fact]
    public void RejeitaPercentualComSomaErrada()
    {
        Assert.False(ParcelamentoRegras.DistribuicaoValida(new[] {
            new RegraParcela(7, 40m), new RegraParcela(14, 50m)
        }));
    }

    [Fact]
    public void RejeitaDiasDuplicadosOuForaDeOrdem()
    {
        Assert.False(ParcelamentoRegras.DistribuicaoValida(new[] {
            new RegraParcela(7, 50m), new RegraParcela(7, 50m)
        }));
        Assert.False(ParcelamentoRegras.DistribuicaoValida(new[] {
            new RegraParcela(14, 50m), new RegraParcela(7, 50m)
        }));
    }

    [Fact]
    public void RejeitaFracoesComMaisDeDuasCasas()
    {
        Assert.False(ParcelamentoRegras.DistribuicaoValida(new[] {
            new RegraParcela(7, 33.333m), new RegraParcela(14, 66.667m)
        }));
    }

    [Fact]
    public void ImpedeParcelaZeradaPorVendaMuitoPequena()
    {
        var parcelas = new[] { new RegraParcela(7, 50m), new RegraParcela(14, 50m) };
        Assert.Throws<InvalidOperationException>(() =>
            ParcelamentoRegras.Simular(0.01m, new DateOnly(2026, 10, 23), parcelas));
    }
}
