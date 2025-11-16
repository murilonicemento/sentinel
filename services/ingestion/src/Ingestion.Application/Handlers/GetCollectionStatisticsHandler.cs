using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Queries;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Ingestion.Application.Handlers;

public class
    GetCollectionStatisticsHandler : IRequestHandler<GetCollectionStatisticsQuery, CollectionStatisticsResponse>
{
    private readonly ReadDbContext _context;

    public GetCollectionStatisticsHandler(ReadDbContext context)
    {
        _context = context;
    }

    public async Task<CollectionStatisticsResponse> Handle(GetCollectionStatisticsQuery request,
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

        return new CollectionStatisticsResponse();

        // var pipeline = new[]
        // {
        //     PipelineStageDefinitionBuilder.Match(filter),
        //     PipelineStageDefinitionBuilder.Group(
        //         BsonNull.Value,
        //         new BsonDocument
        //         {
        //             { "totalEventos", new BsonDocument("$sum", 1) },
        //             {
        //                 "totalPorTipo",
        //                 new BsonDocument("$sum", new BsonDocument("$cond", new BsonArray { "$type", 1, 0 }))
        //             },
        //             { "minIntensidade", new BsonDocument("$min", "$intensity") },
        //             { "maxIntensidade", new BsonDocument("$max", "$intensity") },
        //             { "mediaIntensidade", new BsonDocument("$avg", "$intensity") }
        //         }
        //     )
        // };
        //
        // var result =
        //     await collection.AggregateAsync<CollectionStatisticsResponse>(pipeline,
        //         cancellationToken: cancellationToken);
        //
        // return await result.FirstOrDefaultAsync(cancellationToken: cancellationToken)
        //        ?? new CollectionStatisticsResponse();
    }
}