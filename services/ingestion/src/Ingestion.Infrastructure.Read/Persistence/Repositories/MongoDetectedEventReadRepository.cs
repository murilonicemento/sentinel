using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Domain.Enums;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using Ingestion.Infrastructure.Read.Persistence.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ingestion.Infrastructure.Read.Persistence.Repositories;

public class MongoDetectedEventReadRepository : IDetectedEventReadRepository
{
    // The legacy GetLatestAsync/GetStatisticsAsync overloads read "events", which nothing writes (ING-10 in
    // docs/compliance/services-validation.md); they back the frozen /api/ingestion routes. /api/v1 reads the
    // per-domain collections that the ingestion pipeline actually writes.
    private const string QueryCollectionName = "events";

    private const string ClimaticDomain = "Climatic";
    private const string DisasterDomain = "Disaster";
    private const string ClimaticCollectionName = "climaticEvents";
    private const string DisasterCollectionName = "disasterEvents";

    private readonly ReadDbContext _context;

    public MongoDetectedEventReadRepository(ReadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SensorEventDetected>> GetLatestAsync(int limit,
        CancellationToken cancellationToken)
    {
        var collection = _context.GetCollection<SensorEventDetected>(QueryCollectionName);

        return await collection
            .Find(_ => true)
            .SortByDescending(x => x.CollectedAt)
            .Limit(limit)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    public async Task<CollectionStatisticsResponseDTO?> GetStatisticsAsync(
        DateTime? initialDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var collection = _context.GetCollection<SensorEventDetected>(QueryCollectionName);
        var aggregation = collection.Aggregate();

        if (initialDate.HasValue)
            aggregation =
                aggregation.Match(Builders<SensorEventDetected>.Filter.Gte(x => x.CollectedAt, initialDate.Value));
        if (endDate.HasValue)
            aggregation =
                aggregation.Match(Builders<SensorEventDetected>.Filter.Lte(x => x.CollectedAt, endDate.Value));

        if (!await aggregation.AnyAsync(cancellationToken: cancellationToken))
            return null;

        var statistics = await aggregation
            .Group(
                x => BsonNull.Value,
                g => new CollectionStatisticsDocument
                {
                    TotalEvents = g.Count(),
                    TotalByTypeRaw = g.Select(x => Enum.Parse<ClimaticEventEnum>(x.EventType)).ToList(),
                    MinIntensity = g.Min(x => x.Intensity),
                    MaxIntensity = g.Max(x => x.Intensity),
                    AverageIntensity = g.Average(x => x.Intensity)
                }
            )
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (statistics is null)
            return new CollectionStatisticsResponseDTO();

        return new CollectionStatisticsResponseDTO
        {
            TotalEvents = statistics.TotalEvents,
            TotalByTypeRaw = statistics.TotalByTypeRaw,
            MinIntensity = statistics.MinIntensity,
            MaxIntensity = statistics.MaxIntensity,
            AverageIntensity = statistics.AverageIntensity
        };
    }

    public async Task<PagedResultDTO<DetectedEventDTO>> GetPageAsync(
        TenantAccessScope scope,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var filter = ScopeFilter(scope);

        var countResult = await DetectedEvents(filter).Count().FirstOrDefaultAsync(cancellationToken);
        var totalItems = countResult?.Count ?? 0;
        if (totalItems == 0)
            return PagedResultDTO<DetectedEventDTO>.Create([], page, pageSize, 0);

        var documents = await DetectedEvents(filter)
            .SortByDescending(d => d.CollectedAt)
            .ThenBy(d => d.EventId)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResultDTO<DetectedEventDTO>.Create(documents.Select(ToDto).ToList(), page, pageSize, totalItems);
    }

    public async Task<DetectedEventStatisticsDTO> GetStatisticsAsync(
        TenantAccessScope scope,
        DateTime? initialDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var filter = ScopeFilter(scope);
        if (initialDate.HasValue) filter &= Filter.Gte(d => d.CollectedAt, initialDate.Value);
        if (endDate.HasValue) filter &= Filter.Lte(d => d.CollectedAt, endDate.Value);

        var byType = await DetectedEvents(filter)
            .Group(
                d => d.EventType,
                g => new EventTypeStatisticsDocument
                {
                    EventType = g.Key,
                    Count = g.Count(),
                    MinIntensity = g.Min(d => d.Intensity),
                    MaxIntensity = g.Max(d => d.Intensity),
                    TotalIntensity = g.Sum(d => d.Intensity)
                })
            .ToListAsync(cancellationToken);

        if (byType.Count == 0)
            return DetectedEventStatisticsDTO.Empty;

        long totalEvents = byType.Sum(t => t.Count);
        return new DetectedEventStatisticsDTO
        {
            TotalEvents = totalEvents,
            TotalByType = byType.ToDictionary(t => t.EventType, t => t.Count),
            MinIntensity = byType.Min(t => t.MinIntensity),
            MaxIntensity = byType.Max(t => t.MaxIntensity),
            AverageIntensity = byType.Sum(t => t.TotalIntensity) / totalEvents
        };
    }

    public async Task AddAsync(DetectedEventDTO detectedEvent, CancellationToken cancellationToken)
    {
        var document = new DetectedEventDocument
        {
            EventId = detectedEvent.EventId,
            CollectionId = detectedEvent.CollectionId,
            TenantId = detectedEvent.TenantId,
            EventType = detectedEvent.EventType,
            Intensity = detectedEvent.Intensity,
            Latitude = detectedEvent.Latitude,
            Longitude = detectedEvent.Longitude,
            CollectedAt = detectedEvent.CollectedAt
        };

        try
        {
            await DetectedEventCollection(CollectionFor(detectedEvent.Domain))
                .InsertOneAsync(document, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Event ids are deterministic per collection sample: a retried collection projects the same id again.
        }
    }

    private static FilterDefinitionBuilder<DetectedEventDocument> Filter => Builders<DetectedEventDocument>.Filter;

    private static FilterDefinition<DetectedEventDocument> ScopeFilter(TenantAccessScope scope) =>
        scope.AllTenants ? Filter.Empty : Filter.In(d => d.TenantId, scope.TenantIds);

    private static string CollectionFor(string domain) =>
        domain == ClimaticDomain ? ClimaticCollectionName : DisasterCollectionName;

    private IMongoCollection<DetectedEventDocument> DetectedEventCollection(string name) =>
        _context.GetCollection<DetectedEventDocument>(name);

    // Union of both per-domain collections; the domain is derived from the source collection so documents written
    // before it was tracked are classified too.
    private IAggregateFluent<DetectedEventDocument> DetectedEvents(FilterDefinition<DetectedEventDocument> filter)
    {
        var disasterPipeline = new EmptyPipelineDefinition<DetectedEventDocument>()
            .AppendStage<DetectedEventDocument, DetectedEventDocument, DetectedEventDocument>(
                SetDomainStage(DisasterDomain));

        return DetectedEventCollection(ClimaticCollectionName)
            .Aggregate()
            .AppendStage<DetectedEventDocument>(SetDomainStage(ClimaticDomain))
            .UnionWith(DetectedEventCollection(DisasterCollectionName), disasterPipeline)
            .Match(filter);
    }

    private static BsonDocument SetDomainStage(string domain) =>
        new("$set", new BsonDocument("Domain", domain));

    private static DetectedEventDTO ToDto(DetectedEventDocument document) => new(
        document.EventId,
        document.CollectionId,
        document.TenantId,
        document.Domain ?? string.Empty,
        document.EventType,
        document.Intensity,
        document.Latitude,
        document.Longitude,
        document.CollectedAt);
}
