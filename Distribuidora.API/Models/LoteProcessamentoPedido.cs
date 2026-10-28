namespace Distribuidora.API.Models;

public sealed class LoteProcessamentoPedido
{
    public int Id { get; set; }
    public int LoteProcessamentoId { get; set; }
    public LoteProcessamento LoteProcessamento { get; set; } = null!;
    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
}
