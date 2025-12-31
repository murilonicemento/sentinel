using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Infrastructure.DatabaseContext;

namespace RiskCatalog.Infrastructure.Repositories;

public class EventTypeRepository : IEventTypeRepository
{
    private readonly RiskCatalogDbContext _dbContext;

    public EventTypeRepository(RiskCatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<EventType>> GetEventTypes(bool? isActive)
    {
        return await _dbContext.EventTypes.Where(e => e.IsActive == isActive).ToListAsync();
    }

    public async Task<EventType?> GetEventTypeByCode(string eventTypeCode)
    {
        return await _dbContext.EventTypes.FirstOrDefaultAsync(e => e.Code == eventTypeCode);
    }

    public async Task<IEnumerable<SeverityCriterion>> GetSeveritiesCriterionForEventType(string eventTypeCode,
        int? version)
    {
        var query = _dbContext.SeverityCriteria.AsQueryable();

        query = query.Where(sc => sc.EventType.Code == eventTypeCode);

        if (version.HasValue)
            query = query.Where(sc => sc.Version == version.Value);

        return await query.ToListAsync();
    }
}