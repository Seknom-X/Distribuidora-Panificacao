namespace Distribuidora.API.Models;

public sealed class ParcelaReceber
{
    public int Id { get; set; }
    public int ContaReceberId { get; set; }
    public ContaReceber ContaReceber { get; set; } = null!;
    public int Numero { get; set; }
    public DateOnly Vencimento { get; set; }
    public decimal ValorOriginal { get; set; }
    public decimal ValorPago { get; set; }
    public ICollection<Recebimento> Recebimentos { get; set; } = new List<Recebimento>();
}
