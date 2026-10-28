using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class CriarCondicaoPagamentoDto
{
    [Required, MinLength(3), MaxLength(120)]
    public string Nome { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(12)]
    public List<CriarParcelaCondicaoDto> Parcelas { get; set; } = new();
}

public sealed class CriarParcelaCondicaoDto
{
    [Range(0, 365)]
    public int DiasAposVenda { get; set; }

    public decimal Percentual { get; set; }
}

public sealed record ParcelaCondicaoRespostaDto(int Numero, int DiasAposVenda, decimal Percentual);
public sealed record CondicaoPagamentoRespostaDto(
    int Id, string Nome, bool Ativa, IReadOnlyList<ParcelaCondicaoRespostaDto> Parcelas);
