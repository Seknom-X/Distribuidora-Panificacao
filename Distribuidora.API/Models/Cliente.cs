namespace Distribuidora.API.Models;

public class Cliente
{
    public int Id { get; set; }
    public string RazaoSocial { get; set; } = string.Empty;
    public string Cnpj { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;

    // Zero = cliente ainda nao possui posicao definida no roteiro.
    public int OrdemVisita { get; set; }

    // Desativacao logica: preservar historico de clientes e pedidos.
    public bool Ativo { get; set; } = true;

    public int VendedorId { get; set; }
    public Vendedor Vendedor { get; set; } = null!;
}
