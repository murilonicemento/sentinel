using Ingestion.Infrastructure.Read.Persistence.DbContext;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace Ingestion.Infrastructure.Read.HealthChecks;

/// <summary>Pings the MongoDB read store that serves the detected-event queries.</summary>
public sealed class ReadDatabaseHealthCheck(ReadDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthCheckContext,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await context.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("MongoDB did not answer the ping.", exception);
        }
    }
}
