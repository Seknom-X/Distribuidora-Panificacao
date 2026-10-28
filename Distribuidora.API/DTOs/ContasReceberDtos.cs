using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class GerarContaReceberDto
{
    [Range(1, int.MaxValue)]
    public int CondicaoPagamentoId { get; set; }
}

public sealed class RegistrarRecebimentoDto
{
    public Guid ChaveOperacao { get; set; }
    public decimal Valor { get; set; }
    [Required, MaxLength(40)]
    public string FormaPagamento { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? Observacao { get; set; }
}

public sealed record ParcelaReceberRespostaDto(
    int Id, int Numero, DateOnly Vencimento, decimal ValorOriginal,
    decimal ValorPago, decimal SaldoDevedor, string Situacao);

public sealed record ContaReceberRespostaDto(
    int Id, int VendaId, int ClienteId, string ClienteNome,
    int CondicaoPagamentoId, string CondicaoPagamentoNome,
    DateTime DataCriacaoUtc, decimal ValorOriginal, decimal TotalPago,
    decimal SaldoDevedor, IReadOnlyList<ParcelaReceberRespostaDto> Parcelas);

public sealed record RecebimentoRespostaDto(
    int Id, int ParcelaReceberId, int ContaReceberId, string ChaveOperacao,
    decimal Valor, string FormaPagamento, string Observacao, DateTime DataUtc);
