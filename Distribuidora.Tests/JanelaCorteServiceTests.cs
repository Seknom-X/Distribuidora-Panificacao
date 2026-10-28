using Distribuidora.API.Services;
using Xunit;

namespace Distribuidora.Tests;

public sealed class JanelaCorteServiceTests
{
    [Theory]
    [InlineData("2026-10-23T14:59:59Z", 2026, 10, 23, 12)]
    [InlineData("2026-10-23T15:00:00Z", 2026, 10, 23, 12)]
    [InlineData("2026-10-23T15:00:01Z", 2026, 10, 23, 19)]
    [InlineData("2026-10-23T21:59:59Z", 2026, 10, 23, 19)]
    [InlineData("2026-10-23T22:00:00Z", 2026, 10, 23, 19)]
    [InlineData("2026-10-23T22:00:01Z", 2026, 10, 24, 12)]
    public void ProximaJanelaRespeitaOsCortesNoFusoDeSaoPaulo(
        string dataUtc, int ano, int mes, int dia, int hora)
    {
        var agora = DateTime.Parse(dataUtc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        var proxima = JanelaCorteService.ProximaJanela(agora);

        Assert.Equal(new DateOnly(ano, mes, dia), proxima.Data);
        Assert.Equal(hora, proxima.Hora);
    }

    [Fact]
    public void JanelaDoMeioDiaIncluiPeriodoNoturnoDesdeAs19HorasAnteriores()
    {
        var j = JanelaCorteService.Criar(new DateOnly(2026, 10, 23), 12);
        Assert.Equal(new DateTime(2026, 10, 22, 22, 0, 0, DateTimeKind.Utc), j.InicioUtc);
        Assert.Equal(new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc), j.FimUtc);
    }

    [Fact]
    public void JanelaDas19HorasComecaAoMeioDia()
    {
        var j = JanelaCorteService.Criar(new DateOnly(2026, 10, 23), 19);
        Assert.Equal(new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc), j.InicioUtc);
        Assert.Equal(new DateTime(2026, 10, 23, 22, 0, 0, DateTimeKind.Utc), j.FimUtc);
    }

    [Fact]
    public void JanelaInvalidaEhRejeitada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            JanelaCorteService.Criar(new DateOnly(2026, 10, 23), 14));
    }
}
