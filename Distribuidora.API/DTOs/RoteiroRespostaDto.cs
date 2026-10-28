namespace Distribuidora.API.DTOs;

public sealed record ClienteRoteiroDto(
    int Id,
    string RazaoSocial,
    string Cidade,
    string Endereco,
    string Telefone,
    int OrdemVisita
);

public sealed record RoteiroRespostaDto(
    int VendedorId,
    string VendedorNome,
    IReadOnlyList<ClienteRoteiroDto> Clientes
);
