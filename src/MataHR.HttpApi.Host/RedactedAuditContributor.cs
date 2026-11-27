using Volo.Abp.Auditing;

namespace MataHR.HttpApi.Host;

// Keep useful audit metadata while excluding request parameters and sensitive values from the audit record.
public sealed class RedactedAuditContributor : AuditLogContributor
{
    public override void PostContribute(AuditLogContributionContext context)
    {
        foreach (var action in context.AuditInfo.Actions)
            action.Parameters = "{}";
    }
}
