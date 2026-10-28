namespace Distribuidora.API.Models;

public sealed class ContaReceber
{
    public int Id { get; set; }
    public int VendaId { get; set; }
    public Venda Venda { get; set; } = null!;
    public int CondicaoPagamentoId { get; set; }
    public CondicaoPagamento CondicaoPagamento { get; set; } = null!;
    public DateTime DataCriacaoUtc { get; set; } = DateTime.UtcNow;
    public decimal ValorOriginal { get; set; }
    public ICollection<ParcelaReceber> Parcelas { get; set; } = new List<ParcelaReceber>();
}
