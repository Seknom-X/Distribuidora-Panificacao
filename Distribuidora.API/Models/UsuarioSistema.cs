namespace Distribuidora.API.Models;

public sealed class UsuarioSistema
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Perfil { get; set; } = PerfisSistema.Vendedor;
    public bool Ativo { get; set; } = true;
    public int? VendedorId { get; set; }
    public Vendedor? Vendedor { get; set; }
}

public static class PerfisSistema
{
    public const string Admin = "Admin";
    public const string Vendedor = "Vendedor";
}
