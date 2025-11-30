using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Queries;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ingestion.Application.Handlers;

public class
    GetCollectionStatisticsHandler : IRequestHandler<GetCollectionStatisticsQuery, CollectionStatisticsResponseDTO>
{
    private readonly ReadDbContext _context;

    public GetCollectionStatisticsHandler(ReadDbContext context)
    {
        _context = context;
    }

    public async Task<CollectionStatisticsResponseDTO> Handle(GetCollectionStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        var collection = _context.GetCollection<ClimaticEventDetectedEvent>("events_normalized");
        var filters = new List<FilterDefinition<ClimaticEventDetectedEvent>>();

        if (request.InitialDate.HasValue)
            filters.Add(Builders<ClimaticEventDetectedEvent>.Filter.Gte(x => x.CollectedAt, request.InitialDate.Value));
        if (request.EndDate.HasValue)
            filters.Add(Builders<ClimaticEventDetectedEvent>.Filter.Lte(x => x.CollectedAt, request.EndDate.Value));

        var filter = filters.Count > 0
            ? Builders<ClimaticEventDetectedEvent>.Filter.And(filters)
            : Builders<ClimaticEventDetectedEvent>.Filter.Empty;
        var pipeline = new IPipelineStageDefinition[]
        {
            PipelineStageDefinitionBuilder.Match(filter),
            PipelineStageDefinitionBuilder.Group<ClimaticEventDetectedEvent, BsonNull, BsonDocument>(
                _ => BsonNull.Value,
                g => new BsonDocument
                {
                    { "totalEvents", new BsonDocument("$sum", 1) },
                    { "totalByType", new BsonDocument("$push", "$type") },
                    { "minIntensity", new BsonDocument("$min", "$intensity") },
                    { "maxIntensity", new BsonDocument("$max", "$intensity") },
                    { "averageIntensity", new BsonDocument("$avg", "$intensity") }
                }
            )
        };
        var bson = await collection
            .Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (bson is null)
            return new CollectionStatisticsResponseDTO();

        var types = bson["totalByType"].AsBsonArray
            .Select(t => t.AsString)
            .GroupBy(t => t)
            .ToDictionary(g => g.Key, g => g.Count());

        return new CollectionStatisticsResponseDTO
        {
            TotalEvents = bson["totalEvents"].ToInt32(),
            TotalByType = types,
            MinIntensity = bson["minIntensity"].ToDouble(),
            MaxIntensity = bson["maxIntensity"].ToDouble(),
            AverageIntensity = bson["averageIntensity"].ToDouble()
        };
    }
}