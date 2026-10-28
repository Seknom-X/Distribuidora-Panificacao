namespace Distribuidora.API.DTOs;

public sealed record ClienteRespostaDto(
    int Id,
    string RazaoSocial,
    string Cnpj,
    string Telefone,
    string Endereco,
    string Cidade,
    string Estado,
    int OrdemVisita,
    int VendedorId,
    string VendedorNome,
    bool Ativo
);
