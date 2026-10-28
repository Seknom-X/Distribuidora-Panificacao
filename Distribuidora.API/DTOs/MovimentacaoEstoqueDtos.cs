using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class MovimentacaoEstoqueCriarDto
{
    [Range(1, 2)]
    public int Tipo { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantidade { get; set; }

    [Required, StringLength(36, MinimumLength = 36)]
    public string ChaveOperacao { get; set; } = string.Empty;

    [StringLength(300)]
    public string Observacao { get; set; } = string.Empty;
}

public sealed record MovimentacaoEstoqueRespostaDto(
    int Id, int LoteEstoqueId, int Tipo, int Quantidade,
    int SaldoAposMovimentacao, string ChaveOperacao,
    string Observacao, DateTime DataUtc);
