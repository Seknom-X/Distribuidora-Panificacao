namespace Distribuidora.API.Models;

public sealed class CondicaoPagamento
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; } = true;
    public ICollection<ParcelaCondicao> Parcelas { get; set; } = new List<ParcelaCondicao>();
}
