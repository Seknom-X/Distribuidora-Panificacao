namespace Distribuidora.API.DTOs;

public sealed record PedidoRecenteDto(
    int PedidoId, DateTime DataCriacaoUtc, int Status,
    int QuantidadeSolicitada, int QuantidadeAtendida);

public sealed record CompraRecenteDto(
    int VendaId, int PedidoId, DateTime DataEmissaoUtc, decimal ValorTotal);

public sealed record HistoricoClienteDto(
    int ClienteId, string RazaoSocial, int VendedorId,
    int QuantidadeTotalPedidos, IReadOnlyList<PedidoRecenteDto> PedidosRecentes,
    int QuantidadeTotalVendas, IReadOnlyList<CompraRecenteDto> ComprasRecentes,
    int QuantidadeParcelas, decimal ValorOriginalTitulos,
    decimal ValorPago, decimal SaldoDevedor, decimal SaldoVencido);

public sealed record ParcelaAuditoriaDto(
    int ParcelaId, int Numero, decimal ValorOriginal, decimal ValorPago,
    decimal SomaRecebimentos, decimal SaldoDevedor,
    IReadOnlyList<string> Alertas);

public sealed record ContaAuditoriaDto(
    int ContaId, int VendaId, decimal ValorConta, decimal ValorVenda,
    decimal SomaParcelas, decimal SomaValorPago,
    decimal SomaRecebimentos, decimal SaldoDevedor,
    bool Consistente, IReadOnlyList<string> Alertas,
    IReadOnlyList<ParcelaAuditoriaDto> Parcelas);

public sealed record VisaoGeralAuditoriaDto(
    int ContasConferidas, int ContasComAlertas,
    IReadOnlyList<ContaAuditoriaDto> Contas);
