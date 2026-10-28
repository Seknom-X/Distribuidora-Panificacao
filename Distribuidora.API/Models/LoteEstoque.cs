namespace Distribuidora.API.Models;

public class LoteEstoque
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public Produto Produto { get; set; } = null!;
    public string CodigoLote { get; set; } = string.Empty;
    public DateOnly DataValidade { get; set; }
    public DateTime DataEntradaUtc { get; set; } = DateTime.UtcNow;
    public int QuantidadeDisponivel { get; set; }
}
