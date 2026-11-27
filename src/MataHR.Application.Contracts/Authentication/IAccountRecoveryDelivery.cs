namespace MataHR.Authentication.Contracts;

// Keep the email provider outside the security workflow so delivery can be configured or replaced independently.
public interface IAccountRecoveryDelivery
{
    bool IsConfigured { get; }

    Task SendAsync(Guid tenantId, string email, string token);
}
