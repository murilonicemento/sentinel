using Ingestion.Domain.Aggregates;

namespace Ingestion.Domain.Interfaces.Repositories;

public interface IEventTypePermissionRepository
{
    public Task<bool> RegisterManyAsync(List<EventTypePermission> eventTypePermissions);
}