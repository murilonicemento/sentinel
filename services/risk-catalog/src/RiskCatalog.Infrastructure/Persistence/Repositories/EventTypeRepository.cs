using Microsoft.EntityFrameworkCore;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Infrastructure.Persistence.DatabaseContext;

namespace RiskCatalog.Infrastructure.Persistence.Repositories;

public class EventTypeRepository : IEventTypeRepository
{
    private readonly RiskCatalogDbContext _dbContext;

    public EventTypeRepository(RiskCatalogDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<EventType>> GetEventTypes(
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.EventTypes.Where(e => e.IsActive == isActive).ToListAsync(cancellationToken);
    }

    public async Task<EventType?> GetEventTypeByCode(
        string eventTypeCode,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.EventTypes
            .Include(e => e.SeverityCriteria)
            .FirstOrDefaultAsync(e => e.Code == eventTypeCode, cancellationToken);
    }

    public async Task<IEnumerable<SeverityCriterion>> GetSeveritiesCriterionForEventType(
        string eventTypeCode,
        int? version,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SeverityCriteria.Include(s => s.Severity).AsQueryable();

        query = query.Where(sc => sc.EventType.Code == eventTypeCode);

        if (version.HasValue)
            query = query.Where(sc => sc.Version == version.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<bool> CreateEventType(EventType eventType, CancellationToken cancellationToken = default)
    {
        await _dbContext.EventTypes.AddAsync(eventType, cancellationToken);

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        return result > 0;
    }

    public async Task<bool> UpdateEventTypeStatus(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var eventType = await _dbContext.EventTypes.FindAsync(new object[] { id }, cancellationToken);

        if (eventType == null)
            return false;

        eventType.IsActive = isActive;
        _dbContext.EventTypes.Update(eventType);

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        return result > 0;
    }

    public async Task<bool> AddSeverityCriterionToEventType(
        SeverityCriterion severityCriterion,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SeverityCriteria.AddAsync(severityCriterion, cancellationToken);

        var result = await _dbContext.SaveChangesAsync(cancellationToken);

        return result > 0;
    }
}