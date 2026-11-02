using System.Net.Http.Json;
using System.Text.Json;
using Ingestion.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

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
            var response = await _httpClient.GetAsync($"/api/v1/tenants-billing/tenants/{tenantId}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tenant validation rejected by billing service for tenant {TenantId}. Status: {StatusCode}", tenantId, response.StatusCode);
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return TenantStatusResponse.IsActive(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate tenant {TenantId} against Tenants Billing service.", tenantId);
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
