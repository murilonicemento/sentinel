using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Domain.IRepositories;

public interface IEventTypeRepository
{
    public Task<IEnumerable<EventType>> GetEventTypes(bool? isActive, CancellationToken cancellationToken = default);
    public Task<EventType?> GetEventTypeByCode(string eventTypeCode, CancellationToken cancellationToken = default);
    public Task<IEnumerable<SeverityCriterion>> GetSeveritiesCriterionForEventType(string eventTypeCode, int? version, CancellationToken cancellationToken = default);
    public Task<bool> CreateEventType(EventType eventType, CancellationToken cancellationToken = default);
    public Task<bool> UpdateEventTypeStatus(Guid id, bool isActive, CancellationToken cancellationToken = default);
    public Task<bool> AddSeverityCriterionToEventType(SeverityCriterion severityCriterion, CancellationToken cancellationToken = default);
}