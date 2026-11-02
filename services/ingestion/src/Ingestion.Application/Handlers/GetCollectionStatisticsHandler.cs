using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Queries;
using Ingestion.Domain.Enums;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ingestion.Application.Handlers;

public class
    GetCollectionStatisticsHandler : IRequestHandler<GetCollectionStatisticsQuery, CollectionStatisticsResponseDTO?>
{
    private readonly ReadDbContext _context;

    public GetCollectionStatisticsHandler(ReadDbContext context)
    {
        _context = context;
    }

    public async Task<CollectionStatisticsResponseDTO?> Handle(GetCollectionStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var collection = _context.GetCollection<SensorEventDetected>("events");
        var aggregation = collection.Aggregate();

        if (request.InitialDate.HasValue)
            aggregation =
                aggregation.Match(
                    Builders<SensorEventDetected>.Filter.Gte(x => x.CollectedAt, request.InitialDate.Value));
        if (request.EndDate.HasValue)
            aggregation =
                aggregation.Match(
                    Builders<SensorEventDetected>.Filter.Lte(x => x.CollectedAt, request.EndDate.Value));

        if (!await aggregation.AnyAsync(cancellationToken: cancellationToken))
            return null;

        return await aggregation
            .Group(
                x => BsonNull.Value,
                g => new CollectionStatisticsResponseDTO
                {
                    TotalEvents = g.Count(),
                    TotalByTypeRaw = g.Select(x => Enum.Parse<ClimaticEventEnum>(x.EventType)).ToList(),
                    MinIntensity = g.Min(x => x.Intensity),
                    MaxIntensity = g.Max(x => x.Intensity),
                    AverageIntensity = g.Average(x => x.Intensity)
                }
            )
            .FirstOrDefaultAsync(cancellationToken: cancellationToken) ?? new CollectionStatisticsResponseDTO();
    }
}