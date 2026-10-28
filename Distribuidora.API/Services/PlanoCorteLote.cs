namespace Distribuidora.API.Services;

public static class PlanoCorteLote
{
    // Conjunto de IDs precisa ser exatamente igual; nao aceita duplicidade.
    public static bool MesmosPedidos(IEnumerable<int> esperados, IEnumerable<int> recebidos)
    {
        var listaEsperados = esperados.ToArray();
        var listaRecebidos = recebidos.ToArray();
        return listaEsperados.Length == listaRecebidos.Length &&
               listaEsperados.Distinct().Count() == listaEsperados.Length &&
               listaRecebidos.Distinct().Count() == listaRecebidos.Length &&
               listaEsperados.OrderBy(id => id).SequenceEqual(listaRecebidos.OrderBy(id => id));
    }

    public static bool MesmosProdutos(IEnumerable<int> esperados, IEnumerable<int> recebidos)
        => MesmosPedidos(esperados, recebidos);
}
