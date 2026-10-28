namespace Distribuidora.API.Services;

public static class AuditoriaOperacoesRegras
{
    public static bool DeveRegistrar(string metodo, string? caminho, int statusHttp, bool autenticado)
    {
        if (!autenticado || statusHttp is < 200 or >= 300 || string.IsNullOrWhiteSpace(caminho))
            return false;

        if (!caminho.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            caminho.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            caminho.StartsWith("/api/admin/registros-auditoria", StringComparison.OrdinalIgnoreCase))
            return false;

        return metodo.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
               metodo.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
               metodo.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
               metodo.Equals("DELETE", StringComparison.OrdinalIgnoreCase);
    }
}
