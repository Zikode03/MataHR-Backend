using Volo.Abp;

namespace MataHR.Workforce;
public class WorkforceRuleException(string code, string message) : BusinessException("MataHR:" + code, message)
{
}

public static class DomainGuard
{
    public static void Against(bool invalid, string code, string message)
    {
        if (invalid)
            throw new WorkforceRuleException(code, message);
    }

    public static string Text(string value, string field, int max)
    {
        Against(string.IsNullOrWhiteSpace(value) || value.Length > max, "Invalid" + field, field + " is required and must not exceed " + max + " characters.");
        return value.Trim();
    }
}

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

public readonly record struct EmploymentPeriod
{
    public DateOnly Start { get; }
    public DateOnly? End { get; }

    public EmploymentPeriod(DateOnly start, DateOnly? end)
    {
        DomainGuard.Against(start == default || end < start, "InvalidEmploymentPeriod", "Employment end cannot precede start.");
        Start = start;
        End = end;
    }

    public bool Contains(DateOnly day) => day >= Start && (!End.HasValue || day < End);
}

public readonly record struct EmployeeNumber
{
    public string Value { get; }

    public EmployeeNumber(string value) => Value = DomainGuard.Text(value, "EmployeeNumber", 64);
    public override string ToString() => Value;
}

public enum ContractState
{
    Draft,
    InReview,
    Approved,
    AwaitingSignature,
    Signed
}

public enum LeaveState
{
    Pending,
    Approved,
    Rejected,
    Withdrawn,
    CancellationPending,
    Cancelled
}

public enum ApprovalStage
{
    Manager,
    Hr,
    Complete
}

public enum PayRunState
{
    Draft,
    Reviewed,
    Approved
}

public enum AttendanceState
{
    Open,
    PendingReview,
    Approved,
    Rejected
}

public enum VerificationPurpose
{
    General,
    UnsignedContract,
    SignedContract,
    Payslip
}
