using MediatR;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Queries;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Handlers;

public class EventTypeByCodeHandler : IRequestHandler<GetEventTypeByCodeQuery, EventTypeDTO?>
{
    private readonly IEventTypeRepository _eventTypeRepository;

    public EventTypeByCodeHandler(IEventTypeRepository eventTypeRepository)
    {
        _eventTypeRepository = eventTypeRepository;
    }

    public async Task<EventTypeDTO?> Handle(GetEventTypeByCodeQuery request, CancellationToken cancellationToken)
    {
        var eventType = await _eventTypeRepository.GetEventTypeByCode(request.EventTypeCode);

        if (eventType is null)
            return null;

        var eventTypeDTO = new EventTypeDTO
        {
            Id = eventType.Id,
            Code = eventType.Code,
            Name = eventType.Name,
        };

        return eventTypeDTO;
    }
}