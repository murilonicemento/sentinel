using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Services.Interfaces;

public interface IEventTypeService
{
    public Task<List<EventTypeDTO>> GetEventTypesAsync(
        bool? isActive,
        CancellationToken cancellationToken = default);

    public Task<EventTypeDTO?> GetEventTypeByCodeAsync(
        string eventTypeCode,
        CancellationToken cancellationToken = default);

    public Task<List<SeverityCriterionDTO>> GetSeverityCriterionForEventTypeAsync(
        string eventTypeCode,
        int? version,
        CancellationToken cancellationToken = default);

    public Task<bool> CreateEventTypeAsync(
        CreateEventTypeDTO eventTypeDto,
        CancellationToken cancellationToken = default);

    public Task<bool> CreateSeverity(
        SeverityDTO severityDto,
        CancellationToken cancellationToken = default);

    public Task<bool> CreateSeverityCriterionToEventType(
        SeverityCriterionDTO severityCriterionDto,
        CancellationToken cancellationToken = default);

    public Task<bool> UpdateEventTypeStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default);
}
