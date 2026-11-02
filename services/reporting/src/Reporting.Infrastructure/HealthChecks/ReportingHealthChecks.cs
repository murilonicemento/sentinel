using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Reporting.Infrastructure.HealthChecks;

public static class ReportingHealthChecks
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>Elasticsearch and Redis, the stores every reporting query depends on.</summary>
    public static IHealthChecksBuilder AddReportingReadinessChecks(this IHealthChecksBuilder builder, string tag) =>
        builder
            .AddCheck<ElasticsearchHealthCheck>("elasticsearch", HealthStatus.Unhealthy, [tag], Timeout)
            .AddCheck<RedisHealthCheck>("redis", HealthStatus.Unhealthy, [tag], Timeout);

    internal sealed class ElasticsearchHealthCheck(ElasticsearchClient client) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var response = await client.PingAsync(cancellationToken);
            return response.IsValidResponse
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Elasticsearch did not answer the ping.");
        }
    }

    internal sealed class RedisHealthCheck(IDatabase database) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await database.PingAsync().WaitAsync(cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception exception) when (exception is RedisException or TimeoutException)
            {
                return HealthCheckResult.Unhealthy("Redis did not answer the ping.", exception);
            }
        }
    }
}
