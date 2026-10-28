using Distribuidora.API.Services;
using Xunit;

namespace Distribuidora.Tests;

public sealed class ConferenciaFinanceiraRegrasTests
{
    [Fact]
    public void ContaComRecebimentoParcialCorretoEstaConsistente()
    {
        var dados = new[]
        {
            new ConferenciaFinanceiraRegras.ParcelaEntrada(1, 1, 18.75m, 5m, 5m),
            new ConferenciaFinanceiraRegras.ParcelaEntrada(2, 2, 18.75m, 0m, 0m)
        };
        var resultado = ConferenciaFinanceiraRegras.Conferir(1, 1, 37.50m, 37.50m, dados);

        Assert.True(resultado.Consistente);
        Assert.Equal(32.50m, resultado.SaldoDevedor);
        Assert.Equal(5m, resultado.SomaRecebimentos);
        Assert.Empty(resultado.Alertas);
    }

    [Fact]
    public void SomaRecebimentosDivergenteProduzAlerta()
    {
        var dados = new[]
        {
            new ConferenciaFinanceiraRegras.ParcelaEntrada(10, 1, 37.50m, 5m, 2m)
        };
        var resultado = ConferenciaFinanceiraRegras.Conferir(1, 1, 37.50m, 37.50m, dados);

        Assert.False(resultado.Consistente);
        Assert.Contains(resultado.Parcelas[0].Alertas,
            texto => texto.Contains("Soma de recebimentos"));
    }

    [Fact]
    public void SomaParcelasDiferenteDaContaProduzAlerta()
    {
        var dados = new[]
        {
            new ConferenciaFinanceiraRegras.ParcelaEntrada(10, 1, 20m, 0m, 0m)
        };
        var resultado = ConferenciaFinanceiraRegras.Conferir(1, 1, 37.50m, 37.50m, dados);

        Assert.False(resultado.Consistente);
        Assert.Contains(resultado.Alertas,
            texto => texto.Contains("Soma das parcelas"));
    }

    [Fact]
    public void PagamentoMaiorQueValorDaParcelaProduzAlerta()
    {
        var dados = new[]
        {
            new ConferenciaFinanceiraRegras.ParcelaEntrada(10, 1, 18.75m, 19m, 19m),
            new ConferenciaFinanceiraRegras.ParcelaEntrada(11, 2, 18.75m, 0m, 0m)
        };
        var resultado = ConferenciaFinanceiraRegras.Conferir(1, 1, 37.50m, 37.50m, dados);

        Assert.False(resultado.Consistente);
        Assert.Contains(resultado.Parcelas[0].Alertas,
            texto => texto.Contains("Valor pago fora"));
    }

    [Fact]
    public void ContaSemParcelasEInconsistente()
    {
        var resultado = ConferenciaFinanceiraRegras.Conferir(
            1, 1, 37.50m, 37.50m,
            Array.Empty<ConferenciaFinanceiraRegras.ParcelaEntrada>());

        Assert.False(resultado.Consistente);
        Assert.Contains(resultado.Alertas,
            texto => texto.Contains("Conta sem parcelas"));
    }
}
