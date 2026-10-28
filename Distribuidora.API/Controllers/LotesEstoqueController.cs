using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

// EXERCICIO LOCAL: proteger com JWT / papel Admin antes de publicar em rede.
[ApiController]
[Route("api/produtos/{produtoId:int}/lotes")]
public sealed class LotesEstoqueController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;
    public LotesEstoqueController(DistribuidoraDbContext context) => _context = context;

    private static DateOnly HojeUtc() => DateOnly.FromDateTime(DateTime.UtcNow);

    [HttpGet]
    public async Task<IActionResult> Listar(int produtoId, CancellationToken ct)
    {
        if (!await _context.Produtos.AnyAsync(p => p.Id == produtoId, ct))
            return NotFound(new { mensagem = "Produto nao encontrado." });
        var hoje = HojeUtc();
        var lotes = await _context.LotesEstoque.AsNoTracking()
            .Where(l => l.ProdutoId == produtoId)
            .OrderBy(l => l.DataValidade).ThenBy(l => l.Id)
            .Select(l => new LoteEstoqueRespostaDto(
                l.Id, l.ProdutoId, l.CodigoLote, l.DataValidade,
                l.DataEntradaUtc, l.QuantidadeDisponivel, l.DataValidade < hoje))
            .ToListAsync(ct);
        return Ok(lotes);
    }

    [HttpGet("{loteId:int}")]
    public async Task<IActionResult> BuscarPorId(int produtoId, int loteId, CancellationToken ct)
    {
        var hoje = HojeUtc();
        var lote = await _context.LotesEstoque.AsNoTracking()
            .Where(l => l.Id == loteId && l.ProdutoId == produtoId)
            .Select(l => new LoteEstoqueRespostaDto(
                l.Id, l.ProdutoId, l.CodigoLote, l.DataValidade,
                l.DataEntradaUtc, l.QuantidadeDisponivel, l.DataValidade < hoje))
            .FirstOrDefaultAsync(ct);
        return lote is null ? NotFound(new { mensagem = "Lote nao encontrado." }) : Ok(lote);
    }

    [HttpPost]
    public async Task<IActionResult> Criar(
        int produtoId, [FromBody] LoteEstoqueCriarDto dados, CancellationToken ct)
    {
        var codigo = dados.CodigoLote.Trim();
        if (codigo.Length == 0)
            return BadRequest(new { mensagem = "Informe o codigo do lote." });
        if (dados.DataValidade < HojeUtc())
            return BadRequest(new { mensagem = "Nao e permitido cadastrar lote ja vencido." });

        var produto = await _context.Produtos.AsNoTracking()
            .Where(p => p.Id == produtoId)
            .Select(p => new { p.Id, p.Ativo })
            .FirstOrDefaultAsync(ct);
        if (produto is null) return NotFound(new { mensagem = "Produto nao encontrado." });
        if (!produto.Ativo) return Conflict(new { mensagem = "Produto inativo nao recebe novos lotes." });

        if (await _context.LotesEstoque.AnyAsync(
            l => l.ProdutoId == produtoId && l.CodigoLote == codigo, ct))
            return Conflict(new { mensagem = "Codigo de lote ja cadastrado para este produto." });

        var lote = new LoteEstoque
        {
            ProdutoId = produtoId,
            CodigoLote = codigo,
            DataValidade = dados.DataValidade,
            DataEntradaUtc = DateTime.UtcNow,
            QuantidadeDisponivel = dados.QuantidadeDisponivel
        };
        _context.LotesEstoque.Add(lote);
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (EhDuplicado(ex))
        { return Conflict(new { mensagem = "Codigo de lote ja cadastrado para este produto." }); }

        return CreatedAtAction(nameof(BuscarPorId),
            new { produtoId, loteId = lote.Id },
            new LoteEstoqueRespostaDto(lote.Id, produtoId, lote.CodigoLote,
                lote.DataValidade, lote.DataEntradaUtc, lote.QuantidadeDisponivel, false));
    }

    private static bool EhDuplicado(DbUpdateException ex) =>
        ex.InnerException is MySqlException { Number: 1062 };
}
