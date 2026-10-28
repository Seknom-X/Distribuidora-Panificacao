namespace Distribuidora.API.Models;

public sealed class ParcelaCondicao
{
    public int Id { get; set; }
    public int CondicaoPagamentoId { get; set; }
    public CondicaoPagamento CondicaoPagamento { get; set; } = null!;
    public int Numero { get; set; }
    public int DiasAposVenda { get; set; }
    public decimal Percentual { get; set; }
}
