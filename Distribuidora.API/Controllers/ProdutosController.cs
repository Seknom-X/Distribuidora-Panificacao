using Distribuidora.API.Data;
using Distribuidora.API.DTOs;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

// EXERCICIO LOCAL: proteger com JWT / papel Admin antes de publicar em rede.
[ApiController]
[Route("api/produtos")]
public sealed class ProdutosController : ControllerBase
{
    private readonly DistribuidoraDbContext _context;

    public ProdutosController(DistribuidoraDbContext context) => _context = context;

    private static bool PrecoValido(decimal preco) =>
        preco > 0m && preco <= 9999999999999999.99m &&
        decimal.Round(preco, 2) == preco;

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? grupoProdutoId, [FromQuery] bool? ativo, CancellationToken ct)
    {
        if (grupoProdutoId is <= 0)
            return BadRequest(new { mensagem = "GrupoProdutoId deve ser positivo." });
        var consulta = _context.Produtos.AsNoTracking().AsQueryable();
        if (grupoProdutoId.HasValue)
            consulta = consulta.Where(p => p.GrupoProdutoId == grupoProdutoId.Value);
        if (ativo.HasValue)
            consulta = consulta.Where(p => p.Ativo == ativo.Value);
        var produtos = await consulta.OrderBy(p => p.Nome)
            .Select(p => new ProdutoRespostaDto(
                p.Id, p.Nome, p.UnidadeMedida, p.PrecoVenda, p.Ativo,
                p.GrupoProdutoId, p.GrupoProduto == null ? null : p.GrupoProduto.Nome))
            .ToListAsync(ct);
        return Ok(produtos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(int id, CancellationToken ct)
    {
        var produto = await _context.Produtos.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProdutoRespostaDto(
                p.Id, p.Nome, p.UnidadeMedida, p.PrecoVenda, p.Ativo,
                p.GrupoProdutoId, p.GrupoProduto == null ? null : p.GrupoProduto.Nome))
            .FirstOrDefaultAsync(ct);
        return produto is null
            ? NotFound(new { mensagem = "Produto nao encontrado." }) : Ok(produto);
    }

    [HttpPost]
    public async Task<IActionResult> Cadastrar([FromBody] ProdutoSalvarDto dados, CancellationToken ct)
    {
        if (!PrecoValido(dados.PrecoVenda))
            return BadRequest(new { mensagem = "Preco deve ser positivo, com ate duas casas decimais." });
        var nome = dados.Nome.Trim();
        var unidade = dados.UnidadeMedida.Trim().ToUpperInvariant();
        if (nome.Length < 2 || unidade.Length == 0)
            return BadRequest(new { mensagem = "Informe nome e unidade de medida validos." });

        var grupo = await _context.GruposProduto.AsNoTracking()
            .Where(g => g.Id == dados.GrupoProdutoId)
            .Select(g => new { g.Id, g.Nome, g.Ativo })
            .FirstOrDefaultAsync(ct);
        if (grupo is null) return BadRequest(new { mensagem = "GrupoProdutoId nao encontrado." });
        if (!grupo.Ativo) return Conflict(new { mensagem = "Grupo de produtos desativado." });

        var produto = new Produto
        {
            Nome = nome,
            UnidadeMedida = unidade,
            PrecoVenda = dados.PrecoVenda,
            GrupoProdutoId = grupo.Id,
            Ativo = true
        };
        _context.Produtos.Add(produto);
        await _context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(BuscarPorId), new { id = produto.Id },
            new ProdutoRespostaDto(produto.Id, produto.Nome, produto.UnidadeMedida,
                produto.PrecoVenda, produto.Ativo, produto.GrupoProdutoId, grupo.Nome));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Atualizar(
        int id, [FromBody] ProdutoSalvarDto dados, CancellationToken ct)
    {
        var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (produto is null) return NotFound(new { mensagem = "Produto nao encontrado." });
        if (!PrecoValido(dados.PrecoVenda))
            return BadRequest(new { mensagem = "Preco deve ser positivo, com ate duas casas decimais." });
        var nome = dados.Nome.Trim();
        var unidade = dados.UnidadeMedida.Trim().ToUpperInvariant();
        if (nome.Length < 2 || unidade.Length == 0)
            return BadRequest(new { mensagem = "Informe nome e unidade de medida validos." });

        var grupo = await _context.GruposProduto.AsNoTracking()
            .Where(g => g.Id == dados.GrupoProdutoId)
            .Select(g => new { g.Id, g.Nome, g.Ativo })
            .FirstOrDefaultAsync(ct);
        if (grupo is null) return BadRequest(new { mensagem = "GrupoProdutoId nao encontrado." });
        if (!grupo.Ativo && produto.GrupoProdutoId != grupo.Id)
            return Conflict(new { mensagem = "Nao e permitido transferir para grupo desativado." });

        produto.Nome = nome;
        produto.UnidadeMedida = unidade;
        produto.PrecoVenda = dados.PrecoVenda;
        produto.GrupoProdutoId = grupo.Id;
        await _context.SaveChangesAsync(ct);
        return Ok(new ProdutoRespostaDto(produto.Id, produto.Nome, produto.UnidadeMedida,
            produto.PrecoVenda, produto.Ativo, produto.GrupoProdutoId, grupo.Nome));
    }

    [HttpPatch("{id:int}/desativar")]
    public async Task<IActionResult> Desativar(int id, CancellationToken ct)
    {
        var produto = await _context.Produtos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (produto is null) return NotFound(new { mensagem = "Produto nao encontrado." });
        if (produto.Ativo)
        {
            produto.Ativo = false;
            await _context.SaveChangesAsync(ct);
        }
        return NoContent();
    }
}
