namespace Distribuidora.API.DTOs;

public sealed record VendedorRespostaDto(
    int Id,
    string Nome,
    string Email,
    string Telefone,
    bool Ativo
);
