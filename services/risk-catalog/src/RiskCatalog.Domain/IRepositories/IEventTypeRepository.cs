using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;

namespace RiskCatalog.Domain.IRepositories;

public interface IEventTypeRepository
{
    public Task<IEnumerable<EventType>> GetEventTypes(bool? isActive);
    public Task<EventType?> GetEventTypeByCode(string eventTypeCode);
    public Task<IEnumerable<SeverityCriterion>> GetSeveritiesCriterionForEventType(string eventTypeCode, int? version);
}