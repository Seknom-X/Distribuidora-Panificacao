using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class ItemPedidoCriarDto
{
    [Range(1, int.MaxValue)]
    public int ProdutoId { get; set; }

    [Range(1, 1000000)]
    public int QuantidadeSolicitada { get; set; }
}

public sealed class PedidoCriarDto
{
    [Required, StringLength(36, MinimumLength = 36)]
    public string ChaveRequisicao { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ClienteId { get; set; }

    [Range(1, int.MaxValue)]
    public int VendedorId { get; set; }

    [Required, MinLength(1), MaxLength(100)]
    public List<ItemPedidoCriarDto> Itens { get; set; } = new();
}

public sealed record ItemPedidoRespostaDto(
    int ProdutoId,
    string ProdutoNome,
    int QuantidadeSolicitada,
    int QuantidadeAtendida,
    decimal PrecoUnitario,
    decimal SubtotalSolicitado);

public sealed record PedidoRespostaDto(
    int Id,
    string? ChaveRequisicao,
    int ClienteId,
    string ClienteNome,
    int VendedorId,
    string VendedorNome,
    int Status,
    string StatusNome,
    DateTime DataCriacao,
    decimal ValorTotalSolicitado,
    IReadOnlyList<ItemPedidoRespostaDto> Itens);

public sealed record PedidoResumoDto(
    int Id,
    int ClienteId,
    string ClienteNome,
    int VendedorId,
    string VendedorNome,
    int Status,
    string StatusNome,
    DateTime DataCriacao,
    decimal ValorTotalSolicitado);
