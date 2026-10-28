using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

// Apenas para testes em localhost. Exigir JWT/Admin antes de expor em rede.
[ApiController]
[Route("api/produtos/{produtoId:int}/lotes/{loteId:int}/movimentacoes")]
public sealed class MovimentacoesEstoqueController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public MovimentacoesEstoqueController(DistribuidoraDbContext context)
        => _context = context;

    private static MovimentacaoEstoqueRespostaDto Resposta(MovimentacaoEstoque m)
        => new(m.Id, m.LoteEstoqueId, (int)m.Tipo, m.Quantidade,
            m.SaldoAposMovimentacao, m.ChaveOperacao, m.Observacao, m.DataUtc);

    [HttpGet]
    public async Task<IActionResult> Listar(int produtoId, int loteId, CancellationToken ct)
    {
        if (!await _context.LotesEstoque.AsNoTracking()
            .AnyAsync(l => l.Id == loteId && l.ProdutoId == produtoId, ct))
            return NotFound(new { mensagem = "Lote nao encontrado neste produto." });

        var resultado = await _context.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.LoteEstoqueId == loteId)
            .OrderBy(m => m.Id)
            .Select(m => new MovimentacaoEstoqueRespostaDto(
                m.Id, m.LoteEstoqueId, (int)m.Tipo, m.Quantidade,
                m.SaldoAposMovimentacao, m.ChaveOperacao, m.Observacao, m.DataUtc))
            .ToListAsync(ct);
        return Ok(resultado);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(
        int produtoId, int loteId, int id, CancellationToken ct)
    {
        var resultado = await _context.MovimentacoesEstoque.AsNoTracking()
            .Where(m => m.Id == id && m.LoteEstoqueId == loteId &&
                m.LoteEstoque.ProdutoId == produtoId)
            .Select(m => new MovimentacaoEstoqueRespostaDto(
                m.Id, m.LoteEstoqueId, (int)m.Tipo, m.Quantidade,
                m.SaldoAposMovimentacao, m.ChaveOperacao, m.Observacao, m.DataUtc))
            .FirstOrDefaultAsync(ct);
        return resultado is null
            ? NotFound(new { mensagem = "Movimentacao nao encontrada." })
            : Ok(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> Registrar(
        int produtoId, int loteId,
        [FromBody] MovimentacaoEstoqueCriarDto dados, CancellationToken ct)
    {
        if (!Guid.TryParseExact(dados.ChaveOperacao, "D", out var chaveGuid))
            return BadRequest(new { mensagem = "ChaveOperacao deve ser um UUID no formato padrao." });

        var chave = chaveGuid.ToString("D");
        var tipo = (TipoMovimentacaoEstoque)dados.Tipo;
        var obs = (dados.Observacao ?? string.Empty).Trim();

        // A mesma chave identifica UMA operacao. Reenvios identicos nao baixam duas vezes.
        var anterior = await _context.MovimentacoesEstoque.AsNoTracking()
            .FirstOrDefaultAsync(m => m.ChaveOperacao == chave, ct);
        if (anterior is not null)
        {
            if (anterior.LoteEstoqueId == loteId && anterior.Tipo == tipo &&
                anterior.Quantidade == dados.Quantidade && anterior.Observacao == obs &&
                await _context.LotesEstoque.AsNoTracking().AnyAsync(
                    l => l.Id == loteId && l.ProdutoId == produtoId, ct))
                return Ok(Resposta(anterior));

            return Conflict(new { mensagem = "ChaveOperacao ja usada em outra operacao." });
        }

        var lote = await _context.LotesEstoque.AsNoTracking()
            .Where(l => l.Id == loteId && l.ProdutoId == produtoId)
            .Select(l => new { l.Id, l.DataValidade, l.Produto.Ativo })
            .FirstOrDefaultAsync(ct);
        if (lote is null) return NotFound(new { mensagem = "Lote nao encontrado neste produto." });
        if (!lote.Ativo)
            return Conflict(new { mensagem = "Produto desativado nao permite movimentacao." });

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        if (lote.DataValidade < hoje)
            return Conflict(new { mensagem = "Lote vencido nao permite movimentacao normal." });

        await using var transacao = await _context.Database.BeginTransactionAsync(ct);

        // UPDATE condicional: banco garante que o saldo nao fica negativo nem estoura INT.
        // Em chamadas concorrentes, somente o UPDATE com condicao verdadeira afeta a linha.
        var alterados = tipo == TipoMovimentacaoEstoque.Saida
            ? await _context.LotesEstoque
                .Where(l => l.Id == loteId && l.ProdutoId == produtoId &&
                    l.Produto.Ativo && l.DataValidade >= hoje &&
                    l.QuantidadeDisponivel >= dados.Quantidade)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    l => l.QuantidadeDisponivel,
                    l => l.QuantidadeDisponivel - dados.Quantidade), ct)
            : await _context.LotesEstoque
                .Where(l => l.Id == loteId && l.ProdutoId == produtoId &&
                    l.Produto.Ativo && l.DataValidade >= hoje &&
                    l.QuantidadeDisponivel <= int.MaxValue - dados.Quantidade)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    l => l.QuantidadeDisponivel,
                    l => l.QuantidadeDisponivel + dados.Quantidade), ct);

        if (alterados != 1)
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "Operacao recusada: saldo insuficiente, limite excedido ou lote indisponivel." });
        }

        var saldo = await _context.LotesEstoque.AsNoTracking()
            .Where(l => l.Id == loteId)
            .Select(l => l.QuantidadeDisponivel)
            .SingleAsync(ct);

        var movimento = new MovimentacaoEstoque
        {
            LoteEstoqueId = loteId,
            Tipo = tipo,
            Quantidade = dados.Quantidade,
            SaldoAposMovimentacao = saldo,
            ChaveOperacao = chave,
            Observacao = obs,
            DataUtc = DateTime.UtcNow
        };
        _context.MovimentacoesEstoque.Add(movimento);
        try
        {
            await _context.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            await transacao.RollbackAsync(ct);
            return Conflict(new { mensagem = "ChaveOperacao duplicada por tentativa simultanea; consulte a movimentacao antes de repetir." });
        }

        return CreatedAtAction(nameof(BuscarPorId),
            new { produtoId, loteId, id = movimento.Id }, Resposta(movimento));
    }
}
