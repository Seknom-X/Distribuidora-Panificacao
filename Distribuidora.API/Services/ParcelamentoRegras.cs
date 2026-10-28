using Distribuidora.API.DTOs;

namespace Distribuidora.API.Services;

public sealed record RegraParcela(int DiasAposVenda, decimal Percentual);

public static class ParcelamentoRegras
{
    public static bool DistribuicaoValida(IEnumerable<RegraParcela>? parcelas)
    {
        if (parcelas is null) return false;
        var lista = parcelas.ToList();
        if (lista.Count is < 1 or > 12) return false;
        if (lista.Any(p => p.DiasAposVenda is < 0 or > 365 ||
                           p.Percentual <= 0m || p.Percentual > 100m ||
                           p.Percentual != decimal.Round(p.Percentual, 2)))
            return false;
        if (lista.Select(p => p.DiasAposVenda).Distinct().Count() != lista.Count)
            return false;
        if (lista.Zip(lista.Skip(1)).Any(t => t.First.DiasAposVenda >= t.Second.DiasAposVenda))
            return false;
        return lista.Sum(p => p.Percentual) == 100m;
    }

    public static IReadOnlyList<ParcelaSimuladaDto> Simular(
        decimal valorTotal, DateOnly dataBase, IReadOnlyList<RegraParcela> parcelas)
    {
        if (valorTotal <= 0m || valorTotal != decimal.Round(valorTotal, 2))
            throw new ArgumentException("Valor total deve ser positivo e ter duas casas decimais.", nameof(valorTotal));
        if (!DistribuicaoValida(parcelas))
            throw new ArgumentException("Distribuicao de percentuais e dias invalida.", nameof(parcelas));

        var resultado = new List<ParcelaSimuladaDto>(parcelas.Count);
        decimal acumulado = 0m;
        for (var i = 0; i < parcelas.Count; i++)
        {
            var item = parcelas[i];
            decimal valor = i == parcelas.Count - 1
                ? valorTotal - acumulado
                : decimal.Round(valorTotal * item.Percentual / 100m, 2, MidpointRounding.AwayFromZero);
            if (valor <= 0m)
                throw new InvalidOperationException("Valor da venda e pequeno demais para essa divisao em parcelas.");
            acumulado += valor;
            resultado.Add(new ParcelaSimuladaDto(
                i + 1, item.DiasAposVenda, item.Percentual,
                dataBase.AddDays(item.DiasAposVenda), valor));
        }
        if (acumulado != valorTotal)
            throw new InvalidOperationException("Soma das parcelas divergente do valor da venda.");
        return resultado;
    }
}
