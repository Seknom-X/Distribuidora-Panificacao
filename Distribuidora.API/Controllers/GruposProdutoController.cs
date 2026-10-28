using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

// EXERCICIO LOCAL: proteger com JWT / papel Admin antes de publicar em rede.
[ApiController]
[Route("api/grupos-produto")]
public sealed class GruposProdutoController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public GruposProdutoController(DistribuidoraDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool? ativo, CancellationToken ct)
    {
        var consulta = _context.GruposProduto.AsNoTracking().AsQueryable();
        if (ativo.HasValue) consulta = consulta.Where(g => g.Ativo == ativo.Value);
        var grupos = await consulta.OrderBy(g => g.Nome)
            .Select(g => new GrupoProdutoRespostaDto(
                g.Id, g.Nome, g.Descricao, g.Ativo, g.Produtos.Count))
            .ToListAsync(ct);
        return Ok(grupos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(int id, CancellationToken ct)
    {
        var grupo = await _context.GruposProduto.AsNoTracking()
            .Where(g => g.Id == id)
            .Select(g => new GrupoProdutoRespostaDto(
                g.Id, g.Nome, g.Descricao, g.Ativo, g.Produtos.Count))
            .FirstOrDefaultAsync(ct);
        return grupo is null ? NotFound(new { mensagem = "Grupo nao encontrado." }) : Ok(grupo);
    }

    [HttpPost]
    public async Task<IActionResult> Cadastrar([FromBody] GrupoProdutoSalvarDto dados, CancellationToken ct)
    {
        var nome = dados.Nome.Trim();
        if (nome.Length < 2)
            return BadRequest(new { mensagem = "Nome deve ter pelo menos 2 caracteres." });
        if (await _context.GruposProduto.AnyAsync(g => g.Nome == nome, ct))
            return Conflict(new { mensagem = "Ja existe grupo com este nome." });

        var grupo = new GrupoProduto
        {
            Nome = nome,
            Descricao = dados.Descricao.Trim(),
            Ativo = true
        };
        _context.GruposProduto.Add(grupo);
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (EhDuplicado(ex))
        { return Conflict(new { mensagem = "Ja existe grupo com este nome." }); }

        return CreatedAtAction(nameof(BuscarPorId), new { id = grupo.Id },
            new GrupoProdutoRespostaDto(grupo.Id, grupo.Nome, grupo.Descricao, grupo.Ativo, 0));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(int id, [FromBody] GrupoProdutoSalvarDto dados, CancellationToken ct)
    {
        var grupo = await _context.GruposProduto.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (grupo is null) return NotFound(new { mensagem = "Grupo nao encontrado." });

        var nome = dados.Nome.Trim();
        if (nome.Length < 2) return BadRequest(new { mensagem = "Nome deve ter pelo menos 2 caracteres." });
        if (await _context.GruposProduto.AnyAsync(g => g.Id != id && g.Nome == nome, ct))
            return Conflict(new { mensagem = "Ja existe grupo com este nome." });
        grupo.Nome = nome;
        grupo.Descricao = dados.Descricao.Trim();
        try { await _context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (EhDuplicado(ex))
        { return Conflict(new { mensagem = "Ja existe grupo com este nome." }); }
        var quantidade = await _context.Produtos.CountAsync(p => p.GrupoProdutoId == id, ct);
        return Ok(new GrupoProdutoRespostaDto(grupo.Id, grupo.Nome, grupo.Descricao, grupo.Ativo, quantidade));
    }

    [HttpPatch("{id:int}/desativar")]
    public async Task<IActionResult> Desativar(int id, CancellationToken ct)
    {
        var grupo = await _context.GruposProduto.FirstOrDefaultAsync(g => g.Id == id, ct);
        if (grupo is null) return NotFound(new { mensagem = "Grupo nao encontrado." });
        if (!grupo.Ativo) return NoContent();

        if (await _context.Produtos.AnyAsync(p => p.GrupoProdutoId == id && p.Ativo, ct))
            return Conflict(new { mensagem = "Desative primeiro os produtos ativos deste grupo." });
        grupo.Ativo = false;
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private static bool EhDuplicado(DbUpdateException ex) =>
        ex.InnerException is MySqlException { Number: 1062 };
}
