namespace Distribuidora.API.DTOs;

public sealed record FecharJanelaDto(DateOnly DataCorte, int HoraCorte);

public sealed record PedidoLoteProcessamentoDto(
    int PedidoId, int ClienteId, int VendedorId,
    DateTime DataCriacaoUtc, int StatusAtual, string NomeStatusAtual);

public sealed record LoteProcessamentoRespostaDto(
    int Id, DateOnly DataCorte, int HoraCorte, string FusoHorario,
    DateTime InicioUtc, DateTime FimUtc, DateTime DataFechamentoUtc,
    int PedidosIncluidos, IReadOnlyList<PedidoLoteProcessamentoDto> Pedidos);
