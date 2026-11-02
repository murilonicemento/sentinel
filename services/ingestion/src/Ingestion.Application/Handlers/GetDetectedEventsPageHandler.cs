using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Application.Queries;
using MediatR;

namespace Ingestion.Application.Handlers;

public class GetDetectedEventsPageHandler : IRequestHandler<GetDetectedEventsPageQuery, PagedResultDTO<DetectedEventDTO>>,
    IRequestHandler<GetDetectedEventStatisticsQuery, DetectedEventStatisticsDTO>
{
    private readonly IDetectedEventReadRepository _detectedEventReadRepository;

    public GetDetectedEventsPageHandler(IDetectedEventReadRepository detectedEventReadRepository)
    {
        _detectedEventReadRepository = detectedEventReadRepository;
    }

    public Task<PagedResultDTO<DetectedEventDTO>> Handle(GetDetectedEventsPageQuery request,
        CancellationToken cancellationToken)
        => _detectedEventReadRepository.GetPageAsync(request.Scope, request.Page, request.PageSize, cancellationToken);

    public Task<DetectedEventStatisticsDTO> Handle(GetDetectedEventStatisticsQuery request,
        CancellationToken cancellationToken)
        => _detectedEventReadRepository.GetStatisticsAsync(request.Scope, request.InitialDate, request.EndDate,
            cancellationToken);
}
