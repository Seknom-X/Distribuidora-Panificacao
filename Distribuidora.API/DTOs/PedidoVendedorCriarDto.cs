using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

// O vendedor autenticado e identificado exclusivamente pelas claims do JWT.
// Este DTO nao aceita VendedorId enviado pelo cliente.
public sealed class PedidoVendedorCriarDto
{
    [Required, StringLength(36, MinimumLength = 36)]
    public string ChaveRequisicao { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int ClienteId { get; set; }

    [Required, MinLength(1), MaxLength(100)]
    public List<ItemPedidoCriarDto> Itens { get; set; } = new();
}
