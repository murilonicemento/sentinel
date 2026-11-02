using Dapper;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Infrastructure.Write.Persistence.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence.Repositories;

public class EventTypePermissionRepository : IEventTypePermissionRepository
{
    private readonly WriteDbContext _writeDbContext;

    public EventTypePermissionRepository(WriteDbContext writeDbContext)
    {
        _writeDbContext = writeDbContext;
    }

    public async Task<bool> RegisterManyAsync(List<EventTypePermission> eventTypePermissions)
    {
        var query = @"INSERT INTO event_type_permission(
	                            id, data_source_id, event_domain, event_type)
	                            VALUES (@Id, @DataSourceId, @EventDomain, @EventType);";
        var parameters = eventTypePermissions.Select(p => new
        {
            p.Id,
            p.DataSourceId,
            p.EventDomain,
            p.EventType,
        });

        await _writeDbContext.Connection.ExecuteAsync(query, parameters);

        return true;
    }
}