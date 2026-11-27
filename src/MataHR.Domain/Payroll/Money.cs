using MataHR.Common;
namespace MataHR.Payroll;

public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "ZAR")
    {
        DomainGuard.Against(amount < 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount, "InvalidMoney", "Use a nonnegative monetary amount with at most two decimal places.");
        DomainGuard.Against(currency != "ZAR", "InvalidCurrency", "Only ZAR is configured.");
        Amount = amount;
        Currency = currency;
    }
}
