namespace Distribuidora.API.Models;

public sealed class AlocacaoLotePedido
{
    public int Id { get; set; }
    public int ItemPedidoId { get; set; }
    public ItemPedido ItemPedido { get; set; } = null!;
    public int LoteEstoqueId { get; set; }
    public LoteEstoque LoteEstoque { get; set; } = null!;
    public int MovimentacaoEstoqueId { get; set; }
    public MovimentacaoEstoque MovimentacaoEstoque { get; set; } = null!;
    public int Quantidade { get; set; }
}
