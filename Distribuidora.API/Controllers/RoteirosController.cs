using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

// MVP: roteiro simples por vendedor; sem data, check-in ou GPS nesta etapa.
// ATENCAO: habilitar autenticacao/autorizacao antes de disponibilizar na rede.
[ApiController]
[Route("api/vendedores/{vendedorId:int}/roteiro")]
public sealed class RoteirosController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public RoteirosController(DistribuidoraDbContext context)
    {
        _context = context;
    }

    // GET /api/vendedores/2/roteiro
    [HttpGet]
    public async Task<IActionResult> Consultar(
        int vendedorId,
        CancellationToken cancellationToken)
    {
        if (vendedorId <= 0)
            return BadRequest(new { mensagem = "VendedorId deve ser positivo." });

        var vendedor = await _context.Vendedores
            .AsNoTracking()
            .Where(v => v.Id == vendedorId)
            .Select(v => new { v.Id, v.Nome })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendedor is null)
            return NotFound(new { mensagem = "Vendedor nao encontrado." });

        var clientes = await _context.Clientes
            .AsNoTracking()
            .Where(c => c.VendedorId == vendedorId && c.Ativo)
            .OrderBy(c => c.OrdemVisita == 0) // Sem posicao definida fica por ultimo.
            .ThenBy(c => c.OrdemVisita)
            .ThenBy(c => c.RazaoSocial)
            .Select(c => new ClienteRoteiroDto(
                c.Id, c.RazaoSocial, c.Cidade,
                c.Endereco, c.Telefone, c.OrdemVisita))
            .ToListAsync(cancellationToken);

        return Ok(new RoteiroRespostaDto(vendedor.Id, vendedor.Nome, clientes));
    }

    // PUT /api/vendedores/2/roteiro
    // Corpo: { "clienteIds": [12, 7, 9] }
    [HttpPut]
    public async Task<IActionResult> Reordenar(
        int vendedorId,
        [FromBody] ReordenarRoteiroDto dados,
        CancellationToken cancellationToken)
    {
        if (vendedorId <= 0)
            return BadRequest(new { mensagem = "VendedorId deve ser positivo." });

        var ids = dados.ClienteIds;
        if (ids is null)
            return BadRequest(new { mensagem = "Informe clienteIds." });

        if (ids.Any(id => id <= 0) || ids.Count != ids.Distinct().Count())
            return BadRequest(new { mensagem = "clienteIds deve conter IDs positivos e sem repeticao." });

        var vendedor = await _context.Vendedores
            .AsNoTracking()
            .Where(v => v.Id == vendedorId)
            .Select(v => new { v.Id, v.Nome, v.Ativo })
            .FirstOrDefaultAsync(cancellationToken);

        if (vendedor is null)
            return NotFound(new { mensagem = "Vendedor nao encontrado." });

        if (!vendedor.Ativo)
            return Conflict(new { mensagem = "Nao e permitido reorganizar roteiro de vendedor inativo." });

        // Apenas clientes ativos que realmente pertencem a este vendedor.
        var clientes = await _context.Clientes
            .Where(c => c.VendedorId == vendedorId && c.Ativo)
            .ToListAsync(cancellationToken);

        var idsDaCarteira = clientes.Select(c => c.Id).ToHashSet();
        if (ids.Count != clientes.Count || ids.Any(id => !idsDaCarteira.Contains(id)))
        {
            return Conflict(new
            {
                mensagem = "A lista precisa conter TODOS os clientes ativos deste vendedor, " +
                           "sem incluir clientes de outros vendedores ou desativados.",
                quantidadeClientesAtivos = clientes.Count
            });
        }

        var posicoes = ids.Select((id, indice) => new { id, posicao = indice + 1 })
            .ToDictionary(x => x.id, x => x.posicao);

        foreach (var cliente in clientes)
            cliente.OrdemVisita = posicoes[cliente.Id];

        // Uma unica chamada: EF Core salva as alteracoes numa transacao
        // se o provedor relacional tiver suporte (MySQL/InnoDB tem).
        await _context.SaveChangesAsync(cancellationToken);

        var ordenados = clientes
            .OrderBy(c => c.OrdemVisita)
            .Select(c => new ClienteRoteiroDto(
                c.Id, c.RazaoSocial, c.Cidade,
                c.Endereco, c.Telefone, c.OrdemVisita))
            .ToList();

        return Ok(new RoteiroRespostaDto(vendedor.Id, vendedor.Nome, ordenados));
    }
}
