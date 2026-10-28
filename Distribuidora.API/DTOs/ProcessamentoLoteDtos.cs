using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class ItemProcessamentoLoteDto
{
    [Range(1, int.MaxValue)]
    public int ProdutoId { get; set; }

    [Range(0, 1000000)]
    public int QuantidadeAtendida { get; set; }
}

public sealed class PedidoProcessamentoLoteDto
{
    [Range(1, int.MaxValue)]
    public int PedidoId { get; set; }

    [Required, MinLength(1), MaxLength(100)]
    public List<ItemProcessamentoLoteDto> Itens { get; set; } = new();
}

public sealed class ProcessarLoteDto
{
    [Required, MinLength(1), MaxLength(100)]
    public List<PedidoProcessamentoLoteDto> Pedidos { get; set; } = new();
}

public sealed record PedidoProcessamentoResumoDto(
    int PedidoId, int Status, string NomeStatus,
    int QuantidadeSolicitada, int QuantidadeAtendida,
    decimal ValorAtendido);

public sealed record ProcessamentoLoteRespostaDto(
    int LoteId, bool Processado, DateTime? DataProcessamentoUtc,
    int PedidosIncluidos, int PedidosProcessados,
    long TotalUnidadesAtendidas, decimal ValorAtendido,
    IReadOnlyList<PedidoProcessamentoResumoDto> Pedidos);
