using MataHR.Workforce.Contracts;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Emailing;

namespace MataHR.Workforce.Host;
public class EmailRecoveryDelivery(IEmailSender sender, IConfiguration config) : IAccountRecoveryDelivery, ITransientDependency
{
    public bool IsConfigured => config.GetValue<bool>("Auth:RecoveryEnabled") && Uri.TryCreate(config["Auth:PasswordResetUrl"], UriKind.Absolute, out var uri) && uri.Scheme == "https";

    public Task SendAsync(Guid tenantId, string email, string token)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Recovery delivery not configured.");
        var url = config["Auth:PasswordResetUrl"]! + "?tenantId=" + tenantId + "&email=" + Uri.EscapeDataString(email) + "&token=" + Uri.EscapeDataString(token);
        return sender.SendAsync(email, "MataHR password reset", "Reset your MataHR password using this link: " + url, isBodyHtml: false);
    }
}
