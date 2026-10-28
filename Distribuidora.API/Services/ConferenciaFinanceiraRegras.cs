using Distribuidora.API.DTOs;

namespace Distribuidora.API.Services;

// Conferencia de consistencia apenas leitura, sem corrigir dados automaticamente.
public static class ConferenciaFinanceiraRegras
{
    public sealed record ParcelaEntrada(
        int ParcelaId, int Numero, decimal ValorOriginal,
        decimal ValorPago, decimal SomaRecebimentos);

    public static ContaAuditoriaDto Conferir(
        int contaId, int vendaId, decimal valorConta, decimal valorVenda,
        IReadOnlyCollection<ParcelaEntrada> parcelas)
    {
        var alertas = new List<string>();
        var linhas = new List<ParcelaAuditoriaDto>();
        decimal somaOriginal = 0m, somaPago = 0m, somaRecebimentos = 0m;
        var numeros = new HashSet<int>();

        if (valorConta <= 0m)
            alertas.Add("Valor da conta deve ser positivo.");
        if (valorConta != valorVenda)
            alertas.Add("Valor da conta difere do valor da venda.");
        if (parcelas.Count == 0)
            alertas.Add("Conta sem parcelas.");

        foreach (var p in parcelas.OrderBy(x => x.Numero).ThenBy(x => x.ParcelaId))
        {
            var avisos = new List<string>();
            if (p.Numero <= 0 || !numeros.Add(p.Numero))
                avisos.Add("Numero de parcela invalido ou duplicado.");
            if (p.ValorOriginal <= 0m)
                avisos.Add("Valor original da parcela deve ser positivo.");
            if (p.ValorPago < 0m || p.ValorPago > p.ValorOriginal)
                avisos.Add("Valor pago fora do intervalo de 0 ate o valor original.");
            if (p.SomaRecebimentos != p.ValorPago)
                avisos.Add("Soma de recebimentos diverge do ValorPago da parcela.");

            somaOriginal += p.ValorOriginal;
            somaPago += p.ValorPago;
            somaRecebimentos += p.SomaRecebimentos;
            linhas.Add(new ParcelaAuditoriaDto(
                p.ParcelaId, p.Numero, p.ValorOriginal, p.ValorPago,
                p.SomaRecebimentos, p.ValorOriginal - p.ValorPago, avisos));
        }

        if (somaOriginal != valorConta)
            alertas.Add("Soma das parcelas difere do valor da conta.");
        if (linhas.Any(x => x.Alertas.Count > 0))
            alertas.Add("Ha parcelas com inconsistencias.");

        return new ContaAuditoriaDto(
            contaId, vendaId, valorConta, valorVenda, somaOriginal,
            somaPago, somaRecebimentos, somaOriginal - somaPago,
            alertas.Count == 0, alertas, linhas);
    }
}
