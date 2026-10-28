namespace Distribuidora.API.DTOs;

public sealed record ItemVendaRespostaDto(
    int ItemPedidoId, int ProdutoId, string ProdutoNome,
    int Quantidade, decimal PrecoUnitario, decimal Subtotal);

public sealed record VendaRespostaDto(
    int Id, int PedidoId, int ClienteId, string ClienteNome,
    int VendedorId, int StatusPedido, string NomeStatusPedido,
    DateTime DataEmissaoUtc, decimal ValorTotal,
    IReadOnlyList<ItemVendaRespostaDto> Itens);

public sealed record VendaResumoDto(
    int Id, int PedidoId, int ClienteId, string ClienteNome,
    DateTime DataEmissaoUtc, decimal ValorTotal);
