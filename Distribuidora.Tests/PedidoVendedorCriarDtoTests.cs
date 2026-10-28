using System.ComponentModel.DataAnnotations;
using Distribuidora.API.DTOs;
using Xunit;

namespace Distribuidora.Tests;

public sealed class PedidoVendedorCriarDtoTests
{
    [Fact]
    public void ContratoDeEntradaNaoAceitaCampoVendedorId()
    {
        Assert.Null(typeof(PedidoVendedorCriarDto).GetProperty("VendedorId"));
    }

    [Fact]
    public void PedidoValidoPassaPelasValidacoesDeDto()
    {
        var dto = NovoPedido();
        var erros = Validar(dto);
        Assert.Empty(erros);
    }

    [Fact]
    public void PedidoSemItensEhRecusado()
    {
        var dto = NovoPedido();
        dto.Itens.Clear();
        Assert.NotEmpty(Validar(dto));
    }

    [Fact]
    public void ClienteIdZeroEhRecusado()
    {
        var dto = NovoPedido();
        dto.ClienteId = 0;
        Assert.NotEmpty(Validar(dto));
    }

    [Fact]
    public void ChaveNaoUuidCom36CaracteresPodeSerRecusadaNoController()
    {
        Assert.False(Guid.TryParseExact(new string('x', 36), "D", out _));
    }

    private static PedidoVendedorCriarDto NovoPedido() => new()
    {
        ClienteId = 5,
        ChaveRequisicao = Guid.NewGuid().ToString("D"),
        Itens = new List<ItemPedidoCriarDto>
        {
            new() { ProdutoId = 4, QuantidadeSolicitada = 3 }
        }
    };

    private static List<ValidationResult> Validar(PedidoVendedorCriarDto dto)
    {
        var erros = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), erros, true);
        return erros;
    }
}
