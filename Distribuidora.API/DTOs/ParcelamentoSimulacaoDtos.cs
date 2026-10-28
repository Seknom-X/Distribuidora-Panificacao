namespace Distribuidora.API.DTOs;

public sealed record ParcelaSimuladaDto(
    int Numero, int DiasAposVenda, decimal Percentual,
    DateOnly Vencimento, decimal Valor);

public sealed record SimulacaoParcelamentoRespostaDto(
    int VendaId, int PedidoId, int CondicaoPagamentoId, string CondicaoPagamentoNome,
    DateOnly DataBase, decimal ValorVenda, decimal TotalParcelado,
    IReadOnlyList<ParcelaSimuladaDto> Parcelas);
