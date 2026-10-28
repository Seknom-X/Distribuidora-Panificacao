
namespace Distribuidora.API.Models;

public class Vendedor
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;

    public ICollection<Cliente> Clientes { get; set; }
        = new List<Cliente>();
}
