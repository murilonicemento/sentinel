using AlertOrchestrator.Application.Interfaces.Cooldown;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace AlertOrchestrator.Infrastructure.Cooldown;

public sealed class DistributedAlertCooldownService : IAlertCooldownService
{
    private readonly IDistributedCache _cache;

    public DistributedAlertCooldownService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> IsInCooldownAsync(string region, string riskType, string? tenantId, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(region, riskType, tenantId);
        var value = await _cache.GetStringAsync(key, cancellationToken);
        return !string.IsNullOrEmpty(value);
    }

    public async Task RecordAlertAsync(string region, string riskType, string? tenantId, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(region, riskType, tenantId);
        var cooldownData = new CooldownData
        {
            Region = region,
            RiskType = riskType,
            TenantId = tenantId,
            TriggeredAt = DateTime.UtcNow
        };

        var options = new DistributedCacheEntryOptions
        {
            // Default cooldown: 15 minutes
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        };

        await _cache.SetStringAsync(
            key,
            JsonSerializer.Serialize(cooldownData),
            options,
            cancellationToken);
    }

    public async Task<TimeSpan?> GetRemainingCooldownAsync(string region, string riskType, string? tenantId, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(region, riskType, tenantId);
        var value = await _cache.GetStringAsync(key, cancellationToken);
        
        if (string.IsNullOrEmpty(value))
            return null;

        var data = JsonSerializer.Deserialize<CooldownData>(value);
        if (data == null)
            return null;

        // Calculate remaining cooldown (assuming 15 min default)
        var elapsed = DateTime.UtcNow - data.TriggeredAt;
        var remaining = TimeSpan.FromMinutes(15) - elapsed;
        
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    private static string BuildCacheKey(string region, string riskType, string? tenantId)
    {
        var tenant = tenantId ?? "default";
        return $"alert_cooldown:{region}:{riskType}:{tenant}";
    }

    private sealed class CooldownData
    {
        public string Region { get; set; } = string.Empty;
        public string RiskType { get; set; } = string.Empty;
        public string? TenantId { get; set; }
        public DateTime TriggeredAt { get; set; }
    }
}
