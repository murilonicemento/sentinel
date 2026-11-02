using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace AuditCompliance.Infrastructure.Persistence;

public sealed class MongoAuditExportRepository : IAuditExportRepository
{
    private static readonly string Pending = ExportStatusEnum.Pending.ToString();
    private static readonly string Processing = ExportStatusEnum.Processing.ToString();
    private static readonly string Completed = ExportStatusEnum.Completed.ToString();

    private readonly IMongoCollection<AuditExportDocument> _collection;

    public MongoAuditExportRepository(AuditComplianceMongoContext context)
    {
        _collection = context.AuditExports;
    }

    public Task AddAsync(AuditExport export, CancellationToken cancellationToken)
    {
        return _collection.InsertOneAsync(AuditExportDocument.FromDomain(export), cancellationToken: cancellationToken);
    }

    public async Task<AuditExport?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection.Find(d => d.Id == id && d.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task<AuditExport?> TryClaimNextAsync(DateTimeOffset now, DateTimeOffset leaseUntil, int maxAttempts,
        CancellationToken cancellationToken)
    {
        var filter = Builders<AuditExportDocument>.Filter;
        var claimable = filter.And(
            filter.Lt(d => d.Attempts, maxAttempts),
            filter.Or(
                filter.Eq(d => d.Status, Pending),
                filter.And(
                    filter.Eq(d => d.Status, Processing),
                    filter.Lt(d => d.LeaseExpiresAt, now.UtcDateTime))));

        var update = Builders<AuditExportDocument>.Update
            .Set(d => d.Status, Processing)
            .Set(d => d.LeaseExpiresAt, leaseUntil.UtcDateTime)
            .Inc(d => d.Attempts, 1);

        var document = await _collection.FindOneAndUpdateAsync(claimable, update,
            new FindOneAndUpdateOptions<AuditExportDocument>
            {
                Sort = Builders<AuditExportDocument>.Sort.Ascending(d => d.RequestedAt),
                ReturnDocument = ReturnDocument.After
            }, cancellationToken);

        return document?.ToDomain();
    }

    public async Task<bool> SaveProcessingResultAsync(AuditExport export, CancellationToken cancellationToken)
    {
        // O número de tentativas atua como token de exclusividade: um worker cujo lease expirou não
        // sobrescreve o resultado de quem reivindicou a exportação depois dele.
        var update = Builders<AuditExportDocument>.Update
            .Set(d => d.Status, export.Status.ToString())
            .Set(d => d.FileId, export.FileId)
            .Set(d => d.RecordCount, export.RecordCount)
            .Set(d => d.CompletedAt, export.CompletedAt?.UtcDateTime)
            .Set(d => d.ExpiresAt, export.ExpiresAt?.UtcDateTime)
            .Set(d => d.FailureCode, export.FailureCode)
            .Unset(d => d.LeaseExpiresAt);

        var result = await _collection.UpdateOneAsync(
            d => d.Id == export.Id && d.Status == Processing && d.Attempts == export.Attempts,
            update,
            cancellationToken: cancellationToken);

        return result.MatchedCount == 1;
    }

    public async Task<long> FailAbandonedAsync(DateTimeOffset now, int maxAttempts, string failureCode,
        CancellationToken cancellationToken)
    {
        var update = Builders<AuditExportDocument>.Update
            .Set(d => d.Status, ExportStatusEnum.Failed.ToString())
            .Set(d => d.FailureCode, failureCode)
            .Set(d => d.CompletedAt, now.UtcDateTime)
            .Unset(d => d.LeaseExpiresAt);

        var result = await _collection.UpdateManyAsync(
            d => d.Status == Processing && d.LeaseExpiresAt < now.UtcDateTime && d.Attempts >= maxAttempts,
            update,
            cancellationToken: cancellationToken);

        return result.ModifiedCount;
    }

    public async Task<IReadOnlyList<AuditExport>> GetExpiredCompletedAsync(DateTimeOffset now, int limit,
        CancellationToken cancellationToken)
    {
        var documents = await _collection.Find(d => d.Status == Completed && d.ExpiresAt <= now.UtcDateTime)
            .Limit(limit)
            .ToListAsync(cancellationToken);

        return documents.Select(d => d.ToDomain()).ToList();
    }

    public Task MarkExpiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var update = Builders<AuditExportDocument>.Update
            .Set(d => d.Status, ExportStatusEnum.Expired.ToString())
            .Set(d => d.FileId, null);

        return _collection.UpdateOneAsync(d => d.Id == id && d.Status == Completed, update,
            cancellationToken: cancellationToken);
    }
}
