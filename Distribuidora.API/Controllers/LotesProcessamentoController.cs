using System.Data;
using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Distribuidora.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

// Uso exclusivo em localhost ate implementarmos autenticacao JWT para administradores.
[ApiController]
[Route("api/lotes-processamento")]
public sealed class LotesProcessamentoController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public LotesProcessamentoController(DistribuidoraDbContext context) => _context = context;

    private async Task<LoteProcessamentoRespostaDto?> CarregarResposta(int id, CancellationToken ct)
    {
        var lote = await _context.LotesProcessamento.AsNoTracking()
            .Include(l => l.VinculosPedidos).ThenInclude(v => v.Pedido)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lote is null) return null;

        var pedidos = lote.VinculosPedidos
            .OrderBy(v => v.Pedido.DataCriacao).ThenBy(v => v.PedidoId)
            .Select(v => new PedidoLoteProcessamentoDto(
                v.PedidoId, v.Pedido.ClienteId, v.Pedido.VendedorId,
                v.Pedido.DataCriacao, (int)v.Pedido.Status, v.Pedido.Status.ToString()))
            .ToList();

        return new LoteProcessamentoRespostaDto(
            lote.Id, lote.DataCorte, lote.HoraCorte, JanelaCorteService.FusoHorario,
            lote.InicioUtc, lote.FimUtc, lote.DataFechamentoUtc,
            pedidos.Count, pedidos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new { mensagem = "Id invalido." });
        var resultado = await CarregarResposta(id, ct);
        return resultado is null
            ? NotFound(new { mensagem = "Lote de processamento nao encontrado." })
            : Ok(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> BuscarPorJanela(
        [FromQuery] DateOnly? data, [FromQuery] int? hora, CancellationToken ct)
    {
        if (data is null || data.Value.Year is < 2000 or > 2100 || hora is not (12 or 19))
            return BadRequest(new { mensagem = "Informe data=AAAA-MM-DD e hora=12 ou 19." });

        var id = await _context.LotesProcessamento.AsNoTracking()
            .Where(l => l.DataCorte == data.Value && l.HoraCorte == hora.Value)
            .Select(l => l.Id).FirstOrDefaultAsync(ct);

        if (id == 0) return NotFound(new { mensagem = "Janela ainda nao foi fechada." });
        return Ok((await CarregarResposta(id, ct))!);
    }

    [HttpPost("fechar")]
    public async Task<IActionResult> Fechar([FromBody] FecharJanelaDto? dados, CancellationToken ct)
    {
        if (dados is null || dados.DataCorte.Year is < 2000 or > 2100 ||
            dados.HoraCorte is not (12 or 19))
            return BadRequest(new { mensagem = "Informe dataCorte e horaCorte (12 ou 19)." });

        var intervalo = JanelaCorteService.Criar(dados.DataCorte, dados.HoraCorte);
        if (DateTime.UtcNow < intervalo.FimUtc)
            return Conflict(new { mensagem = "Nao e permitido fechar uma janela antes do fim do periodo." });

        // Repetir um fechamento devolve o mesmo lote, sem incluir pedidos novos.
        var idAnterior = await _context.LotesProcessamento.AsNoTracking()
            .Where(l => l.DataCorte == dados.DataCorte && l.HoraCorte == dados.HoraCorte)
            .Select(l => l.Id).FirstOrDefaultAsync(ct);
        if (idAnterior != 0)
            return Ok((await CarregarResposta(idAnterior, ct))!);

        // Uma unica transacao contem o lote e os vinculos com pedidos.
        await using var transacao = await _context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        var idDentroTransacao = await _context.LotesProcessamento.AsNoTracking()
            .Where(l => l.DataCorte == dados.DataCorte && l.HoraCorte == dados.HoraCorte)
            .Select(l => l.Id).FirstOrDefaultAsync(ct);
        if (idDentroTransacao != 0)
        {
            await transacao.RollbackAsync(ct);
            return Ok((await CarregarResposta(idDentroTransacao, ct))!);
        }

        // FOR UPDATE bloqueia os pedidos selecionados contra o corte simultaneo.
        // Parametros SQL seguros; a leitura seleciona somente PENDENTES da janela.
        // LIMIT explicito para nao criar fotografia incompleta silenciosamente.
        var selecionados = await _context.Pedidos.FromSqlInterpolated($@"
            SELECT * FROM `Pedidos`
            WHERE `Status` = {(int)StatusPedido.Pendente}
              AND `DataCriacao` > {intervalo.InicioUtc}
              AND `DataCriacao` <= {intervalo.FimUtc}
            ORDER BY `Id` LIMIT 1001 FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);

        if (selecionados.Count > 1000)
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "Mais de 1000 pedidos pendentes. Implementar paginacao antes de fechar." });
        }

        var lote = new LoteProcessamento
        {
            DataCorte = dados.DataCorte,
            HoraCorte = dados.HoraCorte,
            InicioUtc = intervalo.InicioUtc,
            FimUtc = intervalo.FimUtc,
            DataFechamentoUtc = DateTime.UtcNow,
            VinculosPedidos = selecionados.Select(p => new LoteProcessamentoPedido
            {
                PedidoId = p.Id
            }).ToList()
        };
        _context.LotesProcessamento.Add(lote);

        try
        {
            await _context.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            await transacao.RollbackAsync(ct);
            _context.ChangeTracker.Clear();

            var idConcorrente = await _context.LotesProcessamento.AsNoTracking()
                .Where(l => l.DataCorte == dados.DataCorte && l.HoraCorte == dados.HoraCorte)
                .Select(l => l.Id).FirstOrDefaultAsync(ct);
            if (idConcorrente != 0)
                return Ok((await CarregarResposta(idConcorrente, ct))!);

            return Conflict(new { mensagem = "Pedido ja incluido em outro lote ou fechamento concorrente." });
        }
        catch (MySqlException ex) when (ex.Number is 1205 or 1213)
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "Concorrencia no fechamento; tente novamente." });
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1205 or 1213 })
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "Concorrencia no fechamento; tente novamente." });
        }

        var resposta = await CarregarResposta(lote.Id, ct);
        return CreatedAtAction(nameof(BuscarPorId), new { id = lote.Id }, resposta);
    }
}
