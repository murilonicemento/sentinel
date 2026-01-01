using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Services.Interfaces;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Services;

public class EventTypeService : IEventTypeService
{
    private readonly IEventTypeRepository _eventTypeRepository;

    public EventTypeService(IEventTypeRepository eventTypeRepository)
    {
        _eventTypeRepository = eventTypeRepository;
    }

    public async Task<List<EventTypeDTO>> GetEventTypesAsync(
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var eventTypes = await _eventTypeRepository.GetEventTypes(isActive);
        var eventTypeDTOs = eventTypes.Select(e => new EventTypeDTO
        {
            Id = e.Id,
            Code = e.Code,
            Name = e.Name,
        }).ToList();

        return eventTypeDTOs;
    }

    public async Task<EventTypeDTO?> GetEventTypeByCodeAsync(
        string eventTypeCode,
        CancellationToken cancellationToken = default)
    {
        var eventType = await _eventTypeRepository.GetEventTypeByCode(eventTypeCode);

        if (eventType is null)
            return null;

        return new EventTypeDTO
        {
            Id = eventType.Id,
            Code = eventType.Code,
            Name = eventType.Name,
        };
    }

    public async Task<List<SeverityCriterionDTO>> GetSeverityCriterionForEventTypeAsync(
        string eventTypeCode,
        int? version,
        CancellationToken cancellationToken = default)
    {
        var severitiesCriterion = await _eventTypeRepository.GetSeveritiesCriterionForEventType(
            eventTypeCode,
            version);
        var severityCriterionDTOs = severitiesCriterion.Select(severityCriterion => new SeverityCriterionDTO
        {
            EventTypeCode = eventTypeCode,
            SeverityLevel = severityCriterion.Severity.Level.ToString(),
            MinValue = severityCriterion.MinValue,
            MaxValue = severityCriterion.MaxValue,
            Unit = severityCriterion.Unit,
            Version = severityCriterion.Version,
        }).ToList();

        return severityCriterionDTOs;
    }
}

