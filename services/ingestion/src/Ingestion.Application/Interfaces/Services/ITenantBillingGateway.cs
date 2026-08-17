namespace Ingestion.Application.Interfaces.Services;

public interface ITenantBillingGateway
{
    Task<bool> ValidateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
