using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class ItemCortePedidoDto
{
    [Range(1, int.MaxValue)]
    public int ProdutoId { get; set; }

    [Range(0, 1000000)]
    public int QuantidadeAtendida { get; set; }
}

public sealed class CortePedidoDto
{
    [Required, MinLength(1), MaxLength(100)]
    public List<ItemCortePedidoDto> Itens { get; set; } = new();
}

public sealed record AlocacaoLotePedidoRespostaDto(
    int ItemPedidoId,
    int ProdutoId,
    int LoteEstoqueId,
    string CodigoLote,
    DateOnly DataValidade,
    int Quantidade,
    int MovimentacaoEstoqueId);

public sealed record CortePedidoRespostaDto(
    int PedidoId,
    int Status,
    string StatusNome,
    int TotalSolicitado,
    int TotalAtendido,
    decimal ValorTotalAtendido,
    IReadOnlyList<AlocacaoLotePedidoRespostaDto> Alocacoes);
