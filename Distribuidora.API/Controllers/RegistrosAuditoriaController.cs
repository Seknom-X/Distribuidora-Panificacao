using Distribuidora.API.Data;
using Distribuidora.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Distribuidora.API.Controllers;

[ApiController]
[Authorize(Roles = PerfisSistema.Admin)]
[Route("api/admin/registros-auditoria")]
public sealed class RegistrosAuditoriaController : ControllerBase
{
    private readonly DistribuidoraDbContext _banco;
    public RegistrosAuditoriaController(DistribuidoraDbContext banco) => _banco = banco;

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int limite = 30,
        [FromQuery] int? usuarioId = null,
        CancellationToken ct = default)
    {
        if (limite is < 1 or > 100 || usuarioId is <= 0)
            return BadRequest(new { mensagem = "Informe limite entre 1 e 100 e usuarioId positivo." });

        var consulta = _banco.RegistrosAuditoria.AsNoTracking().AsQueryable();
        if (usuarioId.HasValue)
            consulta = consulta.Where(r => r.UsuarioId == usuarioId.Value);

        var registros = await consulta.OrderByDescending(r => r.Id).Take(limite)
            .Select(r => new
            {
                r.Id, r.DataUtc, r.UsuarioId, r.Perfil,
                r.Metodo, r.Caminho, r.StatusHttp, r.CorrelacaoId
            })
            .ToListAsync(ct);

        return Ok(registros);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Buscar(long id, CancellationToken ct)
    {
        if (id <= 0) return NotFound();
        var registro = await _banco.RegistrosAuditoria.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        return registro is null ? NotFound() : Ok(registro);
    }
}
