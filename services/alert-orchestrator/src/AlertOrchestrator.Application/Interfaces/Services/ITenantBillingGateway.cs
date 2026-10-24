namespace AlertOrchestrator.Application.Interfaces.Services;

public interface ITenantBillingGateway
{
    Task<bool> ValidateTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
