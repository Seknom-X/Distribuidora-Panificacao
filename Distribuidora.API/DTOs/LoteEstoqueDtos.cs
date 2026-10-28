using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class LoteEstoqueCriarDto
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string CodigoLote { get; set; } = string.Empty;

    public DateOnly DataValidade { get; set; }

    [Range(1, int.MaxValue)]
    public int QuantidadeDisponivel { get; set; }
}

public sealed record LoteEstoqueRespostaDto(
    int Id, int ProdutoId, string CodigoLote, DateOnly DataValidade,
    DateTime DataEntradaUtc, int QuantidadeDisponivel, bool Vencido);

public sealed record ResumoEstoqueDto(
    int ProdutoId, string ProdutoNome, long EstoqueFisico,
    long EstoqueValido, long EstoqueVencido,
    string Observacao);
