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
    // Queries read "events" while writes go to the per-domain collections (see ING-10 in
    // docs/compliance/services-validation.md); kept as-is to preserve current API behavior.
    private const string QueryCollectionName = "events";

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

    public async Task AddAsync<TEvent>(string domain, TEvent detectedEvent, CancellationToken cancellationToken)
        where TEvent : class
    {
        var collectionName = domain == "Climatic" ? "climaticEvents" : "disasterEvents";

        await _context.GetCollection<TEvent>(collectionName).InsertOneAsync(detectedEvent, cancellationToken);
    }
}
