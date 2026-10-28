using System.ComponentModel.DataAnnotations;

namespace Distribuidora.API.DTOs;

public sealed class ClienteSalvarDto
{
    [Required(ErrorMessage = "Informe a razao social.")]
    [StringLength(180, MinimumLength = 3)]
    public string RazaoSocial { get; set; } = string.Empty;

    // Formato simples do MVP: exatamente 14 digitos sem pontuacao.
    // Calculo dos digitos verificadores sera implementado depois.
    [Required(ErrorMessage = "Informe o CNPJ.")]
    [RegularExpression(@"^[0-9]{14}$",
        ErrorMessage = "O CNPJ deve conter exatamente 14 digitos, sem pontuacao.")]
    public string Cnpj { get; set; } = string.Empty;

    [StringLength(30)]
    public string Telefone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o endereco.")]
    [StringLength(250)]
    public string Endereco { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a cidade.")]
    [StringLength(120)]
    public string Cidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o estado.")]
    [RegularExpression(@"^[a-zA-Z]{2}$",
        ErrorMessage = "Informe a sigla do estado com duas letras, ex.: SP.")]
    public string Estado { get; set; } = string.Empty;

    [Range(0, int.MaxValue,
        ErrorMessage = "A ordem da visita nao pode ser negativa.")]
    public int OrdemVisita { get; set; }

    [Range(1, int.MaxValue,
        ErrorMessage = "Informe um VendedorId valido.")]
    public int VendedorId { get; set; }
}
