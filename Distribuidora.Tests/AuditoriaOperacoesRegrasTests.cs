using Distribuidora.API.Services;
using Xunit;

namespace Distribuidora.Tests;

public sealed class AuditoriaOperacoesRegrasTests
{
    [Theory]
    [InlineData("POST", "/api/vendas/do-pedido/1", 200)]
    [InlineData("POST", "/api/pedidos", 201)]
    [InlineData("PUT", "/api/clientes/1", 200)]
    [InlineData("PATCH", "/api/vendedores/1/desativar", 200)]
    [InlineData("DELETE", "/api/clientes/1", 204)]
    public void RegistraOperacaoAutenticadaComSucesso(string metodo, string caminho, int status)
    {
        Assert.True(AuditoriaOperacoesRegras.DeveRegistrar(metodo, caminho, status, true));
    }

    [Theory]
    [InlineData("GET", "/api/vendas/1", 200, true)]
    [InlineData("POST", "/api/vendas/do-pedido/1", 401, false)]
    [InlineData("POST", "/api/vendas/do-pedido/1", 403, true)]
    [InlineData("POST", "/api/vendas/do-pedido/1", 409, true)]
    [InlineData("POST", "/api/auth/login", 200, true)]
    [InlineData("POST", "/api/admin/registros-auditoria", 201, true)]
    [InlineData("POST", "/status", 200, true)]
    public void NaoRegistraConsultasErrosNemLogin(string metodo, string caminho, int status, bool autenticado)
    {
        Assert.False(AuditoriaOperacoesRegras.DeveRegistrar(metodo, caminho, status, autenticado));
    }
}
