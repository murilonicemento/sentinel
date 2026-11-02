namespace ChannelsService.Application.Interfaces.Services;

/// <summary>
/// Gateway interface for validating tenant billing status with the Tenants Billing service.
/// </summary>
public interface ITenantBillingGateway
{
    /// <summary>
    /// Validates if a tenant is active and authorized for operations.
    /// </summary>
    /// <param name="tenantId">The tenant identifier to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if tenant is active and authorized; otherwise false.</returns>
    Task<bool> ValidateTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
