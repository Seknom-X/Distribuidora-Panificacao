using Distribuidora.API.Services;
using Xunit;

namespace Distribuidora.Tests;

public sealed class JanelaFechamentoTests
{
    [Fact]
    public void JanelasDeMesmoDiaNaoCompartilhamHorarioExatoDoMeioDia()
    {
        var data = new DateOnly(2026, 10, 23);
        var almoco = JanelaCorteService.Criar(data, 12);
        var noite = JanelaCorteService.Criar(data, 19);
        var instanteDas12h = almoco.FimUtc;

        Assert.Equal(instanteDas12h, noite.InicioUtc);
        Assert.True(instanteDas12h > almoco.InicioUtc && instanteDas12h <= almoco.FimUtc);
        Assert.False(instanteDas12h > noite.InicioUtc && instanteDas12h <= noite.FimUtc);
    }

    [Fact]
    public void JanelasDaNoiteEManhaSeguinteSaoContiguas()
    {
        var data = new DateOnly(2026, 10, 23);
        var noite = JanelaCorteService.Criar(data, 19);
        var manhaSeguinte = JanelaCorteService.Criar(data.AddDays(1), 12);

        Assert.Equal(noite.FimUtc, manhaSeguinte.InicioUtc);
    }
}
