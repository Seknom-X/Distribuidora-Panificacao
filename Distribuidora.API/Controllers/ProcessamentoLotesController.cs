using System.Data;
using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Distribuidora.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

// Usar exclusivamente no localhost ate implementar autenticacao JWT/Admin.
[ApiController]
[Route("api/lotes-processamento")]
public sealed class ProcessamentoLotesController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public ProcessamentoLotesController(DistribuidoraDbContext context) => _context = context;

    private async Task<ProcessamentoLoteRespostaDto?> CarregarResumo(int loteId, CancellationToken ct)
    {
        var lote = await _context.LotesProcessamento.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == loteId, ct);
        if (lote is null) return null;

        var dados = await _context.LotesProcessamentoPedidos.AsNoTracking()
            .Where(v => v.LoteProcessamentoId == loteId)
            .OrderBy(v => v.PedidoId)
            .Select(v => new
            {
                v.PedidoId,
                v.Pedido.Status,
                Itens = v.Pedido.Itens.Select(i => new
                {
                    i.QuantidadeSolicitada,
                    i.QuantidadeAtendida,
                    i.PrecoUnitario
                }).ToList()
            })
            .ToListAsync(ct);

        var pedidos = dados.Select(p => new PedidoProcessamentoResumoDto(
            p.PedidoId, (int)p.Status, p.Status.ToString(),
            p.Itens.Sum(i => i.QuantidadeSolicitada),
            p.Itens.Sum(i => i.QuantidadeAtendida),
            p.Itens.Sum(i => i.PrecoUnitario * i.QuantidadeAtendida)))
            .ToList();

        return new ProcessamentoLoteRespostaDto(
            lote.Id,
            lote.DataProcessamentoUtc.HasValue,
            lote.DataProcessamentoUtc,
            pedidos.Count,
            pedidos.Count(p => p.Status == (int)StatusPedido.Atendido ||
                               p.Status == (int)StatusPedido.AtendidoParcialmente),
            pedidos.Sum(p => (long)p.QuantidadeAtendida),
            pedidos.Sum(p => p.ValorAtendido),
            pedidos);
    }

    [HttpGet("{id:int}/processamento")]
    public async Task<IActionResult> Consultar(int id, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new { mensagem = "LoteId invalido." });
        var resumo = await CarregarResumo(id, ct);
        return resumo is null
            ? NotFound(new { mensagem = "Lote de processamento nao encontrado." })
            : Ok(resumo);
    }

    // Quantidades decididas pelo responsavel; nao ha corte automatico proporcional nesta aula.
    // TODAS as saidas, movimentos, alocacoes e status do lote sao atomicos.
    [HttpPost("{id:int}/processar")]
    public async Task<IActionResult> Processar(int id, [FromBody] ProcessarLoteDto dados, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new { mensagem = "LoteId invalido." });
        if (dados is null || dados.Pedidos is null || dados.Pedidos.Count is < 1 or > 100 ||
            dados.Pedidos.Any(p => p is null || p.PedidoId <= 0 || p.Itens is null ||
                p.Itens.Count is < 1 or > 100 ||
                p.Itens.Any(i => i is null || i.ProdutoId <= 0 ||
                    i.QuantidadeAtendida is < 0 or > 1000000) ||
                !p.Itens.Any(i => i.QuantidadeAtendida > 0)))
            return BadRequest(new { mensagem = "Informe 1 a 100 pedidos, com todos os itens de cada um e pelo menos uma unidade atendida por pedido." });

        if (dados.Pedidos.Select(p => p.PedidoId).Distinct().Count() != dados.Pedidos.Count ||
            dados.Pedidos.Any(p => p.Itens.Select(i => i.ProdutoId).Distinct().Count() != p.Itens.Count))
            return BadRequest(new { mensagem = "IDs de pedidos e produtos nao podem se repetir." });

        await using var transacao = await _context.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);

        // Bloqueio exclusivo da janela: duas chamadas concorrentes nao processam o mesmo lote.
        // O valor e escrito provisoriamente dentro da transacao e so existe apos COMMIT.
        var atualizados = await _context.LotesProcessamento
            .Where(l => l.Id == id && l.DataProcessamentoUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.DataProcessamentoUtc, DateTime.UtcNow), ct);
        if (atualizados == 0)
        {
            await transacao.RollbackAsync(ct);
            var existe = await _context.LotesProcessamento.AsNoTracking()
                .AnyAsync(l => l.Id == id, ct);
            return existe
                ? Conflict(new { mensagem = "Lote ja processado. Use GET /processamento para consultar. Nenhuma baixa foi repetida." })
                : NotFound(new { mensagem = "Lote nao encontrado." });
        }

        // Snapshot da aula anterior: somente os pedidos vinculados podem ser processados.
        var esperados = await _context.LotesProcessamentoPedidos.AsNoTracking()
            .Where(v => v.LoteProcessamentoId == id)
            .Select(v => v.PedidoId).ToListAsync(ct);
        if (esperados.Count == 0)
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "Lote sem pedidos. Nao ha o que processar." });
        }
        if (esperados.Count > 100)
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "Lote com mais de 100 pedidos. A implementacao inicial exige evolucao para processamento paginado." });
        }
        if (!PlanoCorteLote.MesmosPedidos(esperados, dados.Pedidos.Select(p => p.PedidoId)))
        {
            await transacao.RollbackAsync(ct);
            return BadRequest(new { mensagem = "Envie exatamente os pedidos vinculados a esta janela, sem faltar ou acrescentar IDs." });
        }

        var hojeUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var planoPedido in dados.Pedidos.OrderBy(p => p.PedidoId))
        {
            // So um processador pode alterar cada pedido Pendente.
            var reservados = await _context.Pedidos
                .Where(p => p.Id == planoPedido.PedidoId && p.Status == StatusPedido.Pendente)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPedido.EmAnalise), ct);
            if (reservados != 1)
            {
                await transacao.RollbackAsync(ct);
                return Conflict(new { mensagem = $"Pedido {planoPedido.PedidoId} ja esta processado ou indisponivel. Toda a operacao foi revertida." });
            }

            var pedido = await _context.Pedidos.Include(p => p.Itens)
                .FirstAsync(p => p.Id == planoPedido.PedidoId, ct);
            var itens = pedido.Itens.OrderBy(i => i.ProdutoId).ToList();
            if (!PlanoCorteLote.MesmosProdutos(
                    itens.Select(i => i.ProdutoId), planoPedido.Itens.Select(i => i.ProdutoId)))
            {
                await transacao.RollbackAsync(ct);
                return BadRequest(new { mensagem = $"Os itens do pedido {pedido.Id} devem ser informados exatamente uma vez." });
            }

            var quantidades = planoPedido.Itens.ToDictionary(i => i.ProdutoId, i => i.QuantidadeAtendida);
            if (itens.Any(i => quantidades[i.ProdutoId] > i.QuantidadeSolicitada))
            {
                await transacao.RollbackAsync(ct);
                return BadRequest(new { mensagem = $"Corte acima da quantidade solicitada no pedido {pedido.Id}." });
            }

            foreach (var item in itens)
            {
                var quantidade = quantidades[item.ProdutoId];
                if (quantidade == 0) { item.QuantidadeAtendida = 0; continue; }

                if (!await _context.Produtos.AsNoTracking()
                        .AnyAsync(p => p.Id == item.ProdutoId && p.Ativo, ct))
                {
                    await transacao.RollbackAsync(ct);
                    return Conflict(new { mensagem = $"Produto {item.ProdutoId} desativado; rollback completo." });
                }
                var lotes = await _context.LotesEstoque.AsNoTracking()
                    .Where(l => l.ProdutoId == item.ProdutoId &&
                        l.DataValidade >= hojeUtc && l.QuantidadeDisponivel > 0)
                    .OrderBy(l => l.DataValidade).ThenBy(l => l.Id)
                    .Select(l => new { l.Id, l.CodigoLote, l.DataValidade, l.QuantidadeDisponivel })
                    .ToListAsync(ct);

                if (lotes.Sum(l => (long)l.QuantidadeDisponivel) < quantidade)
                {
                    await transacao.RollbackAsync(ct);
                    return Conflict(new { mensagem = $"Saldo valido insuficiente para o produto {item.ProdutoId} do pedido {pedido.Id}. Toda a janela foi revertida." });
                }

                var restante = quantidade;
                foreach (var lote in lotes)
                {
                    if (restante == 0) break;
                    var retirar = Math.Min(restante, lote.QuantidadeDisponivel);
                    var alterados = await _context.LotesEstoque
                        .Where(l => l.Id == lote.Id && l.ProdutoId == item.ProdutoId &&
                            l.Produto.Ativo && l.DataValidade >= hojeUtc &&
                            l.QuantidadeDisponivel >= retirar)
                        .ExecuteUpdateAsync(s => s.SetProperty(
                            l => l.QuantidadeDisponivel,
                            l => l.QuantidadeDisponivel - retirar), ct);
                    if (alterados != 1)
                    {
                        await transacao.RollbackAsync(ct);
                        return Conflict(new { mensagem = "Saldo concorrente alterado. Toda a janela foi revertida; tente novamente." });
                    }

                    var saldoDepois = await _context.LotesEstoque.AsNoTracking()
                        .Where(l => l.Id == lote.Id)
                        .Select(l => l.QuantidadeDisponivel).SingleAsync(ct);
                    var movimento = new MovimentacaoEstoque
                    {
                        LoteEstoqueId = lote.Id,
                        Tipo = TipoMovimentacaoEstoque.Saida,
                        Quantidade = retirar,
                        SaldoAposMovimentacao = saldoDepois,
                        ChaveOperacao = Guid.NewGuid().ToString("D"),
                        Observacao = $"Janela {id}, pedido {pedido.Id}, produto {item.ProdutoId}",
                        DataUtc = DateTime.UtcNow
                    };
                    _context.AlocacoesLotePedido.Add(new AlocacaoLotePedido
                    {
                        ItemPedidoId = item.Id,
                        LoteEstoqueId = lote.Id,
                        Quantidade = retirar,
                        MovimentacaoEstoque = movimento
                    });
                    restante -= retirar;
                }
                item.QuantidadeAtendida = quantidade;
            }

            pedido.Status = itens.Sum(i => i.QuantidadeAtendida) == itens.Sum(i => i.QuantidadeSolicitada)
                ? StatusPedido.Atendido
                : StatusPedido.AtendidoParcialmente;
        }

        try
        {
            await _context.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 or 1205 or 1213 })
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "Conflito no processamento; todas as alteracoes foram revertidas." });
        }
        catch (MySqlException ex) when (ex.Number is 1205 or 1213)
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "Concorrencia no processamento; nenhuma baixa foi confirmada." });
        }

        var resposta = await CarregarResumo(id, ct);
        return Ok(resposta);
    }
}
