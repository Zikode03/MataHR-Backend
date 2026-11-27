namespace MataHR.Common;

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
