using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RiskCatalog.Application.Interfaces;
using StackExchange.Redis;

namespace RiskCatalog.Infrastructure.Cache;

public class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisCacheService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisCacheService> logger,
        IConfiguration configuration)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _database = connectionMultiplexer.GetDatabase();
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // ConfigureRedisPolicies(configuration);
        RegisterCallbacks();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var cachedValue = await _database.StringGetAsync(key);

            if (!cachedValue.HasValue)
            {
                _logger.LogDebug("Cache miss for key: {key}", key);

                return null;
            }

            _logger.LogDebug("Cache hit for key: {key}", key);

            return JsonSerializer.Deserialize<T>(cachedValue!, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get value from cache for key: {key}", key);

            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
            var defaultExpiration = expiration ?? TimeSpan.FromHours(1);

            await _database.StringSetAsync(key, serializedValue, defaultExpiration);

            _logger.LogDebug(
                "Value cached successfully. Key: {key}, Expiration: {expiration}",
                key,
                defaultExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set value in cache for key: {key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.KeyDeleteAsync(key);

            _logger.LogDebug("Key removed from cache: {key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove key from cache: {key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
            var keysLength = 0;

            await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: 20)
                               .WithCancellation(cancellationToken))
            {
                await _database.KeyDeleteAsync(key);
                keysLength++;
            }

            if (keysLength > 0)
            {
                _logger.LogInformation(
                    "Removed {count} keys from cache matching pattern: {pattern}",
                    keysLength,
                    pattern);
            }
            else
            {
                _logger.LogDebug("No keys found matching pattern: {pattern}", pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove keys by pattern from cache: {pattern}", pattern);
            throw new Exception("An error ocurred.", ex);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if key exists in cache: {key}", key);
            return false;
        }
    }

    private void ConfigureRedisPolicies(IConfiguration configuration)
    {
        var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
        var maxMemory = configuration["Redis:MaxMemory"] ?? "256mb";
        var evictionPolicy = configuration["Redis:EvictionPolicy"] ?? "allkeys-lru";

        try
        {
            server.ConfigSet("maxmemory", maxMemory);
            server.ConfigSet("maxmemory-policy", evictionPolicy);

            _logger.LogInformation(
                "Redis cache policies configured. MaxMemory: {maxMemory}, EvictionPolicy: {evictionPolicy}",
                maxMemory,
                evictionPolicy);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to configure Redis policies. Using default settings. MaxMemory: {maxMemory}, EvictionPolicy: {evictionPolicy}",
                maxMemory,
                evictionPolicy);
        }
    }

    private void RegisterCallbacks()
    {
        _connectionMultiplexer.ConnectionFailed += (sender, e) =>
        {
            _logger.LogError(
                "Redis connection failed. EndPoint: {endPoint}, FailureType: {failureType}",
                e.EndPoint,
                e.FailureType);
        };
        _connectionMultiplexer.ConnectionRestored += (sender, e) =>
        {
            _logger.LogInformation(
                "Redis connection restored. EndPoint: {endPoint}",
                e.EndPoint);
        };
        _connectionMultiplexer.ErrorMessage += (sender, e) =>
        {
            _logger.LogError(
                "Redis error message. EndPoint: {endPoint}, Message: {message}",
                e.EndPoint,
                e.Message);
        };
    }
}