using System.Security.Claims;
using Distribuidora.API.Data;
using Distribuidora.API.Models;
using Distribuidora.API.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Distribuidora.API.Middleware;

public sealed class AuditoriaOperacoesMiddleware
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<AuditoriaOperacoesMiddleware> _logger;

    public AuditoriaOperacoesMiddleware(
        RequestDelegate proximo,
        ILogger<AuditoriaOperacoesMiddleware> logger)
    {
        _proximo = proximo;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext contexto, IServiceScopeFactory escopos)
    {
        await _proximo(contexto);

        var metodo = contexto.Request.Method;
        var caminho = contexto.Request.Path.Value ?? string.Empty;
        var autenticado = contexto.User.Identity?.IsAuthenticated ?? false;

        if (!AuditoriaOperacoesRegras.DeveRegistrar(
            metodo, caminho, contexto.Response.StatusCode, autenticado))
            return;

        var idTexto = contexto.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var perfil = contexto.User.FindFirst(ClaimTypes.Role)?.Value;
        if (!int.TryParse(idTexto, out var usuarioId) || usuarioId <= 0 ||
            string.IsNullOrWhiteSpace(perfil))
            return;

        var evento = new RegistroAuditoria
        {
            DataUtc = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Perfil = perfil.Length > 20 ? perfil[..20] : perfil,
            Metodo = metodo.ToUpperInvariant(),
            Caminho = caminho.Length > 350 ? caminho[..350] : caminho,
            StatusHttp = contexto.Response.StatusCode,
            CorrelacaoId = contexto.TraceIdentifier.Length > 100
                ? contexto.TraceIdentifier[..100] : contexto.TraceIdentifier
        };

        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var banco = escopo.ServiceProvider.GetRequiredService<DistribuidoraDbContext>();
            banco.RegistrosAuditoria.Add(evento);
            await banco.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Falha ao registrar auditoria de requisicao {Metodo} {Caminho}. Operacao original nao foi revertida.",
                metodo, caminho);
        }
    }
}
