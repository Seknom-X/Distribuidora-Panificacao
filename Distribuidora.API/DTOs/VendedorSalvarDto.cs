using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class VendedorSalvarDto
{
    [Required(ErrorMessage = "O nome e obrigatorio.")]
    [StringLength(150, MinimumLength = 3,
        ErrorMessage = "O nome deve conter entre 3 e 150 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail e obrigatorio.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
    [StringLength(180, ErrorMessage = "O e-mail deve ter no maximo 180 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "O telefone deve ter no maximo 30 caracteres.")]
    public string Telefone { get; set; } = string.Empty;
}
