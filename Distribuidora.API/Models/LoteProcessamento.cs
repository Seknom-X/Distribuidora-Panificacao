namespace Distribuidora.API.Models;

// Um lote e a fotografia imutavel dos IDs de pedidos de uma janela ja encerrada.
public sealed class LoteProcessamento
{
    public int Id { get; set; }
    public DateOnly DataCorte { get; set; }
    public int HoraCorte { get; set; }
    public DateTime InicioUtc { get; set; }
    public DateTime FimUtc { get; set; }
    public DateTime DataFechamentoUtc { get; set; }
    public DateTime? DataProcessamentoUtc { get; set; }
    public ICollection<LoteProcessamentoPedido> VinculosPedidos { get; set; } = new List<LoteProcessamentoPedido>();
}
