using System.Linq.Expressions;
using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public ClientesController(DistribuidoraDbContext context)
    {
        _context = context;
    }

    // Projecao feita no proprio SQL, sem retornar entidades do banco.
    private static readonly Expression<Func<Cliente, ClienteRespostaDto>> Projecao =
        c => new ClienteRespostaDto(
            c.Id,
            c.RazaoSocial,
            c.Cnpj,
            c.Telefone,
            c.Endereco,
            c.Cidade,
            c.Estado,
            c.OrdemVisita,
            c.VendedorId,
            c.Vendedor.Nome,
            c.Ativo);

    // GET /api/clientes?vendedorId=1&ativo=true
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? vendedorId,
        [FromQuery] bool? ativo,
        CancellationToken cancellationToken)
    {
        if (vendedorId is <= 0)
            return BadRequest(new { mensagem = "VendedorId deve ser positivo." });

        IQueryable<Cliente> consulta = _context.Clientes.AsNoTracking();

        if (vendedorId.HasValue)
            consulta = consulta.Where(c => c.VendedorId == vendedorId.Value);

        if (ativo.HasValue)
            consulta = consulta.Where(c => c.Ativo == ativo.Value);

        var clientes = await consulta
            .OrderBy(c => c.OrdemVisita == 0) // Sem ordem definida por ultimo.
            .ThenBy(c => c.OrdemVisita)
            .ThenBy(c => c.RazaoSocial)
            .Select(Projecao)
            .ToListAsync(cancellationToken);

        return Ok(clientes);
    }

    // GET /api/clientes/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(
        int id,
        CancellationToken cancellationToken)
    {
        var cliente = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(Projecao)
            .FirstOrDefaultAsync(cancellationToken);

        return cliente is null
            ? NotFound(new { mensagem = "Cliente nao encontrado." })
            : Ok(cliente);
    }

    // POST /api/clientes
    [HttpPost]
    public async Task<IActionResult> Cadastrar(
        [FromBody] ClienteSalvarDto dados,
        CancellationToken cancellationToken)
    {
        var razaoSocial = dados.RazaoSocial.Trim();
        var cnpj = dados.Cnpj.Trim();

        if (razaoSocial.Length < 3)
            return BadRequest(new { mensagem = "Razao social deve ter pelo menos 3 caracteres." });

        var vendedor = await _context.Vendedores.AsNoTracking()
            .Where(v => v.Id == dados.VendedorId)
            .Select(v => new { v.Id, v.Nome, v.Ativo })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendedor is null)
            return BadRequest(new { mensagem = "VendedorId nao corresponde a um vendedor existente." });

        if (!vendedor.Ativo)
            return Conflict(new { mensagem = "Nao e possivel associar cliente a vendedor inativo." });

        if (await _context.Clientes.AnyAsync(c => c.Cnpj == cnpj, cancellationToken))
            return Conflict(new { mensagem = "Ja existe um cliente com este CNPJ." });

        var cliente = new Cliente
        {
            RazaoSocial = razaoSocial,
            Cnpj = cnpj,
            Telefone = dados.Telefone.Trim(),
            Endereco = dados.Endereco.Trim(),
            Cidade = dados.Cidade.Trim(),
            Estado = dados.Estado.Trim().ToUpperInvariant(),
            OrdemVisita = dados.OrdemVisita,
            VendedorId = vendedor.Id,
            Ativo = true
        };

        _context.Clientes.Add(cliente);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EhChaveDuplicada(ex))
        {
            return Conflict(new { mensagem = "Ja existe um cliente com este CNPJ." });
        }

        return CreatedAtAction(
            nameof(BuscarPorId),
            new { id = cliente.Id },
            new ClienteRespostaDto(
                cliente.Id, cliente.RazaoSocial, cliente.Cnpj,
                cliente.Telefone, cliente.Endereco, cliente.Cidade,
                cliente.Estado, cliente.OrdemVisita, cliente.VendedorId,
                vendedor.Nome, cliente.Ativo));
    }

    // PUT /api/clientes/1
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(
        int id,
        [FromBody] ClienteSalvarDto dados,
        CancellationToken cancellationToken)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cliente is null)
            return NotFound(new { mensagem = "Cliente nao encontrado." });

        var razaoSocial = dados.RazaoSocial.Trim();
        var cnpj = dados.Cnpj.Trim();

        if (razaoSocial.Length < 3)
            return BadRequest(new { mensagem = "Razao social deve ter pelo menos 3 caracteres." });

        var vendedor = await _context.Vendedores.AsNoTracking()
            .Where(v => v.Id == dados.VendedorId)
            .Select(v => new { v.Id, v.Nome, v.Ativo })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendedor is null)
            return BadRequest(new { mensagem = "VendedorId nao corresponde a um vendedor existente." });

        // Mantem edicao cadastral se o vendedor atual foi desativado.
        // Entretanto, uma transferencia exige vendedor de destino ativo.
        if (!vendedor.Ativo && dados.VendedorId != cliente.VendedorId)
            return Conflict(new { mensagem = "Nao e possivel transferir cliente para vendedor inativo." });

        if (await _context.Clientes.AnyAsync(
                c => c.Id != id && c.Cnpj == cnpj, cancellationToken))
            return Conflict(new { mensagem = "Ja existe outro cliente com este CNPJ." });

        cliente.RazaoSocial = razaoSocial;
        cliente.Cnpj = cnpj;
        cliente.Telefone = dados.Telefone.Trim();
        cliente.Endereco = dados.Endereco.Trim();
        cliente.Cidade = dados.Cidade.Trim();
        cliente.Estado = dados.Estado.Trim().ToUpperInvariant();
        cliente.OrdemVisita = dados.OrdemVisita;
        cliente.VendedorId = dados.VendedorId;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EhChaveDuplicada(ex))
        {
            return Conflict(new { mensagem = "Ja existe outro cliente com este CNPJ." });
        }

        return Ok(new ClienteRespostaDto(
            cliente.Id, cliente.RazaoSocial, cliente.Cnpj,
            cliente.Telefone, cliente.Endereco, cliente.Cidade,
            cliente.Estado, cliente.OrdemVisita, cliente.VendedorId,
            vendedor.Nome, cliente.Ativo));
    }

    // PATCH /api/clientes/1/desativar
    [HttpPatch("{id:int}/desativar")]
    public async Task<IActionResult> Desativar(
        int id,
        CancellationToken cancellationToken)
    {
        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (cliente is null)
            return NotFound(new { mensagem = "Cliente nao encontrado." });

        if (cliente.Ativo)
        {
            cliente.Ativo = false;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static bool EhChaveDuplicada(DbUpdateException exception) =>
        exception.InnerException is MySqlException { Number: 1062 };
}
