using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class ProdutoSalvarDto
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required, StringLength(10, MinimumLength = 1)]
    public string UnidadeMedida { get; set; } = "UN";

    public decimal PrecoVenda { get; set; }

    [Range(1, int.MaxValue)]
    public int GrupoProdutoId { get; set; }
}

public sealed record ProdutoRespostaDto(
    int Id, string Nome, string UnidadeMedida, decimal PrecoVenda,
    bool Ativo, int? GrupoProdutoId, string? GrupoProdutoNome);
