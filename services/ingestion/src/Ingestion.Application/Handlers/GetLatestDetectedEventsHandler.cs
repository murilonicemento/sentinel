using Ingestion.Application.Events;
using Ingestion.Application.Queries;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using MediatR;
using MongoDB.Driver;

namespace Ingestion.Application.Handlers;

public class
    GetLatestDetectedEventsHandler : IRequestHandler<GetLatestDetectedEventsQuery,
    IEnumerable<SensorEventDetected>>
{
    private readonly ReadDbContext _context;

    public GetLatestDetectedEventsHandler(ReadDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SensorEventDetected>> Handle(
        GetLatestDetectedEventsQuery request,
        CancellationToken cancellationToken)
    {
        var collection = _context.GetCollection<SensorEventDetected>("events");

        return await collection
            .Find(_ => true)
            .SortByDescending(x => x.CollectedAt)
            .Limit(request.Limit)
            .ToListAsync(cancellationToken: cancellationToken);
    }
}