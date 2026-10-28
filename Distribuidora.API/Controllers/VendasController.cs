using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Distribuidora.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/vendas")]
[Authorize(Roles = PerfisSistema.Admin)]
public sealed class VendasController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;

    public VendasController(DistribuidoraDbContext banco) => _banco = banco;

    private async Task<Venda?> CarregarVenda(int? id, int? pedidoId, CancellationToken ct)
    {
        var consulta = _banco.Vendas.AsNoTracking().AsQueryable();
        if (id.HasValue) consulta = consulta.Where(v => v.Id == id.Value);
        if (pedidoId.HasValue) consulta = consulta.Where(v => v.PedidoId == pedidoId.Value);
        return await consulta
            .Include(v => v.Pedido).ThenInclude(p => p.Cliente)
            .Include(v => v.Itens).ThenInclude(i => i.ItemPedido).ThenInclude(i => i.Produto)
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
    }

    private static VendaRespostaDto ParaResposta(Venda venda)
    {
        var p = venda.Pedido;
        return new VendaRespostaDto(
            venda.Id, venda.PedidoId, p.ClienteId, p.Cliente.RazaoSocial,
            p.VendedorId, (int)p.Status, p.Status.ToString(),
            venda.DataEmissaoUtc, venda.ValorTotal,
            venda.Itens.OrderBy(i => i.Id)
                .Select(i => new ItemVendaRespostaDto(
                    i.ItemPedidoId,
                    i.ItemPedido.ProdutoId,
                    i.ItemPedido.Produto.Nome,
                    i.Quantidade,
                    i.PrecoUnitario,
                    i.Quantidade * i.PrecoUnitario))
                .ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(int id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        var venda = await CarregarVenda(id, null, ct);
        return venda is null ? NotFound(new { mensagem = "Venda nao encontrada." }) : Ok(ParaResposta(venda));
    }

    [HttpGet("por-pedido/{pedidoId:int}")]
    public async Task<IActionResult> BuscarPorPedido(int pedidoId, CancellationToken ct)
    {
        if (pedidoId <= 0) return NotFound();
        var venda = await CarregarVenda(null, pedidoId, ct);
        return venda is null ? NotFound(new { mensagem = "Venda nao encontrada para o pedido." }) : Ok(ParaResposta(venda));
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? clienteId, CancellationToken ct)
    {
        if (clienteId.HasValue && clienteId <= 0)
            return BadRequest(new { mensagem = "ClienteId invalido." });

        var query = _banco.Vendas.AsNoTracking().AsQueryable();
        if (clienteId.HasValue) query = query.Where(v => v.Pedido.ClienteId == clienteId.Value);
        var vendas = await query.OrderByDescending(v => v.DataEmissaoUtc)
            .ThenByDescending(v => v.Id).Take(100)
            .Select(v => new VendaResumoDto(
                v.Id, v.PedidoId, v.Pedido.ClienteId,
                v.Pedido.Cliente.RazaoSocial, v.DataEmissaoUtc, v.ValorTotal))
            .ToListAsync(ct);
        return Ok(vendas);
    }

    // Somente emite o registro comercial. Nao grava movimentacao de estoque.
    [HttpPost("do-pedido/{pedidoId:int}")]
    public async Task<IActionResult> GerarDoPedido(int pedidoId, CancellationToken ct)
    {
        if (pedidoId <= 0) return BadRequest(new { mensagem = "PedidoId invalido." });

        // Reenvio: devolve a venda existente sem duplicar o financeiro.
        var existente = await CarregarVenda(null, pedidoId, ct);
        if (existente is not null) return Ok(ParaResposta(existente));

        var pedido = await _banco.Pedidos.AsNoTracking()
            .Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, ct);
        if (pedido is null)
            return NotFound(new { mensagem = "Pedido nao encontrado." });
        if (pedido.Status != StatusPedido.Atendido &&
            pedido.Status != StatusPedido.AtendidoParcialmente)
            return Conflict(new { mensagem = "Venda exige pedido atendido ou atendido parcialmente." });

        var itens = pedido.Itens.OrderBy(i => i.Id).ToList();
        if (!VendaRegras.QuantidadesValidas(
            itens.Select(i => (i.QuantidadeSolicitada, i.QuantidadeAtendida))) ||
            itens.Any(i => i.PrecoUnitario <= 0))
            return Conflict(new { mensagem = "Pedido nao tem quantidades/precos atendidos validos." });

        // Exige rastreabilidade: toda unidade vendida ja foi realmente baixada em lotes.
        var idsItens = itens.Select(i => i.Id).ToList();
        var baixas = await _banco.AlocacoesLotePedido.AsNoTracking()
            .Where(a => idsItens.Contains(a.ItemPedidoId))
            .GroupBy(a => a.ItemPedidoId)
            .Select(g => new { ItemPedidoId = g.Key, Quantidade = g.Sum(a => a.Quantidade) })
            .ToListAsync(ct);
        var quantidadeBaixadaPorItem = baixas.ToDictionary(a => a.ItemPedidoId, a => a.Quantidade);
        if (itens.Any(i => quantidadeBaixadaPorItem.GetValueOrDefault(i.Id) != i.QuantidadeAtendida))
            return Conflict(new { mensagem = "As quantidades atendidas nao correspondem as baixas por lote." });

        var selecionados = itens.Where(i => i.QuantidadeAtendida > 0).ToList();
        decimal valor;
        try
        {
            valor = VendaRegras.TotalAtendido(
                selecionados.Select(i => (i.QuantidadeAtendida, i.PrecoUnitario)));
        }
        catch (OverflowException)
        {
            return Conflict(new { mensagem = "O valor da venda ultrapassa o limite decimal." });
        }
        if (valor <= 0)
            return Conflict(new { mensagem = "O valor atendido precisa ser positivo." });

        var venda = new Venda
        {
            PedidoId = pedido.Id,
            DataEmissaoUtc = DateTime.UtcNow,
            ValorTotal = valor,
            Itens = selecionados.Select(i => new ItemVenda
            {
                ItemPedidoId = i.Id,
                Quantidade = i.QuantidadeAtendida,
                PrecoUnitario = i.PrecoUnitario
            }).ToList()
        };

        _banco.Vendas.Add(venda);
        try
        {
            // O EF usa transacao unica na gravacao de Venda + ItensVenda.
            await _banco.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            // Em corrida entre dois requests, o indice UNIQUE(PedidoId) e o guardiao.
            _banco.ChangeTracker.Clear();
            var concorrente = await CarregarVenda(null, pedidoId, ct);
            if (concorrente is not null) return Ok(ParaResposta(concorrente));
            return Conflict(new { mensagem = "Operacao duplicada; consulte novamente a venda." });
        }

        // Carregar novamente para incluir os nomes dos produtos e do cliente.
        _banco.ChangeTracker.Clear();
        var gravada = await CarregarVenda(venda.Id, null, ct);
        if (gravada is null)
            return StatusCode(500, new { mensagem = "Venda gravada mas nao encontrada para leitura." });
        return CreatedAtAction(nameof(BuscarPorId), new { id = gravada.Id }, ParaResposta(gravada));
    }
}
