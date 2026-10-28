namespace Distribuidora.API.Models;

public class Produto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string UnidadeMedida { get; set; } = "UN";
    public decimal PrecoVenda { get; set; }
    public bool Ativo { get; set; } = true;

    // Opcional no BANCO para preservar produtos antigos na migration.
    // Obrigatorio para novos produtos via DTO/Controller.
    public int? GrupoProdutoId { get; set; }
    public GrupoProduto? GrupoProduto { get; set; }
    public ICollection<LoteEstoque> Lotes { get; set; } = new List<LoteEstoque>();
}
