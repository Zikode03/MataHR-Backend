using Volo.Abp.Auditing;

namespace MataHR.Workforce.Host;
public sealed class RedactedAuditContributor : AuditLogContributor
{
    public override void PostContribute(AuditLogContributionContext context)
    {
        foreach (var action in context.AuditInfo.Actions)
            action.Parameters = "{}";
    }
}
