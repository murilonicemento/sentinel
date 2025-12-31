using MediatR;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Queries;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Handlers;

public class EventTypeHandler : IRequestHandler<GetEventTypesQuery, List<EventTypeDTO>>
{
    private readonly IEventTypeRepository _eventTypeRepository;

    public EventTypeHandler(IEventTypeRepository eventTypeRepository)
    {
        _eventTypeRepository = eventTypeRepository;
    }

    public async Task<List<EventTypeDTO>> Handle(GetEventTypesQuery request, CancellationToken cancellationToken)
    {
        var eventTypes = await _eventTypeRepository.GetEventTypes(request.IsActive);
        var eventTypeDTOs = eventTypes.Select(e => new EventTypeDTO
        {
            Id = e.Id,
            Code = e.Code,
            Name = e.Name,
        }).ToList();

        return eventTypeDTOs;
    }
}