using Distribuidora.API.Services;
using Xunit;

namespace Distribuidora.Tests;

public sealed class VendaRegrasTests
{
    [Fact]
    public void ValorConsideraSomenteQuantidadeAtendida()
    {
        var itens = new[] { (quantidade: 3, preco: 12.50m), (quantidade: 0, preco: 20m) };
        Assert.Equal(37.50m, VendaRegras.TotalAtendido(itens));
    }

    [Fact]
    public void ImpedeVendaQuandoNenhumaUnidadeFoiAtendida()
    {
        Assert.False(VendaRegras.QuantidadesValidas(new[] { (solicitada: 8, atendida: 0) }));
    }

    [Fact]
    public void AceitaAtendimentoParcial()
    {
        Assert.True(VendaRegras.QuantidadesValidas(new[] { (solicitada: 8, atendida: 3) }));
    }

    [Fact]
    public void RejeitaAtendimentoAcimaDoSolicitado()
    {
        Assert.False(VendaRegras.QuantidadesValidas(new[] { (solicitada: 8, atendida: 9) }));
    }

    [Fact]
    public void RejeitaPrecoNegativo()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            VendaRegras.TotalAtendido(new[] { (quantidade: 3, preco: -1m) }));
    }
}
