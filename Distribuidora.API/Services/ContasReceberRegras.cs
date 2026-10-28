namespace Distribuidora.API.Services;

public static class ContasReceberRegras
{
    public static bool ValorMonetarioPositivo(decimal valor) =>
        valor > 0m && valor == decimal.Round(valor, 2);

    public static bool PagamentoCabe(decimal valorOriginal, decimal valorPago, decimal novoPagamento) =>
        ValorMonetarioPositivo(novoPagamento) &&
        valorOriginal >= 0m && valorPago >= 0m && valorPago <= valorOriginal &&
        novoPagamento <= valorOriginal - valorPago;

    public static decimal SaldoDevedor(decimal valorOriginal, decimal valorPago) =>
        valorOriginal - valorPago;
}
