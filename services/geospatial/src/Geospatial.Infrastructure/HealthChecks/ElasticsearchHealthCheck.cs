using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nest;

namespace Geospatial.Infrastructure.HealthChecks;

public static class GeospatialHealthChecks
{
    /// <summary>
    /// Elasticsearch backs only the event search; geometry operations work without it, so the check is reported on
    /// <c>/health</c> without gating readiness.
    /// </summary>
    public static IHealthChecksBuilder AddGeospatialHealthChecks(this IHealthChecksBuilder builder) =>
        builder.AddCheck<ElasticsearchHealthCheck>("elasticsearch", HealthStatus.Degraded, ["search"],
            TimeSpan.FromSeconds(3));
}

internal sealed class ElasticsearchHealthCheck(IElasticClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var response = await client.PingAsync(ct: cancellationToken);
        return response.IsValid
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "Elasticsearch did not answer the ping.");
    }
}
