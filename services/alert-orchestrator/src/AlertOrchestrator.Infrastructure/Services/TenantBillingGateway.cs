using AlertOrchestrator.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Services;

public class TenantBillingGateway : ITenantBillingGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantBillingGateway> _logger;
    private readonly string _baseUrl;

    public TenantBillingGateway(HttpClient httpClient, ILogger<TenantBillingGateway> logger, string baseUrl)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<bool> ValidateTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_baseUrl}/api/v1/tenants/{tenantId}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
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
