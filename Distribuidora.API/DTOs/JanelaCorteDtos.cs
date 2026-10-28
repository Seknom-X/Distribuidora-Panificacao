namespace Distribuidora.API.DTOs;

public sealed record ProdutoJanelaCorteDto(
    int ProdutoId,
    string NomeProduto,
    long QuantidadeSolicitada,
    long EstoqueAtualValidoNaDataDoCorte,
    long FaltaEstimada);

public sealed record PedidoJanelaCorteDto(
    int PedidoId,
    int ClienteId,
    string Cliente,
    int VendedorId,
    DateTime DataCriacaoUtc,
    long UnidadesSolicitadas,
    decimal ValorSolicitado);

public sealed record JanelaCorteDto(
    DateOnly DataCorte,
    int HoraCorte,
    string FusoHorario,
    DateTime InicioUtc,
    DateTime FimUtc,
    bool HorarioDeCorteJaPassou,
    int PedidosPendentes,
    long UnidadesSolicitadas,
    decimal ValorSolicitado,
    IReadOnlyList<ProdutoJanelaCorteDto> Produtos,
    IReadOnlyList<PedidoJanelaCorteDto> Pedidos);
