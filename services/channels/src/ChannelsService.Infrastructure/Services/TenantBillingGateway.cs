using ChannelsService.Application.Interfaces.Services;
using System.Net.Http.Json;
using System.Text.Json;
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
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tenant {TenantId} validation failed with status {StatusCode}", tenantId,
                    response.StatusCode);
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var isActive = TenantStatusResponse.IsActive(body);
            if (!isActive)
                _logger.LogWarning("Tenant {TenantId} is not active", tenantId);

            return isActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating tenant {TenantId}", tenantId);
            return false;
        }
    }
}

/// <summary>
/// Reads the tenant status returned by tenants-billing (TB-09). The legacy route serializes the status as a number
/// ({"status": 1}); the v1 route wraps a string status in an envelope ({"data": {"status": "Active"}}).
/// </summary>
public static class TenantStatusResponse
{
    // TenantStatus.Active in tenants-billing.
    private const int ActiveStatusValue = 1;

    public static bool IsActive(JsonElement body)
    {
        if (body.ValueKind == JsonValueKind.Object && TryGetProperty(body, "data", out var data) &&
            data.ValueKind == JsonValueKind.Object)
            body = data;

        if (body.ValueKind != JsonValueKind.Object || !TryGetProperty(body, "status", out var status)) return false;

        return status.ValueKind switch
        {
            JsonValueKind.String => string.Equals(status.GetString(), "Active", StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => status.TryGetInt32(out var value) && value == ActiveStatusValue,
            _ => false
        };
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }
}
