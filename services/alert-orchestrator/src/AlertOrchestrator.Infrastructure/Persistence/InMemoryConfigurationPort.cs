using AlertOrchestrator.Application.DTOs;
using AlertOrchestrator.Application.Ports;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Persistence;

public sealed class InMemoryConfigurationPort : IAlertConfigurationPort
{
    private readonly IMemoryCache _cache;

    private readonly Dictionary<string, TriggerRuleConfiguration> _defaultConfigurations = new()
    {
        ["flood"] = new TriggerRuleConfiguration(0.7, TimeSpan.FromMinutes(30), 2, 1, false, false),
        ["wildfire"] = new TriggerRuleConfiguration(0.8, TimeSpan.FromMinutes(15), 3, 2, true, true),
        ["earthquake"] = new TriggerRuleConfiguration(0.6, TimeSpan.FromMinutes(10), 2, 1, true, false),
        ["landslide"] = new TriggerRuleConfiguration(0.75, TimeSpan.FromMinutes(20), 2, 1, true, true),
        ["storm"] = new TriggerRuleConfiguration(0.65, TimeSpan.FromMinutes(25), 2, 1, false, true),
        ["heatwave"] = new TriggerRuleConfiguration(0.7, TimeSpan.FromMinutes(60), 2, 1, false, false)
    };

    private readonly ILogger<InMemoryConfigurationPort> _logger;

    public InMemoryConfigurationPort(IMemoryCache cache, ILogger<InMemoryConfigurationPort> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<TriggerRuleConfiguration> GetConfigurationAsync(string riskType, string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"config:{tenantId ?? "default"}:{riskType.ToLowerInvariant()}";

        if (_cache.TryGetValue(cacheKey, out TriggerRuleConfiguration? cachedConfig) && cachedConfig is not null)
        {
            _logger.LogDebug("Configuration cache hit for {RiskType}/{TenantId}", riskType, tenantId ?? "default");
            return Task.FromResult(cachedConfig);
        }

        var config = _defaultConfigurations.GetValueOrDefault(
            riskType.ToLowerInvariant(),
            new TriggerRuleConfiguration(
                0.75,
                TimeSpan.FromMinutes(30),
                2,
                1,
                false,
                false,
                EscalationIntervals: new List<TimeSpan> { TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15) }));

        _cache.Set(cacheKey, config, TimeSpan.FromMinutes(5));

        _logger.LogDebug(
            "Configuration loaded for {RiskType}/{TenantId}: Threshold={Threshold}, Window={WindowDuration}",
            riskType, tenantId ?? "default", config.Threshold, config.WindowDuration);

        return Task.FromResult(config);
    }
}