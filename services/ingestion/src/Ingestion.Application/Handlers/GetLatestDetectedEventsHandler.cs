using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Application.Queries;
using MediatR;

namespace Ingestion.Application.Handlers;

public class
    GetLatestDetectedEventsHandler : IRequestHandler<GetLatestDetectedEventsQuery,
    IEnumerable<SensorEventDetected>>
{
    private readonly IDetectedEventReadRepository _detectedEventReadRepository;

    public GetLatestDetectedEventsHandler(IDetectedEventReadRepository detectedEventReadRepository)
    {
        _detectedEventReadRepository = detectedEventReadRepository;
    }

    public async Task<IEnumerable<SensorEventDetected>> Handle(
        GetLatestDetectedEventsQuery request,
        CancellationToken cancellationToken)
        => await _detectedEventReadRepository.GetLatestAsync(request.Limit, cancellationToken);
}
