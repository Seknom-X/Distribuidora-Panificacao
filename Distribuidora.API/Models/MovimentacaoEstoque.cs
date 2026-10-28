namespace Distribuidora.API.Models;

public class MovimentacaoEstoque
{
    public int Id { get; set; }
    public int LoteEstoqueId { get; set; }
    public LoteEstoque LoteEstoque { get; set; } = null!;
    public TipoMovimentacaoEstoque Tipo { get; set; }
    public int Quantidade { get; set; }
    public int SaldoAposMovimentacao { get; set; }
    public string ChaveOperacao { get; set; } = string.Empty;
    public string Observacao { get; set; } = string.Empty;
    public DateTime DataUtc { get; set; } = DateTime.UtcNow;
}
