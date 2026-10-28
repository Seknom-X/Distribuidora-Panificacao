using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/pedidos")]
public sealed class PedidosController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public PedidosController(DistribuidoraDbContext context) => _context = context;

    private static PedidoRespostaDto Resposta(Pedido pedido)
    {
        var itens = pedido.Itens.OrderBy(i => i.Id)
            .Select(i => new ItemPedidoRespostaDto(
                i.ProdutoId,
                i.Produto.Nome,
                i.QuantidadeSolicitada,
                i.QuantidadeAtendida,
                i.PrecoUnitario,
                i.PrecoUnitario * i.QuantidadeSolicitada))
            .ToList();

        return new PedidoRespostaDto(
            pedido.Id, pedido.ChaveRequisicao,
            pedido.ClienteId, pedido.Cliente.RazaoSocial,
            pedido.VendedorId, pedido.Vendedor.Nome,
            (int)pedido.Status, pedido.Status.ToString(),
            pedido.DataCriacao, itens.Sum(i => i.SubtotalSolicitado), itens);
    }

    private static bool MesmoPedido(Pedido pedido, PedidoCriarDto dados)
    {
        if (pedido.ClienteId != dados.ClienteId ||
            pedido.VendedorId != dados.VendedorId ||
            pedido.Itens.Count != dados.Itens.Count)
            return false;

        var itensRecebidos = dados.Itens.ToDictionary(i => i.ProdutoId, i => i.QuantidadeSolicitada);
        return pedido.Itens.All(i => itensRecebidos.TryGetValue(i.ProdutoId, out var quantidade) &&
            quantidade == i.QuantidadeSolicitada);
    }

    private async Task<Pedido?> CarregarPorChave(string chave, CancellationToken ct)
        => await _context.Pedidos.AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Vendedor)
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.ChaveRequisicao == chave, ct);

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? vendedorId, [FromQuery] int? clienteId,
        [FromQuery] int? status, CancellationToken ct)
    {
        if (vendedorId is <= 0 || clienteId is <= 0 ||
            (status.HasValue && !Enum.IsDefined(typeof(StatusPedido), status.Value)))
            return BadRequest(new { mensagem = "Filtros de pedidos invalidos." });

        var consulta = _context.Pedidos.AsNoTracking().AsQueryable();
        if (vendedorId.HasValue) consulta = consulta.Where(p => p.VendedorId == vendedorId.Value);
        if (clienteId.HasValue) consulta = consulta.Where(p => p.ClienteId == clienteId.Value);
        if (status.HasValue) consulta = consulta.Where(p => (int)p.Status == status.Value);

        var pedidos = await consulta.OrderByDescending(p => p.DataCriacao)
            .ThenByDescending(p => p.Id)
            .Take(100)
            .Include(p => p.Cliente)
            .Include(p => p.Vendedor)
            .Include(p => p.Itens)
            .AsSplitQuery()
            .ToListAsync(ct);

        var resposta = pedidos.Select(p => new PedidoResumoDto(
            p.Id, p.ClienteId, p.Cliente.RazaoSocial,
            p.VendedorId, p.Vendedor.Nome, (int)p.Status,
            p.Status.ToString(), p.DataCriacao,
            p.Itens.Sum(i => i.PrecoUnitario * i.QuantidadeSolicitada)))
            .ToList();
        return Ok(resposta);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(int id, CancellationToken ct)
    {
        var pedido = await _context.Pedidos.AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Vendedor)
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        return pedido is null
            ? NotFound(new { mensagem = "Pedido nao encontrado." })
            : Ok(Resposta(pedido));
    }

    [HttpPost]
    public async Task<IActionResult> Cadastrar([FromBody] PedidoCriarDto dados, CancellationToken ct)
    {
        if (!Guid.TryParseExact(dados.ChaveRequisicao, "D", out var chaveGuid))
            return BadRequest(new { mensagem = "ChaveRequisicao deve ser um UUID no formato padrao." });

        if (dados.Itens is null || dados.Itens.Count == 0 || dados.Itens.Count > 100 ||
            dados.Itens.Any(i => i.ProdutoId <= 0 ||
                i.QuantidadeSolicitada <= 0 || i.QuantidadeSolicitada > 1000000))
            return BadRequest(new { mensagem = "Informe entre 1 e 100 itens, com produtos e quantidades validos." });

        var ids = dados.Itens.Select(i => i.ProdutoId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            return BadRequest(new { mensagem = "O mesmo produto nao pode aparecer duas vezes no pedido." });

        var chave = chaveGuid.ToString("D");
        var anterior = await CarregarPorChave(chave, ct);
        if (anterior is not null)
            return MesmoPedido(anterior, dados)
                ? Ok(Resposta(anterior))
                : Conflict(new { mensagem = "ChaveRequisicao ja usada com dados diferentes." });

        var vendedor = await _context.Vendedores.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == dados.VendedorId, ct);
        if (vendedor is null)
            return BadRequest(new { mensagem = "Vendedor nao encontrado." });
        if (!vendedor.Ativo)
            return Conflict(new { mensagem = "Vendedor desativado." });

        var cliente = await _context.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dados.ClienteId, ct);
        if (cliente is null)
            return BadRequest(new { mensagem = "Cliente nao encontrado." });
        if (!cliente.Ativo)
            return Conflict(new { mensagem = "Cliente desativado." });
        if (cliente.VendedorId != vendedor.Id)
            return Conflict(new { mensagem = "Cliente nao pertence a carteira do vendedor informado." });

        var produtos = await _context.Produtos.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.Nome, p.Ativo, p.PrecoVenda })
            .ToListAsync(ct);

        if (produtos.Count != ids.Count)
            return BadRequest(new { mensagem = "Um ou mais produtos nao existem." });
        if (produtos.Any(p => !p.Ativo || p.PrecoVenda <= 0))
            return Conflict(new { mensagem = "Todos os produtos devem estar ativos e ter preco positivo." });

        var precos = produtos.ToDictionary(p => p.Id, p => p.PrecoVenda);
        var pedido = new Pedido
        {
            ChaveRequisicao = chave,
            ClienteId = cliente.Id,
            VendedorId = vendedor.Id,
            Status = StatusPedido.Pendente,
            DataCriacao = DateTime.UtcNow,
            Itens = dados.Itens.Select(i => new ItemPedido
            {
                ProdutoId = i.ProdutoId,
                QuantidadeSolicitada = i.QuantidadeSolicitada,
                QuantidadeAtendida = 0,
                PrecoUnitario = precos[i.ProdutoId]
            }).ToList()
        };

        _context.Pedidos.Add(pedido);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            _context.ChangeTracker.Clear();
            var concorrente = await CarregarPorChave(chave, ct);
            if (concorrente is not null)
                return MesmoPedido(concorrente, dados)
                    ? Ok(Resposta(concorrente))
                    : Conflict(new { mensagem = "ChaveRequisicao ja usada com dados diferentes." });
            return Conflict(new { mensagem = "Cadastro duplicado; verifique os dados do pedido." });
        }

        return CreatedAtAction(nameof(BuscarPorId), new { id = pedido.Id },
            new PedidoRespostaDto(
                pedido.Id, pedido.ChaveRequisicao,
                pedido.ClienteId, cliente.RazaoSocial,
                pedido.VendedorId, vendedor.Nome,
                (int)pedido.Status, pedido.Status.ToString(),
                pedido.DataCriacao,
                pedido.Itens.Sum(i => i.PrecoUnitario * i.QuantidadeSolicitada),
                pedido.Itens.Select(i => new ItemPedidoRespostaDto(
                    i.ProdutoId, produtos.First(p => p.Id == i.ProdutoId).Nome,
                    i.QuantidadeSolicitada, i.QuantidadeAtendida,
                    i.PrecoUnitario, i.PrecoUnitario * i.QuantidadeSolicitada))
                    .ToList()));
    }

    // Apenas para desenvolvimento local. Exigir JWT/Admin antes da implantacao publica.
    // PUT processa o pedido pendente uma unica vez, com baixa FEFO e trilha por lote.
    [HttpPut("{id:int}/cortar")]
    public async Task<IActionResult> Cortar(int id, [FromBody] CortePedidoDto dados, CancellationToken ct)
    {
        if (id <= 0) return BadRequest(new { mensagem = "Id do pedido invalido." });
        if (dados.Itens is null || dados.Itens.Count == 0 || dados.Itens.Count > 100 ||
            dados.Itens.Any(i => i.ProdutoId <= 0 || i.QuantidadeAtendida < 0 ||
                i.QuantidadeAtendida > 1000000))
            return BadRequest(new { mensagem = "Itens do corte invalidos." });

        var produtosRecebidos = dados.Itens.Select(i => i.ProdutoId).ToArray();
        if (produtosRecebidos.Distinct().Count() != produtosRecebidos.Length)
            return BadRequest(new { mensagem = "Informe cada produto apenas uma vez no corte." });
        if (!dados.Itens.Any(i => i.QuantidadeAtendida > 0))
            return BadRequest(new { mensagem = "Um corte deve atender pelo menos uma unidade. Cancelamento sera outra operacao." });

        // ExecuteUpdate nao cria transacao implicita. A transacao explicita abrange
        // a transicao do pedido, cada lote, os movimentos e as alocacoes.
        await using var transacao = await _context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.ReadCommitted, ct);

        // UPDATE condicional trava a linha do pedido: apenas uma chamada ganha.
        var bloqueados = await _context.Pedidos
            .Where(p => p.Id == id && p.Status == StatusPedido.Pendente)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPedido.EmAnalise), ct);

        if (bloqueados != 1)
        {
            await transacao.RollbackAsync(ct);
            var existe = await _context.Pedidos.AsNoTracking().AnyAsync(p => p.Id == id, ct);
            if (existe)
                return Conflict(new { mensagem = "Pedido ja processado ou indisponivel para corte." });
            return NotFound(new { mensagem = "Pedido nao encontrado." });
        }

        // Carregar DEPOIS do UPDATE: o status no tracking reflete EmAnalise.
        var pedido = await _context.Pedidos
            .Include(p => p.Itens)
            .FirstAsync(p => p.Id == id, ct);

        var itens = pedido.Itens.OrderBy(i => i.ProdutoId).ToList();
        if (itens.Count == 0 || itens.Count != dados.Itens.Count ||
            itens.Any(i => !produtosRecebidos.Contains(i.ProdutoId)))
        {
            await transacao.RollbackAsync(ct);
            return BadRequest(new { mensagem = "O corte deve informar todos os produtos existentes no pedido." });
        }

        var quantidades = dados.Itens.ToDictionary(i => i.ProdutoId, i => i.QuantidadeAtendida);
        if (itens.Any(i => quantidades[i.ProdutoId] > i.QuantidadeSolicitada))
        {
            await transacao.RollbackAsync(ct);
            return BadRequest(new { mensagem = "Quantidade atendida nao pode exceder a solicitada." });
        }

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        // Ordena produtos e lotes para reduzir o risco de deadlocks entre cortes.
        foreach (var item in itens)
        {
            var quantidade = quantidades[item.ProdutoId];
            if (quantidade == 0) { item.QuantidadeAtendida = 0; continue; }

            var produtoAtivo = await _context.Produtos.AsNoTracking()
                .AnyAsync(p => p.Id == item.ProdutoId && p.Ativo, ct);
            if (!produtoAtivo)
            {
                await transacao.RollbackAsync(ct);
                return Conflict(new { mensagem = $"Produto {item.ProdutoId} esta desativado." });
            }

            var lotes = await _context.LotesEstoque.AsNoTracking()
                .Where(l => l.ProdutoId == item.ProdutoId &&
                    l.DataValidade >= hoje && l.QuantidadeDisponivel > 0)
                .OrderBy(l => l.DataValidade).ThenBy(l => l.Id)
                .Select(l => new { l.Id, l.CodigoLote, l.DataValidade, l.QuantidadeDisponivel })
                .ToListAsync(ct);

            if (lotes.Sum(l => (long)l.QuantidadeDisponivel) < quantidade)
            {
                await transacao.RollbackAsync(ct);
                return Conflict(new { mensagem = $"Estoque valido insuficiente para o produto {item.ProdutoId}. Nenhum lote foi alterado." });
            }

            var restante = quantidade;
            foreach (var lote in lotes)
            {
                if (restante == 0) break;
                var retirar = Math.Min(restante, lote.QuantidadeDisponivel);

                // Condicao avaliada no MySQL; impede saldo negativo em concorrencia.
                var atualizados = await _context.LotesEstoque
                    .Where(l => l.Id == lote.Id && l.ProdutoId == item.ProdutoId &&
                        l.Produto.Ativo && l.DataValidade >= hoje &&
                        l.QuantidadeDisponivel >= retirar)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.QuantidadeDisponivel,
                        l => l.QuantidadeDisponivel - retirar), ct);
                if (atualizados != 1)
                {
                    await transacao.RollbackAsync(ct);
                    return Conflict(new { mensagem = "Saldo alterado por outra operacao. O corte foi revertido; tente novamente." });
                }

                // Leitura apos o UPDATE usa o saldo efetivo, mesmo com concorrencia.
                var saldoDepois = await _context.LotesEstoque.AsNoTracking()
                    .Where(l => l.Id == lote.Id)
                    .Select(l => l.QuantidadeDisponivel)
                    .SingleAsync(ct);

                var movimento = new MovimentacaoEstoque
                {
                    LoteEstoqueId = lote.Id,
                    Tipo = TipoMovimentacaoEstoque.Saida,
                    Quantidade = retirar,
                    SaldoAposMovimentacao = saldoDepois,
                    ChaveOperacao = Guid.NewGuid().ToString("D"),
                    Observacao = $"Corte do pedido {pedido.Id}, produto {item.ProdutoId}",
                    DataUtc = DateTime.UtcNow
                };
                var alocacao = new AlocacaoLotePedido
                {
                    ItemPedidoId = item.Id,
                    LoteEstoqueId = lote.Id,
                    Quantidade = retirar,
                    MovimentacaoEstoque = movimento
                };

                _context.AlocacoesLotePedido.Add(alocacao);
                restante -= retirar;

                // IDs da movimentacao serao gerados ao salvar; completamos a resposta depois.
            }
            item.QuantidadeAtendida = quantidade;
        }

        var totalSolicitado = itens.Sum(i => i.QuantidadeSolicitada);
        var totalAtendido = itens.Sum(i => i.QuantidadeAtendida);
        var valorTotalAtendido = itens.Sum(i => i.PrecoUnitario * i.QuantidadeAtendida);
        pedido.Status = totalAtendido == totalSolicitado
            ? StatusPedido.Atendido
            : StatusPedido.AtendidoParcialmente;

        await _context.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        // Reconsulta sem tracking para retornar as alocacoes com IDs reais.
        var alocacoesSalvas = await _context.AlocacoesLotePedido.AsNoTracking()
            .Where(a => a.ItemPedido.PedidoId == id)
            .OrderBy(a => a.ItemPedido.ProdutoId)
            .ThenBy(a => a.LoteEstoque.DataValidade)
            .ThenBy(a => a.LoteEstoqueId)
            .Select(a => new AlocacaoLotePedidoRespostaDto(
                a.ItemPedidoId, a.ItemPedido.ProdutoId, a.LoteEstoqueId,
                a.LoteEstoque.CodigoLote, a.LoteEstoque.DataValidade,
                a.Quantidade, a.MovimentacaoEstoqueId))
            .ToListAsync(ct);

        return Ok(new CortePedidoRespostaDto(pedido.Id, (int)pedido.Status,
            pedido.Status.ToString(), totalSolicitado, totalAtendido,
            valorTotalAtendido, alocacoesSalvas));
    }

    [HttpGet("{id:int}/alocacoes")]
    public async Task<IActionResult> ListarAlocacoes(int id, CancellationToken ct)
    {
        if (!await _context.Pedidos.AsNoTracking().AnyAsync(p => p.Id == id, ct))
            return NotFound(new { mensagem = "Pedido nao encontrado." });

        var resultado = await _context.AlocacoesLotePedido.AsNoTracking()
            .Where(a => a.ItemPedido.PedidoId == id)
            .OrderBy(a => a.ItemPedido.ProdutoId)
            .ThenBy(a => a.LoteEstoque.DataValidade)
            .ThenBy(a => a.LoteEstoqueId)
            .Select(a => new AlocacaoLotePedidoRespostaDto(
                a.ItemPedidoId, a.ItemPedido.ProdutoId, a.LoteEstoqueId,
                a.LoteEstoque.CodigoLote, a.LoteEstoque.DataValidade,
                a.Quantidade, a.MovimentacaoEstoqueId))
            .ToListAsync(ct);
        return Ok(resultado);
    }
}
