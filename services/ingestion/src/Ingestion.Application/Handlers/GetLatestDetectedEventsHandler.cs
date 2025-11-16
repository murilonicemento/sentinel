using Ingestion.Application.Events;
using Ingestion.Application.Queries;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using MediatR;
using MongoDB.Driver;

namespace Ingestion.Application.Handlers;

public class
    GetLatestDetectedEventsHandler : IRequestHandler<GetLatestDetectedEventsQuery,
    IEnumerable<ClimaticEventDetectedEvent>>
{
    private readonly ReadDbContext _context;

    public GetLatestDetectedEventsHandler(ReadDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ClimaticEventDetectedEvent>> Handle(
        GetLatestDetectedEventsQuery request,
        CancellationToken cancellationToken)
    {
        var collection = _context.GetCollection<ClimaticEventDetectedEvent>("events");

        return await collection
            .Find(_ => true)
            .SortByDescending(x => x.CollectedAt)
            .Limit(request.Limit)
            .ToListAsync(cancellationToken: cancellationToken);
    }
}