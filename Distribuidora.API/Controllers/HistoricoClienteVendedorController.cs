using System.Security.Claims;
using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/me/clientes")]
[Authorize(Roles = PerfisSistema.Vendedor)]
public sealed class HistoricoClienteVendedorController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;

    public HistoricoClienteVendedorController(DistribuidoraDbContext banco) => _banco = banco;

    [HttpGet("{clienteId:int}/historico")]
    public async Task<IActionResult> Historico(int clienteId, CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirstValue("vendedorId"), out var vendedorId) || vendedorId <= 0)
            return Unauthorized();
        if (clienteId <= 0) return NotFound();

        // Conferir o vinculo ANTES de consultar pedidos ou valores financeiros.
        var cliente = await _banco.Clientes.AsNoTracking()
            .Where(c => c.Id == clienteId && c.Ativo && c.VendedorId == vendedorId)
            .Select(c => new { c.Id, c.RazaoSocial, c.VendedorId })
            .FirstOrDefaultAsync(ct);
        if (cliente is null) return NotFound(new { mensagem = "Cliente nao encontrado na sua carteira." });

        var totalPedidos = await _banco.Pedidos.AsNoTracking()
            .CountAsync(p => p.ClienteId == clienteId, ct);
        var pedidos = await _banco.Pedidos.AsNoTracking()
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.DataCriacao).ThenByDescending(p => p.Id)
            .Take(20)
            .Select(p => new PedidoRecenteDto(
                p.Id, p.DataCriacao, (int)p.Status,
                p.Itens.Sum(i => i.QuantidadeSolicitada),
                p.Itens.Sum(i => i.QuantidadeAtendida)))
            .ToListAsync(ct);

        var totalVendas = await _banco.Vendas.AsNoTracking()
            .CountAsync(v => v.Pedido.ClienteId == clienteId, ct);
        var compras = await _banco.Vendas.AsNoTracking()
            .Where(v => v.Pedido.ClienteId == clienteId)
            .OrderByDescending(v => v.DataEmissaoUtc).ThenByDescending(v => v.Id)
            .Take(20)
            .Select(v => new CompraRecenteDto(
                v.Id, v.PedidoId, v.DataEmissaoUtc, v.ValorTotal))
            .ToListAsync(ct);

        var hojeUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var totais = await _banco.ParcelasReceber.AsNoTracking()
            .Where(p => p.ContaReceber.Venda.Pedido.ClienteId == clienteId)
            .GroupBy(p => p.ContaReceber.Venda.Pedido.ClienteId)
            .Select(g => new
            {
                Parcelas = g.Count(),
                Original = g.Sum(p => p.ValorOriginal),
                Pago = g.Sum(p => p.ValorPago),
                Vencido = g.Sum(p => p.Vencimento < hojeUtc
                    ? p.ValorOriginal - p.ValorPago : 0m)
            })
            .FirstOrDefaultAsync(ct);

        var resposta = new HistoricoClienteDto(
            cliente.Id, cliente.RazaoSocial, cliente.VendedorId,
            totalPedidos, pedidos, totalVendas, compras,
            totais?.Parcelas ?? 0, totais?.Original ?? 0m,
            totais?.Pago ?? 0m,
            (totais?.Original ?? 0m) - (totais?.Pago ?? 0m),
            totais?.Vencido ?? 0m);
        return Ok(resposta);
    }
}
