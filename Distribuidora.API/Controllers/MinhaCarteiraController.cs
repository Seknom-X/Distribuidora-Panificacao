using System.Security.Claims;
using Distribuidora.API.Data;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/me")]
[Authorize(Roles = PerfisSistema.Vendedor)]
public sealed class MinhaCarteiraController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;
    public MinhaCarteiraController(DistribuidoraDbContext banco) => _banco = banco;

    private int MeuVendedorId() =>
        int.Parse(User.FindFirstValue("vendedorId")!);

    [HttpGet("clientes")]
    public async Task<IActionResult> Clientes(CancellationToken ct)
    {
        var vendedorId = MeuVendedorId();
        var clientes = await _banco.Clientes.AsNoTracking()
            .Where(c => c.Ativo && c.VendedorId == vendedorId)
            .OrderBy(c => c.OrdemVisita == 0).ThenBy(c => c.OrdemVisita)
            .ThenBy(c => c.RazaoSocial)
            .Select(c => new { c.Id, c.RazaoSocial, c.Cidade,
                c.Estado, c.Endereco, c.OrdemVisita })
            .ToListAsync(ct);
        return Ok(clientes);
    }

    [HttpGet("clientes/{id:int}")]
    public async Task<IActionResult> Cliente(int id, CancellationToken ct)
    {
        var vendedorId = MeuVendedorId();
        var cliente = await _banco.Clientes.AsNoTracking()
            .Where(c => c.Id == id && c.Ativo && c.VendedorId == vendedorId)
            .Select(c => new { c.Id, c.RazaoSocial, c.Cidade,
                c.Estado, c.Endereco, c.OrdemVisita })
            .FirstOrDefaultAsync(ct);
        return cliente is null ? NotFound() : Ok(cliente);
    }

    [HttpGet("pedidos")]
    public async Task<IActionResult> Pedidos(CancellationToken ct)
    {
        var vendedorId = MeuVendedorId();
        var pedidos = await _banco.Pedidos.AsNoTracking()
            .Where(p => p.VendedorId == vendedorId)
            .OrderByDescending(p => p.DataCriacao).ThenByDescending(p => p.Id)
            .Take(100)
            .Select(p => new { p.Id, p.ClienteId, p.DataCriacao,
                status = (int)p.Status,
                quantidadeSolicitada = p.Itens.Sum(i => i.QuantidadeSolicitada),
                quantidadeAtendida = p.Itens.Sum(i => i.QuantidadeAtendida) })
            .ToListAsync(ct);
        return Ok(pedidos);
    }

    [HttpGet("produtos")]
    public async Task<IActionResult> Produtos(CancellationToken ct)
    {
        var produtos = await _banco.Produtos.AsNoTracking()
            .Where(p => p.Ativo)
            .OrderBy(p => p.Nome)
            .Select(p => new { p.Id, p.Nome, p.UnidadeMedida, p.PrecoVenda })
            .ToListAsync(ct);
        return Ok(produtos);
    }
}
