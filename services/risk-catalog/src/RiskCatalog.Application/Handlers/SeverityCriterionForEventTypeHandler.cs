using MediatR;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Queries;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Handlers;

public class
    SeverityCriterionForEventTypeHandler : IRequestHandler<GetSeverityCriterionForEventTypeQuery,
    List<SeverityCriterionDTO>>
{
    private readonly IEventTypeRepository _eventTypeRepository;

    public SeverityCriterionForEventTypeHandler(IEventTypeRepository eventTypeRepository)
    {
        _eventTypeRepository = eventTypeRepository;
    }

    public async Task<List<SeverityCriterionDTO>> Handle(GetSeverityCriterionForEventTypeQuery request,
        CancellationToken cancellationToken)
    {
        var severitiesCriterion = await _eventTypeRepository.GetSeveritiesCriterionForEventType(request.EventTypeCode,
            request.Version);
        var sevetiesCriterionDTOs = severitiesCriterion.Select(severityCriterion => new SeverityCriterionDTO
        {
            EventTypeCode = request.EventTypeCode,
            SeverityLevel = severityCriterion.Severity.Level.ToString(),
            MinValue = severityCriterion.MinValue,
            MaxValue = severityCriterion.MaxValue,
            Unit = severityCriterion.Unit,
            Version = severityCriterion.Version,
        }).ToList();

        return sevetiesCriterionDTOs;
    }
}