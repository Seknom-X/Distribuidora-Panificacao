namespace Distribuidora.API.Models;

public sealed class Recebimento
{
    public int Id { get; set; }
    public int ParcelaReceberId { get; set; }
    public ParcelaReceber ParcelaReceber { get; set; } = null!;
    public string ChaveOperacao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string FormaPagamento { get; set; } = string.Empty;
    public string Observacao { get; set; } = string.Empty;
    public DateTime DataUtc { get; set; } = DateTime.UtcNow;
}
