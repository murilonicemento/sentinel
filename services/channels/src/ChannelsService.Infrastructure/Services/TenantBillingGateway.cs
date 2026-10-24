using ChannelsService.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Services;

/// <summary>
/// HTTP client implementation for validating tenant billing status with the Tenants Billing service.
/// </summary>
public class TenantBillingGateway : ITenantBillingGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantBillingGateway> _logger;

    public TenantBillingGateway(HttpClient httpClient, ILogger<TenantBillingGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Validates if a tenant is active and authorized by calling the Tenants Billing service.
    /// </summary>
    public async Task<bool> ValidateTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.LogWarning("ValidateTenantAsync called with empty tenantId");
            return false;
        }

        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/tenants/{tenantId}", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Tenant {TenantId} validated successfully", tenantId);
                return true;
            }

            _logger.LogWarning("Tenant {TenantId} validation failed with status {StatusCode}", tenantId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating tenant {TenantId}", tenantId);
            return false;
        }
    }
}
