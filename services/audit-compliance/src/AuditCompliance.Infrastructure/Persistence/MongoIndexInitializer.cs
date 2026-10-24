using AuditCompliance.Infrastructure.Persistence.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace AuditCompliance.Infrastructure.Persistence;

/// <summary>
/// Cria os índices do serviço (operação idempotente). Mantém paridade com
/// platform/init-db/mongodb/2-schema.js; alterações devem ser refletidas nos dois lugares.
/// </summary>
public sealed class MongoIndexInitializer : IHostedService
{
    private readonly AuditComplianceMongoContext _context;
    private readonly ILogger<MongoIndexInitializer> _logger;

    public MongoIndexInitializer(AuditComplianceMongoContext context, ILogger<MongoIndexInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await EnsureIndexesAsync(_context, cancellationToken);
        _logger.LogInformation("Audit compliance MongoDB indexes ensured");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public static async Task EnsureIndexesAsync(AuditComplianceMongoContext context, CancellationToken cancellationToken)
    {
        var records = Builders<AuditRecordDocument>.IndexKeys;

        await context.AuditRecords.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AuditRecordDocument>(
                records.Ascending(d => d.SourceService).Ascending(d => d.EventId),
                new CreateIndexOptions { Name = "ux_source_service_event_id", Unique = true }),
            new CreateIndexModel<AuditRecordDocument>(
                records.Ascending(d => d.TenantId).Descending(d => d.OccurredAt).Descending("_id"),
                new CreateIndexOptions { Name = "ix_tenant_occurred_at" }),
            new CreateIndexModel<AuditRecordDocument>(
                records.Ascending(d => d.TenantId).Ascending(d => d.EventType).Descending(d => d.OccurredAt),
                new CreateIndexOptions { Name = "ix_tenant_event_type_occurred_at" }),
            new CreateIndexModel<AuditRecordDocument>(
                records.Ascending(d => d.TenantId).Ascending("Actor.Id").Descending(d => d.OccurredAt),
                new CreateIndexOptions { Name = "ix_tenant_actor_occurred_at" }),
            new CreateIndexModel<AuditRecordDocument>(
                records.Ascending(d => d.TenantId).Ascending("Resource.Id").Descending(d => d.OccurredAt),
                new CreateIndexOptions { Name = "ix_tenant_resource_occurred_at" }),
            new CreateIndexModel<AuditRecordDocument>(
                records.Ascending(d => d.TenantId).Ascending(d => d.CorrelationId),
                new CreateIndexOptions { Name = "ix_tenant_correlation_id" })
        ], cancellationToken);

        var exports = Builders<AuditExportDocument>.IndexKeys;

        await context.AuditExports.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AuditExportDocument>(
                exports.Ascending(d => d.Status).Ascending(d => d.RequestedAt),
                new CreateIndexOptions { Name = "ix_status_requested_at" }),
            new CreateIndexModel<AuditExportDocument>(
                exports.Ascending(d => d.Status).Ascending(d => d.ExpiresAt),
                new CreateIndexOptions { Name = "ix_status_expires_at" })
        ], cancellationToken);
    }
}
