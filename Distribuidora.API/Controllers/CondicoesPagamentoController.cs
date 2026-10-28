using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Distribuidora.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/condicoes-pagamento")]
[Authorize(Roles = PerfisSistema.Admin)]
public sealed class CondicoesPagamentoController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;
    public CondicoesPagamentoController(DistribuidoraDbContext banco) => _banco = banco;

    private static CondicaoPagamentoRespostaDto Resposta(CondicaoPagamento c) =>
        new(c.Id, c.Nome, c.Ativa,
            c.Parcelas.OrderBy(p => p.Numero)
                .Select(p => new ParcelaCondicaoRespostaDto(
                    p.Numero, p.DiasAposVenda, p.Percentual)).ToList());

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool? ativa, CancellationToken ct)
    {
        var consulta = _banco.CondicoesPagamento.AsNoTracking().AsQueryable();
        if (ativa.HasValue) consulta = consulta.Where(c => c.Ativa == ativa.Value);
        var condicoes = await consulta.Include(c => c.Parcelas)
            .OrderBy(c => c.Id).Take(100).AsSplitQuery().ToListAsync(ct);
        return Ok(condicoes.Select(Resposta).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Buscar(int id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        var condicao = await _banco.CondicoesPagamento.AsNoTracking()
            .Include(c => c.Parcelas).AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return condicao is null ? NotFound(new { mensagem = "Condicao nao encontrada." }) : Ok(Resposta(condicao));
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarCondicaoPagamentoDto dto, CancellationToken ct)
    {
        var nome = dto.Nome?.Trim() ?? string.Empty;
        if (nome.Length is < 3 or > 120)
            return BadRequest(new { mensagem = "Nome deve ter entre 3 e 120 caracteres." });
        var parcelas = dto.Parcelas?.Select(p => new RegraParcela(p.DiasAposVenda, p.Percentual)).ToList();
        if (!ParcelamentoRegras.DistribuicaoValida(parcelas))
            return BadRequest(new { mensagem = "Informe de 1 a 12 parcelas com dias crescentes e distintos, percentuais positivos de ate duas casas e soma exata de 100%." });

        if (await _banco.CondicoesPagamento.AnyAsync(c => c.Nome == nome, ct))
            return Conflict(new { mensagem = "Ja existe condicao de pagamento com esse nome." });

        var condicao = new CondicaoPagamento
        {
            Nome = nome,
            Ativa = true,
            Parcelas = parcelas!.Select((p, i) => new ParcelaCondicao
            {
                Numero = i + 1, DiasAposVenda = p.DiasAposVenda, Percentual = p.Percentual
            }).ToList()
        };
        _banco.CondicoesPagamento.Add(condicao);
        try
        {
            await _banco.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            return Conflict(new { mensagem = "Condicao duplicada ou numero de parcela repetido." });
        }
        return CreatedAtAction(nameof(Buscar), new { id = condicao.Id }, Resposta(condicao));
    }

    [HttpPatch("{id:int}/desativar")]
    public async Task<IActionResult> Desativar(int id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        var condicao = await _banco.CondicoesPagamento.FindAsync(new object[] { id }, ct);
        if (condicao is null) return NotFound(new { mensagem = "Condicao nao encontrada." });
        if (!condicao.Ativa) return Ok(new { condicao.Id, condicao.Nome, condicao.Ativa });
        condicao.Ativa = false;
        await _banco.SaveChangesAsync(ct);
        return Ok(new { condicao.Id, condicao.Nome, condicao.Ativa });
    }
}
