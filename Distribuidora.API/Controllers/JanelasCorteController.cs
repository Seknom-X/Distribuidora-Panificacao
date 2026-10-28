using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Distribuidora.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/janelas-corte")]
public sealed class JanelasCorteController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public JanelasCorteController(DistribuidoraDbContext context) => _context = context;

    [HttpGet("atual")]
    public async Task<IActionResult> Proxima(CancellationToken ct)
    {
        var (data, hora) = JanelaCorteService.ProximaJanela(DateTime.UtcNow);
        return await GerarPrevia(data, hora, ct);
    }

    [HttpGet]
    public async Task<IActionResult> Consultar(
        [FromQuery] DateOnly? data,
        [FromQuery] int? hora,
        CancellationToken ct)
    {
        if (!data.HasValue || data.Value.Year is < 2000 or > 2100 ||
            hora is not (12 or 19))
            return BadRequest(new { mensagem = "Informe data=AAAA-MM-DD e hora=12 ou 19." });

        return await GerarPrevia(data.Value, hora.Value, ct);
    }

    private async Task<IActionResult> GerarPrevia(DateOnly data, int hora, CancellationToken ct)
    {
        var intervalo = JanelaCorteService.Criar(data, hora);

        // Somente pendentes e somente pedidos criados no intervalo da janela.
        // O limite e explicito: nao gerar um resumo incompleto silenciosamente.
        var pedidos = await _context.Pedidos.AsNoTracking()
            .Where(p => p.Status == StatusPedido.Pendente &&
                p.DataCriacao > intervalo.InicioUtc &&
                p.DataCriacao <= intervalo.FimUtc)
            .OrderBy(p => p.DataCriacao).ThenBy(p => p.Id)
            .Take(1001)
            .Include(p => p.Cliente)
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .AsSplitQuery()
            .ToListAsync(ct);

        if (pedidos.Count > 1000)
            return Conflict(new { mensagem = "Mais de 1000 pedidos na janela; implementar paginacao antes de processar." });

        var linhas = pedidos.SelectMany(p => p.Itens).ToList();
        var grupos = linhas.GroupBy(i => new { i.ProdutoId, i.Produto.Nome })
            .Select(g => new
            {
                g.Key.ProdutoId,
                NomeProduto = g.Key.Nome,
                QuantidadeSolicitada = g.Sum(i => (long)i.QuantidadeSolicitada)
            })
            .OrderBy(g => g.NomeProduto).ThenBy(g => g.ProdutoId)
            .ToList();

        var ids = grupos.Select(g => g.ProdutoId).ToArray();
        var hojeLocal = JanelaCorteService.DataLocal(DateTime.UtcNow);

        // Estoque e o saldo ATUAL, filtrado pelos lotes validos NA DATA DO CORTE.
        // Nao e inventario historico nem reserva; pode mudar apos esta consulta.
        var saldos = ids.Length == 0
            ? new Dictionary<int, long>()
            : await _context.LotesEstoque.AsNoTracking()
                .Where(l => ids.Contains(l.ProdutoId) &&
                    l.Produto.Ativo &&
                    l.DataValidade >= (data > hojeLocal ? data : hojeLocal) &&
                    l.QuantidadeDisponivel > 0)
                .GroupBy(l => l.ProdutoId)
                .Select(g => new { ProdutoId = g.Key, Quantidade = g.Sum(l => (long)l.QuantidadeDisponivel) })
                .ToDictionaryAsync(g => g.ProdutoId, g => g.Quantidade, ct);

        var produtos = grupos.Select(g =>
        {
            var estoque = saldos.GetValueOrDefault(g.ProdutoId);
            return new ProdutoJanelaCorteDto(g.ProdutoId, g.NomeProduto,
                g.QuantidadeSolicitada, estoque,
                Math.Max(0L, g.QuantidadeSolicitada - estoque));
        }).ToList();

        var pedidosDto = pedidos.Select(p => new PedidoJanelaCorteDto(
            p.Id, p.ClienteId, p.Cliente.RazaoSocial, p.VendedorId,
            p.DataCriacao, p.Itens.Sum(i => (long)i.QuantidadeSolicitada),
            p.Itens.Sum(i => i.PrecoUnitario * i.QuantidadeSolicitada)))
            .ToList();

        return Ok(new JanelaCorteDto(
            data,
            hora,
            JanelaCorteService.FusoHorario,
            intervalo.InicioUtc,
            intervalo.FimUtc,
            DateTime.UtcNow >= intervalo.FimUtc,
            pedidos.Count,
            pedidosDto.Sum(p => p.UnidadesSolicitadas),
            pedidosDto.Sum(p => p.ValorSolicitado),
            produtos,
            pedidosDto));
    }
}
