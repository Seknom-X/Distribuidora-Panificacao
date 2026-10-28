using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

// A ordem da lista define as posicoes 1, 2, 3 ... no roteiro.
public sealed class ReordenarRoteiroDto
{
    [Required(ErrorMessage = "Informe clienteIds (use [] se o vendedor nao possuir clientes ativos).")]
    public List<int>? ClienteIds { get; set; }
}
