namespace Distribuidora.API.Models;

public class GrupoProduto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public ICollection<Produto> Produtos { get; set; } = new List<Produto>();
}
