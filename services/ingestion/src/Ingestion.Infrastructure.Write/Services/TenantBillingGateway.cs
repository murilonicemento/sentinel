using System.Net.Http.Json;
using Ingestion.Application.Interfaces.Services;

namespace Ingestion.Infrastructure.Write.Services;

public sealed class TenantBillingGateway : ITenantBillingGateway
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantBillingGateway> _logger;

    public TenantBillingGateway(HttpClient httpClient, ILogger<TenantBillingGateway> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> ValidateTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/tenants/{tenantId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tenant validation rejected by billing service for tenant {TenantId}. Status: {StatusCode}", tenantId, response.StatusCode);
                return false;
            }

            var tenant = await response.Content.ReadFromJsonAsync<TenantBillingTenantResponse>(cancellationToken: cancellationToken);
            return tenant is not null && tenant.Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate tenant {TenantId} against Tenants Billing service.", tenantId);
            return false;
        }
    }

    private sealed record TenantBillingTenantResponse(string Status);
}
