using Distribuidora.API.Data;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Distribuidora.API.Controllers;
[ApiController,Route("api/admin/painel"),Authorize(Roles=PerfisSistema.Admin)]
public sealed class PainelAdminController(DistribuidoraDbContext banco):ControllerBase {
 [HttpGet("resumo")] public async Task<IActionResult> Resumo(CancellationToken ct) {
  var hoje=DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo")));
  return Ok(new {
   vendedores=await banco.Vendedores.CountAsync(v=>v.Ativo,ct), clientes=await banco.Clientes.CountAsync(c=>c.Ativo,ct), produtos=await banco.Produtos.CountAsync(p=>p.Ativo,ct),
   pedidosPendentes=await banco.Pedidos.CountAsync(p=>p.Status==StatusPedido.Pendente,ct),
   vendas=await banco.Vendas.CountAsync(ct), valorVendido=await banco.Vendas.SumAsync(v=>(decimal?)v.ValorTotal,ct)??0,
   saldoAReceber=await banco.ParcelasReceber.SumAsync(p=>(decimal?)(p.ValorOriginal-p.ValorPago),ct)??0,
   saldoVencido=await banco.ParcelasReceber.Where(p=>p.Vencimento<hoje).SumAsync(p=>(decimal?)(p.ValorOriginal-p.ValorPago),ct)??0,
   lotesVencidosComSaldo=await banco.LotesEstoque.CountAsync(l=>l.DataValidade<hoje&&l.QuantidadeDisponivel>0,ct)
  });
 }
 [HttpGet("contas")] public async Task<IActionResult> Contas(int pagina=1,int tamanho=50,int? clienteId=null,CancellationToken ct=default) {
  if(pagina<1||pagina>100000||tamanho is <1 or >100||clienteId is <=0)return BadRequest(new {mensagem="Filtros inválidos."});
  var query=banco.ContasReceber.AsNoTracking().AsQueryable();
  if(clienteId.HasValue)query=query.Where(c=>c.Venda.Pedido.ClienteId==clienteId);
  var total=await query.CountAsync(ct);
  var contas=await query.OrderByDescending(c=>c.Id).Skip((pagina-1)*tamanho).Take(tamanho).Select(c=>new {
   c.Id,c.VendaId,cliente=c.Venda.Pedido.Cliente.RazaoSocial,c.ValorOriginal,
   totalPago=c.Parcelas.Sum(p=>p.ValorPago),saldoDevedor=c.Parcelas.Sum(p=>p.ValorOriginal-p.ValorPago)
  }).ToListAsync(ct);
  return Ok(new {pagina,tamanho,total,contas});
 }
}
