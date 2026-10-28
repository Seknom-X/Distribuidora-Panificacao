using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class LoginDto
{
    [Required, EmailAddress, MaxLength(180)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Senha { get; set; } = string.Empty;
}

public sealed class CriarUsuarioVendedorDto
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Nome { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(180)]    
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string Senha { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int VendedorId { get; set; }
}

