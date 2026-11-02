using System.Runtime.CompilerServices;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Domain.ValueObjects;
using AuditCompliance.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace AuditCompliance.Infrastructure.Persistence;

/// <summary>
/// Implementação append-only: somente inserções e leituras. A deduplicação é garantida pelo índice
/// único (SourceService, EventId), inclusive sob entregas concorrentes.
/// </summary>
public sealed class MongoAuditRecordRepository : IAuditRecordRepository, IAuditRecordQueryRepository
{
    private readonly IMongoCollection<AuditRecordDocument> _collection;

    public MongoAuditRecordRepository(AuditComplianceMongoContext context)
    {
        _collection = context.AuditRecords;
    }

    public async Task<AppendResultEnum> AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        try
        {
            await _collection.InsertOneAsync(AuditRecordDocument.FromDomain(record), cancellationToken: cancellationToken);
            return AppendResultEnum.Appended;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return AppendResultEnum.Duplicate;
        }
    }

    public async Task<AuditRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(d => d.Id == id && d.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    public async Task<(IReadOnlyList<AuditRecord> Items, long TotalItems)> SearchAsync(
        AuditRecordFilter filter,
        AuditRecordSort sort,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var mongoFilter = BuildFilter(filter);

        var total = await _collection.CountDocumentsAsync(mongoFilter, cancellationToken: cancellationToken);
        var documents = await _collection.Find(mongoFilter)
            .Sort(BuildSort(sort))
            .Skip(skip)
            .Limit(take)
            .ToListAsync(cancellationToken);

        return (documents.Select(d => d.ToDomain()).ToList(), total);
    }

    public Task<long> CountAsync(AuditRecordFilter filter, CancellationToken cancellationToken)
    {
        return _collection.CountDocumentsAsync(BuildFilter(filter), cancellationToken: cancellationToken);
    }

    public async IAsyncEnumerable<AuditRecord> StreamAsync(AuditRecordFilter filter, int limit,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cursor = await _collection.Find(BuildFilter(filter))
            .Sort(BuildSort(new AuditRecordSort(AuditRecordSortFieldEnum.OccurredAt, false)))
            .Limit(limit)
            .ToCursorAsync(cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
            foreach (var document in cursor.Current)
                yield return document.ToDomain();
    }

    private static FilterDefinition<AuditRecordDocument> BuildFilter(AuditRecordFilter filter)
    {
        var builder = Builders<AuditRecordDocument>.Filter;
        var conditions = new List<FilterDefinition<AuditRecordDocument>>
        {
            builder.Eq(d => d.TenantId, filter.TenantId),
            builder.Gte(d => d.OccurredAt, filter.From.UtcDateTime),
            builder.Lt(d => d.OccurredAt, filter.To.UtcDateTime)
        };

        if (filter.EventType is not null) conditions.Add(builder.Eq(d => d.EventType, filter.EventType));
        if (filter.ActorId is not null) conditions.Add(builder.Eq(d => d.Actor!.Id, filter.ActorId));
        if (filter.SourceService is not null) conditions.Add(builder.Eq(d => d.SourceService, filter.SourceService));
        if (filter.ResourceType is not null) conditions.Add(builder.Eq(d => d.Resource!.Type, filter.ResourceType));
        if (filter.ResourceId is not null) conditions.Add(builder.Eq(d => d.Resource!.Id, filter.ResourceId));
        if (filter.CorrelationId is not null) conditions.Add(builder.Eq(d => d.CorrelationId, filter.CorrelationId));

        return builder.And(conditions);
    }

    private static SortDefinition<AuditRecordDocument> BuildSort(AuditRecordSort sort)
    {
        var builder = Builders<AuditRecordDocument>.Sort;
        var field = sort.Field == AuditRecordSortFieldEnum.RecordedAt ? nameof(AuditRecordDocument.RecordedAt) : nameof(AuditRecordDocument.OccurredAt);

        return sort.Descending
            ? builder.Combine(builder.Descending(field), builder.Descending("_id"))
            : builder.Combine(builder.Ascending(field), builder.Ascending("_id"));
    }
}
