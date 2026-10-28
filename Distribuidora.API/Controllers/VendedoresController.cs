using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/vendedores")]
public class VendedoresController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public VendedoresController(DistribuidoraDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        IQueryable<Vendedor> consulta = _context.Vendedores.AsNoTracking();

        if (ativo.HasValue)
            consulta = consulta.Where(v => v.Ativo == ativo.Value);

        var vendedores = await consulta
            .OrderBy(v => v.Nome)
            .ToListAsync(cancellationToken);

        return Ok(vendedores.Select(ParaResposta));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(
        int id,
        CancellationToken cancellationToken)
    {
        var vendedor = await _context.Vendedores
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (vendedor is null)
            return NotFound(new { mensagem = "Vendedor nao encontrado." });

        return Ok(ParaResposta(vendedor));
    }

    [HttpPost]
    public async Task<IActionResult> Cadastrar(
        [FromBody] VendedorSalvarDto dados,
        CancellationToken cancellationToken)
    {
        var nome = dados.Nome?.Trim() ?? string.Empty;
        var email = dados.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var telefone = dados.Telefone?.Trim() ?? string.Empty;

        if (nome.Length < 3)
            return BadRequest(new { mensagem = "O nome deve ter pelo menos 3 caracteres." });

        if (await _context.Vendedores.AnyAsync(v => v.Email == email, cancellationToken))
            return Conflict(new { mensagem = "Ja existe um vendedor com este e-mail." });

        var vendedor = new Vendedor
        {
            Nome = nome,
            Email = email,
            Telefone = telefone,
            Ativo = true
        };

        _context.Vendedores.Add(vendedor);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EhEmailDuplicado(ex))
        {
            return Conflict(new { mensagem = "Ja existe um vendedor com este e-mail." });
        }

        return CreatedAtAction(
            nameof(BuscarPorId),
            new { id = vendedor.Id },
            ParaResposta(vendedor));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(
        int id,
        [FromBody] VendedorSalvarDto dados,
        CancellationToken cancellationToken)
    {
        var vendedor = await _context.Vendedores
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (vendedor is null)
            return NotFound(new { mensagem = "Vendedor nao encontrado." });

        var nome = dados.Nome?.Trim() ?? string.Empty;
        var email = dados.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var telefone = dados.Telefone?.Trim() ?? string.Empty;

        if (nome.Length < 3)
            return BadRequest(new { mensagem = "O nome deve ter pelo menos 3 caracteres." });

        if (await _context.Vendedores.AnyAsync(
            v => v.Id != id && v.Email == email, cancellationToken))
            return Conflict(new { mensagem = "Ja existe outro vendedor com este e-mail." });

        vendedor.Nome = nome;
        vendedor.Email = email;
        vendedor.Telefone = telefone;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EhEmailDuplicado(ex))
        {
            return Conflict(new { mensagem = "Ja existe outro vendedor com este e-mail." });
        }

        return Ok(ParaResposta(vendedor));
    }

    [HttpPatch("{id:int}/desativar")]
    public async Task<IActionResult> Desativar(
        int id,
        CancellationToken cancellationToken)
    {
        var vendedor = await _context.Vendedores
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (vendedor is null)
            return NotFound(new { mensagem = "Vendedor nao encontrado." });

        if (vendedor.Ativo)
        {
            vendedor.Ativo = false;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static bool EhEmailDuplicado(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };

    private static VendedorRespostaDto ParaResposta(Vendedor vendedor) =>
        new(
            vendedor.Id,
            vendedor.Nome,
            vendedor.Email,
            vendedor.Telefone,
            vendedor.Ativo);
}
