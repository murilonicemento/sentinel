using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;

namespace AuditCompliance.Infrastructure.Persistence;

public sealed class MongoHealthCheck : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    private readonly AuditComplianceMongoContext _context;

    public MongoHealthCheck(AuditComplianceMongoContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            await _context.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1),
                cancellationToken: timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                          !cancellationToken.IsCancellationRequested)
        {
            // A descrição é genérica para não expor endereços ou detalhes de conexão.
            return HealthCheckResult.Unhealthy("Audit storage is unavailable.", exception);
        }
    }
}
