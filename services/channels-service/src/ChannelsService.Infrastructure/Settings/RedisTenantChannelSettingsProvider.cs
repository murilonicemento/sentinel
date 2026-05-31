using System.Text.Json;
using System.Text.Json.Serialization;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.Settings;

public sealed class RedisTenantChannelSettingsProvider : ITenantChannelSettingsProvider
{
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ConfigurationTenantChannelSettingsProvider _inner;
    private readonly ILogger<RedisTenantChannelSettingsProvider> _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly TimeSpan _cacheTtl;

    public RedisTenantChannelSettingsProvider(
        IDistributedCache cache,
        IConfiguration configuration,
        ConfigurationTenantChannelSettingsProvider inner,
        ILogger<RedisTenantChannelSettingsProvider> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _inner = inner;
        _logger = logger;

        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var ttlSeconds = configuration.GetValue<int?>("Redis:TenantSettingsCacheTtlSeconds") ?? 300;
        _cacheTtl = TimeSpan.FromSeconds(ttlSeconds);
    }

    public TenantChannelSettings GetSettings(string tenantId)
    {
        var cacheKey = GetCacheKey(tenantId);
        try
        {
            var cachedValue = _cache.GetString(cacheKey);
            if (!string.IsNullOrWhiteSpace(cachedValue))
            {
                var settings = JsonSerializer.Deserialize<TenantChannelSettings>(cachedValue, _serializerOptions);
                if (settings != null)
                {
                    _logger.LogDebug("Loaded tenant settings for {TenantId} from Redis cache.", tenantId);
                    return settings;
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to read tenant settings for {TenantId} from Redis cache. Falling back to configuration.", tenantId);
        }

        var resolvedSettings = _inner.GetSettings(tenantId);

        try
        {
            var serialized = JsonSerializer.Serialize(resolvedSettings, _serializerOptions);
            _cache.SetString(cacheKey, serialized, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _cacheTtl
            });
            _logger.LogDebug("Cached tenant settings for {TenantId} in Redis for {TtlSeconds}s.", tenantId, _cacheTtl.TotalSeconds);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to cache tenant settings for {TenantId} in Redis.", tenantId);
        }

        return resolvedSettings;
    }

    private static string GetCacheKey(string tenantId) => $"ChannelsService:TenantSettings:{tenantId}";
}
