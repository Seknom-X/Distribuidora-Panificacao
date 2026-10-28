namespace Distribuidora.API.Services;

public sealed record IntervaloJanelaCorte(
    DateOnly DataCorte,
    int HoraCorte,
    DateTime InicioUtc,
    DateTime FimUtc);

public static class JanelaCorteService
{
    public const string FusoHorario = "America/Sao_Paulo";

    private static readonly TimeZoneInfo Fuso = ObterFuso();

    private static TimeZoneInfo ObterFuso()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(FusoHorario);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }

    public static DateOnly DataLocal(DateTime instanteUtc)
    {
        if (instanteUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("O instante precisa estar em UTC.", nameof(instanteUtc));

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(instanteUtc, Fuso));
    }

    public static (DateOnly Data, int Hora) ProximaJanela(DateTime agoraUtc)
    {
        if (agoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("O instante precisa estar em UTC.", nameof(agoraUtc));

        var agoraLocal = TimeZoneInfo.ConvertTimeFromUtc(agoraUtc, Fuso);
        var data = DateOnly.FromDateTime(agoraLocal);
        var hora = TimeOnly.FromDateTime(agoraLocal);

        if (hora <= new TimeOnly(12, 0)) return (data, 12);
        if (hora <= new TimeOnly(19, 0)) return (data, 19);
        return (data.AddDays(1), 12);
    }

    public static IntervaloJanelaCorte Criar(DateOnly dataCorte, int horaCorte)
    {
        if (horaCorte != 12 && horaCorte != 19)
            throw new ArgumentOutOfRangeException(nameof(horaCorte), "Horarios permitidos: 12 ou 19.");

        var fimLocal = dataCorte.ToDateTime(new TimeOnly(horaCorte, 0));
        var inicioLocal = horaCorte == 12
            ? dataCorte.AddDays(-1).ToDateTime(new TimeOnly(19, 0))
            : dataCorte.ToDateTime(new TimeOnly(12, 0));

        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicioLocal, Fuso);
        var fimUtc = TimeZoneInfo.ConvertTimeToUtc(fimLocal, Fuso);

        return new IntervaloJanelaCorte(dataCorte, horaCorte, inicioUtc, fimUtc);
    }
}
