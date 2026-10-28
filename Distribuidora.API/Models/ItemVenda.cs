namespace Distribuidora.API.Models;

// A quantidade e o preco sao fotografados ao gerar a venda.
public sealed class ItemVenda
{
    public int Id { get; set; }
    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;
    public int ItemPedidoId { get; set; }
    public ItemPedido ItemPedido { get; set; } = null!;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
}
