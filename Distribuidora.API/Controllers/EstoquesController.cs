using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/produtos/{produtoId:int}/estoque")]
public sealed class EstoquesController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;
    public EstoquesController(DistribuidoraDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Consultar(int produtoId, CancellationToken ct)
    {
        var produto = await _context.Produtos.AsNoTracking()
            .Where(p => p.Id == produtoId)
            .Select(p => new { p.Nome, p.Ativo })
            .FirstOrDefaultAsync(ct);
        if (produto is null) return NotFound(new { mensagem = "Produto nao encontrado." });

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var lotes = await _context.LotesEstoque.AsNoTracking()
            .Where(l => l.ProdutoId == produtoId)
            .Select(l => new { l.DataValidade, l.QuantidadeDisponivel })
            .ToListAsync(ct);

        var fisico = lotes.Sum(l => (long)l.QuantidadeDisponivel);
        var vencido = lotes.Where(l => l.DataValidade < hoje)
            .Sum(l => (long)l.QuantidadeDisponivel);
        var valido = fisico - vencido;

        return Ok(new ResumoEstoqueDto(
            produtoId, produto.Nome, fisico, valido, vencido,
            produto.Ativo
                ? "Saldo informativo. Ainda nao ha baixa, reservas ou FEFO automáticos."
                : "Produto desativado. Saldo informativo, nao disponivel para novas vendas."));
    }
}
