using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Distribuidora.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/vendas/{vendaId:int}/parcelamento")]
[Authorize(Roles = PerfisSistema.Admin)]
public sealed class SimulacoesParcelamentoController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;
    public SimulacoesParcelamentoController(DistribuidoraDbContext banco) => _banco = banco;

    [HttpGet("simular")]
    public async Task<IActionResult> Simular(int vendaId, [FromQuery] int condicaoId, CancellationToken ct)
    {
        if (vendaId <= 0 || condicaoId <= 0)
            return BadRequest(new { mensagem = "Informe IDs positivos da venda e da condicao." });
        var venda = await _banco.Vendas.AsNoTracking()
            .Where(v => v.Id == vendaId)
            .Select(v => new { v.Id, v.PedidoId, v.DataEmissaoUtc, v.ValorTotal })
            .FirstOrDefaultAsync(ct);
        if (venda is null) return NotFound(new { mensagem = "Venda nao encontrada." });

        var condicao = await _banco.CondicoesPagamento.AsNoTracking()
            .Include(c => c.Parcelas).AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == condicaoId, ct);
        if (condicao is null) return NotFound(new { mensagem = "Condicao nao encontrada." });
        if (!condicao.Ativa) return Conflict(new { mensagem = "Condicao desativada nao pode ser aplicada a uma venda nova." });

        var regras = condicao.Parcelas.OrderBy(p => p.Numero)
            .Select(p => new RegraParcela(p.DiasAposVenda, p.Percentual)).ToList();
        IReadOnlyList<ParcelaSimuladaDto> simulacao;
        try
        {
            simulacao = ParcelamentoRegras.Simular(
                venda.ValorTotal, DateOnly.FromDateTime(venda.DataEmissaoUtc), regras);
        }
        catch (ArgumentException)
        {
            return Conflict(new { mensagem = "Venda ou condicao possui dados invalidos para parcelar." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { mensagem = "Valor da venda nao comporta essas parcelas sem parcela zerada." });
        }

        return Ok(new SimulacaoParcelamentoRespostaDto(
            venda.Id, venda.PedidoId, condicao.Id, condicao.Nome,
            DateOnly.FromDateTime(venda.DataEmissaoUtc), venda.ValorTotal,
            simulacao.Sum(p => p.Valor), simulacao));
    }
}
