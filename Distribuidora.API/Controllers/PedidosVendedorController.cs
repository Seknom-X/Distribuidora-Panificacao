using System.Security.Claims;
using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Distribuidora.API.Controllers;

[ApiController]
[Route("api/me/pedidos")]
[Authorize(Roles = PerfisSistema.Vendedor)]
public sealed class PedidosVendedorController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;

    public PedidosVendedorController(DistribuidoraDbContext banco) => _banco = banco;

    private int VendedorAutenticadoId()
    {
        // OnTokenValidated garante que a claim corresponde a uma conta ativa.
        return int.TryParse(User.FindFirstValue("vendedorId"), out var id) && id > 0 ? id : 0;
    }

    private static PedidoRespostaDto ParaResposta(Pedido pedido)
    {
        var itens = pedido.Itens.OrderBy(i => i.Id)
            .Select(i => new ItemPedidoRespostaDto(
                i.ProdutoId, i.Produto.Nome, i.QuantidadeSolicitada,
                i.QuantidadeAtendida, i.PrecoUnitario,
                i.PrecoUnitario * i.QuantidadeSolicitada))
            .ToList();

        return new PedidoRespostaDto(
            pedido.Id, pedido.ChaveRequisicao, pedido.ClienteId,
            pedido.Cliente.RazaoSocial, pedido.VendedorId, pedido.Vendedor.Nome,
            (int)pedido.Status, pedido.Status.ToString(), pedido.DataCriacao,
            itens.Sum(i => i.SubtotalSolicitado), itens);
    }

    private static bool PedidoEhReenvioValido(
        Pedido anterior, PedidoVendedorCriarDto dados, int vendedorId)
    {
        if (anterior.VendedorId != vendedorId || anterior.ClienteId != dados.ClienteId ||
            anterior.Itens.Count != dados.Itens.Count)
            return false;

        var recebidos = dados.Itens.ToDictionary(i => i.ProdutoId, i => i.QuantidadeSolicitada);
        return anterior.Itens.All(i =>
            recebidos.TryGetValue(i.ProdutoId, out var quantidade) &&
            quantidade == i.QuantidadeSolicitada);
    }

    private async Task<Pedido?> CarregarPorChaveAsync(string chave, CancellationToken ct)
        => await _banco.Pedidos.AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Vendedor)
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.ChaveRequisicao == chave, ct);

    // Apenas o proprio vendedor ve os detalhes do seu pedido.
    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(int id, CancellationToken ct)
    {
        var vendedorId = VendedorAutenticadoId();
        if (vendedorId == 0) return Unauthorized();
        if (id <= 0) return NotFound();

        var pedido = await _banco.Pedidos.AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Vendedor)
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id && p.VendedorId == vendedorId, ct);

        return pedido is null
            ? NotFound(new { mensagem = "Pedido nao encontrado na sua carteira." })
            : Ok(ParaResposta(pedido));
    }

    // O GET /api/me/pedidos (lista) continua no MinhaCarteiraController.
    [HttpPost]
    public async Task<IActionResult> Cadastrar(
        [FromBody] PedidoVendedorCriarDto dados, CancellationToken ct)
    {
        var vendedorId = VendedorAutenticadoId();
        if (vendedorId == 0) return Unauthorized();

        if (!Guid.TryParseExact(dados.ChaveRequisicao, "D", out var uuid))
            return BadRequest(new { mensagem = "ChaveRequisicao deve ser UUID no formato padrao." });

        if (dados.Itens is null || dados.Itens.Count is < 1 or > 100 ||
            dados.Itens.Any(i => i.ProdutoId <= 0 ||
                i.QuantidadeSolicitada is < 1 or > 1000000))
            return BadRequest(new { mensagem = "Itens ou quantidades invalidos." });

        var idsProduto = dados.Itens.Select(i => i.ProdutoId).ToList();
        if (idsProduto.Distinct().Count() != idsProduto.Count)
            return BadRequest(new { mensagem = "O mesmo produto nao pode aparecer duas vezes." });

        // Nao permitir consultar ou cadastrar clientes de outro vendedor.
        // Usar 404 impede expor existencia/detalhes de outro cliente.
        var cliente = await _banco.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == dados.ClienteId && c.VendedorId == vendedorId && c.Ativo, ct);
        if (cliente is null)
            return NotFound(new { mensagem = "Cliente nao encontrado na sua carteira ativa." });

        var chave = uuid.ToString("D");
        var anterior = await CarregarPorChaveAsync(chave, ct);
        if (anterior is not null)
        {
            // Nao retornar o pedido de um vendedor diferente, mesmo com chave conhecida.
            return PedidoEhReenvioValido(anterior, dados, vendedorId)
                ? Ok(ParaResposta(anterior))
                : Conflict(new { mensagem = "ChaveRequisicao ja usada." });
        }

        var vendedor = await _banco.Vendedores.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vendedorId && v.Ativo, ct);
        if (vendedor is null)
            return Unauthorized(new { mensagem = "Vendedor inativo ou nao encontrado." });

        var produtos = await _banco.Produtos.AsNoTracking()
            .Where(p => idsProduto.Contains(p.Id))
            .Select(p => new { p.Id, p.Nome, p.Ativo, p.PrecoVenda })
            .ToListAsync(ct);

        if (produtos.Count != idsProduto.Count)
            return BadRequest(new { mensagem = "Um ou mais produtos nao existem." });
        if (produtos.Any(p => !p.Ativo || p.PrecoVenda <= 0))
            return Conflict(new { mensagem = "Produtos devem estar ativos e ter preco positivo." });

        var precos = produtos.ToDictionary(p => p.Id, p => p.PrecoVenda);
        var nomes = produtos.ToDictionary(p => p.Id, p => p.Nome);

        // Nenhuma baixa nem reserva: o pedido nasce Pendente como na Aula 9.1.
        var pedido = new Pedido
        {
            ChaveRequisicao = chave,
            ClienteId = cliente.Id,
            VendedorId = vendedorId,
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

        _banco.Pedidos.Add(pedido);
        try
        {
            // SaveChanges e transacional para o pedido e seus itens.
            await _banco.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            _banco.ChangeTracker.Clear();
            var concorrente = await CarregarPorChaveAsync(chave, ct);
            if (concorrente is not null)
                return PedidoEhReenvioValido(concorrente, dados, vendedorId)
                    ? Ok(ParaResposta(concorrente))
                    : Conflict(new { mensagem = "ChaveRequisicao ja usada." });
            return Conflict(new { mensagem = "Cadastro duplicado; confira os dados." });
        }

        var resposta = new PedidoRespostaDto(
            pedido.Id, pedido.ChaveRequisicao, pedido.ClienteId,
            cliente.RazaoSocial, vendedorId, vendedor.Nome,
            (int)pedido.Status, pedido.Status.ToString(), pedido.DataCriacao,
            pedido.Itens.Sum(i => i.PrecoUnitario * i.QuantidadeSolicitada),
            pedido.Itens.Select(i => new ItemPedidoRespostaDto(
                i.ProdutoId, nomes[i.ProdutoId], i.QuantidadeSolicitada,
                i.QuantidadeAtendida, i.PrecoUnitario,
                i.PrecoUnitario * i.QuantidadeSolicitada)).ToList());

        return CreatedAtAction(nameof(BuscarPorId), new { id = pedido.Id }, resposta);
    }
}
