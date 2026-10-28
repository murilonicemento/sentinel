using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

namespace RiskEvaluation.Infrastructure.HealthChecks;

public static class RiskEvaluationHealthChecks
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>MongoDB (evaluations, catalog snapshots) and Redis (score cache, consumer claims).</summary>
    public static IHealthChecksBuilder AddRiskEvaluationReadinessChecks(this IHealthChecksBuilder builder, string tag) =>
        builder
            .AddCheck<MongoHealthCheck>("mongodb", HealthStatus.Unhealthy, [tag], Timeout)
            .AddCheck<RedisHealthCheck>("redis", HealthStatus.Unhealthy, [tag], Timeout);

    internal sealed class MongoHealthCheck(IMongoClient client) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await client.GetDatabase("admin")
                    .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException)
            {
                return HealthCheckResult.Unhealthy("MongoDB did not answer the ping.", exception);
            }
        }
    }

    internal sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                return HealthCheckResult.Unhealthy("Redis did not answer the ping.", exception);
            }
        }
    }
}
