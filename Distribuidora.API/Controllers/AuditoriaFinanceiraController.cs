using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Distribuidora.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/admin/auditoria")]
[Authorize(Roles = PerfisSistema.Admin)]
public sealed class AuditoriaFinanceiraController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;

    public AuditoriaFinanceiraController(DistribuidoraDbContext banco) => _banco = banco;

    private static ContaAuditoriaDto Conferir(ContaReceber conta)
    {
        var linhas = conta.Parcelas.Select(p =>
            new ConferenciaFinanceiraRegras.ParcelaEntrada(
                p.Id, p.Numero, p.ValorOriginal, p.ValorPago,
                p.Recebimentos.Sum(r => r.Valor))).ToList();

        return ConferenciaFinanceiraRegras.Conferir(
            conta.Id, conta.VendaId, conta.ValorOriginal,
            conta.Venda.ValorTotal, linhas);
    }

    private IQueryable<ContaReceber> QueryCompleta() =>
        _banco.ContasReceber.AsNoTracking()
            .Include(c => c.Venda)
            .Include(c => c.Parcelas).ThenInclude(p => p.Recebimentos)
            .AsSplitQuery();

    [HttpGet("contas/{contaId:int}")]
    public async Task<IActionResult> ConferirConta(int contaId, CancellationToken ct)
    {
        if (contaId <= 0) return NotFound();
        var conta = await QueryCompleta()
            .FirstOrDefaultAsync(c => c.Id == contaId, ct);
        return conta is null ? NotFound(new { mensagem = "Conta nao encontrada." })
                             : Ok(Conferir(conta));
    }

    // Limite didatico: a consulta e explicitamente das ultimas N contas,
    // nao de toda a historia do banco. Nao altera nenhum registro.
    [HttpGet("financeiro")]
    public async Task<IActionResult> ConferenciaGeral(
        [FromQuery] int limite = 20, CancellationToken ct = default)
    {
        if (limite is < 1 or > 100)
            return BadRequest(new { mensagem = "Limite deve ser entre 1 e 100." });

        var ids = await _banco.ContasReceber.AsNoTracking()
            .OrderByDescending(c => c.Id)
            .Select(c => c.Id)
            .Take(limite)
            .ToListAsync(ct);

        var contas = await QueryCompleta()
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(ct);

        var itens = contas.OrderByDescending(c => c.Id).Select(Conferir).ToList();
        return Ok(new VisaoGeralAuditoriaDto(
            itens.Count, itens.Count(c => !c.Consistente), itens));
    }
}
