using System.Data;
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
[Route("api/contas-receber")]
[Authorize(Roles = PerfisSistema.Admin)]
public sealed class ContasReceberController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;
    public ContasReceberController(DistribuidoraDbContext banco) => _banco = banco;

    private static ContaReceberRespostaDto Resposta(ContaReceber conta)
    {
        var parcelas = conta.Parcelas.OrderBy(p => p.Numero).Select(p =>
        {
            var saldo = ContasReceberRegras.SaldoDevedor(p.ValorOriginal, p.ValorPago);
            var situacao = saldo == 0m ? "Pago" : p.ValorPago > 0m ? "Parcial" : "Aberto";
            return new ParcelaReceberRespostaDto(
                p.Id, p.Numero, p.Vencimento, p.ValorOriginal, p.ValorPago, saldo, situacao);
        }).ToList();
        var pago = parcelas.Sum(p => p.ValorPago);
        return new ContaReceberRespostaDto(
            conta.Id, conta.VendaId, conta.Venda.Pedido.ClienteId,
            conta.Venda.Pedido.Cliente.RazaoSocial, conta.CondicaoPagamentoId,
            conta.CondicaoPagamento.Nome, conta.DataCriacaoUtc,
            conta.ValorOriginal, pago, conta.ValorOriginal - pago, parcelas);
    }

    private async Task<ContaReceber?> Carregar(int? contaId, int? vendaId, CancellationToken ct)
    {
        var consulta = _banco.ContasReceber.AsNoTracking().AsQueryable();
        if (contaId.HasValue) consulta = consulta.Where(c => c.Id == contaId.Value);
        if (vendaId.HasValue) consulta = consulta.Where(c => c.VendaId == vendaId.Value);
        return await consulta
            .Include(c => c.Venda).ThenInclude(v => v.Pedido).ThenInclude(p => p.Cliente)
            .Include(c => c.CondicaoPagamento)
            .Include(c => c.Parcelas)
            .AsSplitQuery().FirstOrDefaultAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Buscar(int id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        var conta = await Carregar(id, null, ct);
        return conta is null ? NotFound(new { mensagem = "Conta nao encontrada." }) : Ok(Resposta(conta));
    }

    [HttpGet("por-venda/{vendaId:int}")]
    public async Task<IActionResult> BuscarPorVenda(int vendaId, CancellationToken ct)
    {
        if (vendaId <= 0) return NotFound();
        var conta = await Carregar(null, vendaId, ct);
        return conta is null ? NotFound(new { mensagem = "Venda sem conta a receber." }) : Ok(Resposta(conta));
    }

    [HttpGet("cliente/{clienteId:int}/resumo")]
    public async Task<IActionResult> ResumoCliente(int clienteId, CancellationToken ct)
    {
        if (clienteId <= 0) return BadRequest(new { mensagem = "ClienteId invalido." });
        var cliente = await _banco.Clientes.AsNoTracking()
            .Where(c => c.Id == clienteId)
            .Select(c => new { c.Id, c.RazaoSocial }).FirstOrDefaultAsync(ct);
        if (cliente is null) return NotFound(new { mensagem = "Cliente nao encontrado." });

        var parcelas = await _banco.ParcelasReceber.AsNoTracking()
            .Where(p => p.ContaReceber.Venda.Pedido.ClienteId == clienteId)
            .Select(p => new { p.Vencimento, p.ValorOriginal, p.ValorPago })
            .ToListAsync(ct);
        var total = parcelas.Sum(p => p.ValorOriginal);
        var pago = parcelas.Sum(p => p.ValorPago);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var vencido = parcelas.Where(p => p.Vencimento < hoje)
            .Sum(p => p.ValorOriginal - p.ValorPago);
        return Ok(new { cliente.Id, cliente.RazaoSocial,
            ValorTotal = total, ValorPago = pago, SaldoDevedor = total - pago,
            SaldoVencido = vencido, Parcelas = parcelas.Count });
    }

    [HttpPost("da-venda/{vendaId:int}")]
    public async Task<IActionResult> GerarDaVenda(
        int vendaId, [FromBody] GerarContaReceberDto dto, CancellationToken ct)
    {
        if (vendaId <= 0 || dto.CondicaoPagamentoId <= 0)
            return BadRequest(new { mensagem = "Informe IDs positivos da venda e condicao." });

        var existente = await Carregar(null, vendaId, ct);
        if (existente is not null)
            return existente.CondicaoPagamentoId == dto.CondicaoPagamentoId
                ? Ok(Resposta(existente))
                : Conflict(new { mensagem = "A venda ja possui conta com outra condicao." });

        var venda = await _banco.Vendas.AsNoTracking()
            .Where(v => v.Id == vendaId)
            .Select(v => new { v.Id, v.ValorTotal, v.DataEmissaoUtc })
            .FirstOrDefaultAsync(ct);
        if (venda is null) return NotFound(new { mensagem = "Venda nao encontrada." });

        var condicao = await _banco.CondicoesPagamento.AsNoTracking()
            .Include(c => c.Parcelas).AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == dto.CondicaoPagamentoId, ct);
        if (condicao is null) return NotFound(new { mensagem = "Condicao nao encontrada." });
        if (!condicao.Ativa)
            return Conflict(new { mensagem = "Condicao inativa nao pode ser usada em novas contas." });

        IReadOnlyList<ParcelaSimuladaDto> simuladas;
        try
        {
            var regras = condicao.Parcelas.OrderBy(p => p.Numero)
                .Select(p => new RegraParcela(p.DiasAposVenda, p.Percentual)).ToList();
            simuladas = ParcelamentoRegras.Simular(
                venda.ValorTotal, DateOnly.FromDateTime(venda.DataEmissaoUtc), regras);
        }
        catch (ArgumentException)
        {
            return Conflict(new { mensagem = "Venda ou condicao invalida para parcelamento." });
        }
        catch (InvalidOperationException)
        {
            return Conflict(new { mensagem = "A condicao resulta em parcela de valor zero." });
        }

        var conta = new ContaReceber
        {
            VendaId = venda.Id,
            CondicaoPagamentoId = condicao.Id,
            DataCriacaoUtc = DateTime.UtcNow,
            ValorOriginal = venda.ValorTotal,
            Parcelas = simuladas.Select(p => new ParcelaReceber
            {
                Numero = p.Numero, Vencimento = p.Vencimento,
                ValorOriginal = p.Valor, ValorPago = 0m
            }).ToList()
        };
        _banco.ContasReceber.Add(conta);
        try
        {
            await _banco.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            _banco.ChangeTracker.Clear();
            var concorrente = await Carregar(null, vendaId, ct);
            if (concorrente is not null)
                return concorrente.CondicaoPagamentoId == dto.CondicaoPagamentoId
                    ? Ok(Resposta(concorrente))
                    : Conflict(new { mensagem = "Outra condicao ja foi aplicada a esta venda." });
            return Conflict(new { mensagem = "Operacao concorrente; tente consultar a conta." });
        }
        _banco.ChangeTracker.Clear();
        var criada = await Carregar(conta.Id, null, ct);
        if (criada is null) return StatusCode(500, new { mensagem = "Conta gravada mas nao encontrada." });
        return CreatedAtAction(nameof(Buscar), new { id = criada.Id }, Resposta(criada));
    }

    private static RecebimentoRespostaDto Resposta(Recebimento r) =>
        new(r.Id, r.ParcelaReceberId, r.ParcelaReceber.ContaReceberId,
            r.ChaveOperacao, r.Valor, r.FormaPagamento, r.Observacao, r.DataUtc);

    private async Task<Recebimento?> CarregarRecebimento(string chave, CancellationToken ct) =>
        await _banco.Recebimentos.AsNoTracking()
            .Include(r => r.ParcelaReceber)
            .FirstOrDefaultAsync(r => r.ChaveOperacao == chave, ct);

    [HttpGet("parcelas/{parcelaId:int}/recebimentos")]
    public async Task<IActionResult> ListarRecebimentos(int parcelaId, CancellationToken ct)
    {
        if (parcelaId <= 0) return NotFound();
        if (!await _banco.ParcelasReceber.AnyAsync(p => p.Id == parcelaId, ct))
            return NotFound(new { mensagem = "Parcela nao encontrada." });
        var recebimentos = await _banco.Recebimentos.AsNoTracking()
            .Include(r => r.ParcelaReceber)
            .Where(r => r.ParcelaReceberId == parcelaId)
            .OrderBy(r => r.DataUtc).ThenBy(r => r.Id)
            .ToListAsync(ct);
        return Ok(recebimentos.Select(Resposta).ToList());
    }

    [HttpPost("parcelas/{parcelaId:int}/recebimentos")]
    public async Task<IActionResult> RegistrarRecebimento(
        int parcelaId, [FromBody] RegistrarRecebimentoDto dto, CancellationToken ct)
    {
        if (parcelaId <= 0) return BadRequest(new { mensagem = "ParcelaId invalido." });
        if (dto.ChaveOperacao == Guid.Empty)
            return BadRequest(new { mensagem = "ChaveOperacao deve ser UUID nao vazio." });
        if (!ContasReceberRegras.ValorMonetarioPositivo(dto.Valor))
            return BadRequest(new { mensagem = "Valor deve ser positivo e ter ate duas casas decimais." });
        var forma = dto.FormaPagamento?.Trim() ?? string.Empty;
        var observacao = dto.Observacao?.Trim() ?? string.Empty;
        if (forma.Length is < 2 or > 40 || observacao.Length > 200)
            return BadRequest(new { mensagem = "Forma ou observacao invalida." });
        var chave = dto.ChaveOperacao.ToString("D");

        bool Identico(Recebimento r) => r.ParcelaReceberId == parcelaId &&
            r.Valor == dto.Valor && r.FormaPagamento == forma && r.Observacao == observacao;

        var existente = await CarregarRecebimento(chave, ct);
        if (existente is not null)
            return Identico(existente) ? Ok(Resposta(existente))
                : Conflict(new { mensagem = "ChaveOperacao utilizada com outros dados." });

        await using var transacao = await _banco.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var atualizadas = await _banco.ParcelasReceber
            .Where(p => p.Id == parcelaId && p.ValorPago + dto.Valor <= p.ValorOriginal)
            .ExecuteUpdateAsync(p => p.SetProperty(
                item => item.ValorPago, item => item.ValorPago + dto.Valor), ct);
        if (atualizadas != 1)
        {
            await transacao.RollbackAsync(ct);
            if (!await _banco.ParcelasReceber.AnyAsync(p => p.Id == parcelaId, ct))
                return NotFound(new { mensagem = "Parcela nao encontrada." });
            return Conflict(new { mensagem = "Recebimento excede o saldo devedor." });
        }

        var novo = new Recebimento
        {
            ParcelaReceberId = parcelaId, ChaveOperacao = chave,
            Valor = dto.Valor, FormaPagamento = forma,
            Observacao = observacao, DataUtc = DateTime.UtcNow
        };
        _banco.Recebimentos.Add(novo);
        try
        {
            await _banco.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            await transacao.RollbackAsync(ct);
            _banco.ChangeTracker.Clear();
            var concorrente = await CarregarRecebimento(chave, ct);
            if (concorrente is not null)
                return Identico(concorrente) ? Ok(Resposta(concorrente))
                    : Conflict(new { mensagem = "Chave reutilizada para recebimento diferente." });
            return Conflict(new { mensagem = "Recebimento concorrente; consulte novamente." });
        }

        _banco.ChangeTracker.Clear();
        var criado = await CarregarRecebimento(chave, ct);
        if (criado is null)
            return StatusCode(500, new { mensagem = "Recebimento gravado mas nao encontrado." });
        return CreatedAtAction(nameof(ListarRecebimentos),
            new { parcelaId = criado.ParcelaReceberId }, Resposta(criado));
    }
}
