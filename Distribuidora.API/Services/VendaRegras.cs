namespace Distribuidora.API.Services;

// Validacoes puras para impedir vendas de itens nao atendidos.
public static class VendaRegras
{
    public static bool QuantidadesValidas(IEnumerable<(int solicitada, int atendida)> itens)
    {
        var lista = itens.ToList();
        return lista.Count > 0 &&
               lista.All(i => i.solicitada > 0 && i.atendida >= 0 && i.atendida <= i.solicitada) &&
               lista.Any(i => i.atendida > 0);
    }

    public static decimal TotalAtendido(IEnumerable<(int quantidade, decimal preco)> itens)
    {
        decimal total = 0;
        foreach (var (quantidade, preco) in itens)
        {
            if (quantidade < 0 || preco < 0)
                throw new ArgumentOutOfRangeException(nameof(itens), "Valores negativos nao sao aceitos.");
            total = checked(total + quantidade * preco);
        }
        return total;
    }
}
