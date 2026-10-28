namespace Distribuidora.API.Models;

// Venda comercial gerada somente do atendimento efetivo de um pedido.
public sealed class Venda
{
    public int Id { get; set; }
    public int PedidoId { get; set; }
    public Pedido Pedido { get; set; } = null!;
    public DateTime DataEmissaoUtc { get; set; } = DateTime.UtcNow;
    public decimal ValorTotal { get; set; }
    public ICollection<ItemVenda> Itens { get; set; } = new List<ItemVenda>();
}
