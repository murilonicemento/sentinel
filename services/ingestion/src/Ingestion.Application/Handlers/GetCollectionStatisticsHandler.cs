using Ingestion.Application.DTO;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Application.Queries;
using MediatR;

namespace Ingestion.Application.Handlers;

public class
    GetCollectionStatisticsHandler : IRequestHandler<GetCollectionStatisticsQuery, CollectionStatisticsResponseDTO?>
{
    private readonly IDetectedEventReadRepository _detectedEventReadRepository;

    public GetCollectionStatisticsHandler(IDetectedEventReadRepository detectedEventReadRepository)
    {
        _detectedEventReadRepository = detectedEventReadRepository;
    }

    public async Task<CollectionStatisticsResponseDTO?> Handle(GetCollectionStatisticsQuery request,
        CancellationToken cancellationToken)
        => await _detectedEventReadRepository.GetStatisticsAsync(
            request.InitialDate,
            request.EndDate,
            cancellationToken);
}
