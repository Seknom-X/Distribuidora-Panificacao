using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class GrupoProdutoSalvarDto
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [StringLength(300)]
    public string Descricao { get; set; } = string.Empty;
}

public sealed record GrupoProdutoRespostaDto(
    int Id, string Nome, string Descricao, bool Ativo, int QuantidadeProdutos);
