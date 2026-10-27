using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using AlertOrchestrator.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Persistence.Repositories;

public sealed class PostgreSqlAlertWindowRepository : IAlertWindowRepository
{
    private readonly AlertOrchestratorDbContext _context;
    private readonly ILogger<PostgreSqlAlertWindowRepository> _logger;

    public PostgreSqlAlertWindowRepository(AlertOrchestratorDbContext context,
        ILogger<PostgreSqlAlertWindowRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AlertWindow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .Include(w => w.Signals)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<AlertWindow?> GetOpenWindowAsync(string region, RiskType riskType, string? tenantId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AlertWindows
            .Include(w => w.Signals)
            .Where(w => w.Region == region
                        && w.RiskType == riskType
                        && w.Status == AlertStatus.Open
                        && w.ExpiresAt > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(w => w.TenantId == tenantId);
        else
            query = query.Where(w => w.TenantId == null);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWindow>> GetByRegionAsync(string region,
        CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .AsNoTracking()
            .Include(w => w.Signals)
            .Where(w => w.Region == region)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWindow>> GetByStatusAsync(AlertStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .AsNoTracking()
            .Include(w => w.Signals)
            .Where(w => w.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWindow>> GetExpiredOpenWindowsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .Include(w => w.Signals)
            .Where(w => w.Status == AlertStatus.Open && w.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWindow>> GetTriggeredUnconfirmedWindowsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .Include(w => w.Signals)
            .Where(w => w.Status == AlertStatus.Triggered && w.ConfirmedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<AlertWindowPage> SearchAsync(string? region, AlertStatus? status,
        IReadOnlyCollection<string>? tenantIds, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.AlertWindows.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(region)) query = query.Where(w => w.Region == region);
        if (status is not null) query = query.Where(w => w.Status == status);
        if (tenantIds is not null)
        {
            var normalized = tenantIds.Select(t => t.ToLowerInvariant()).ToList();
            query = query.Where(w => w.TenantId != null && normalized.Contains(w.TenantId.ToLower()));
        }

        var totalItems = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(w => w.OpenedAt)
            .ThenBy(w => w.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(w => w.Signals)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return new AlertWindowPage(items, totalItems);
    }

    public async Task AddAsync(AlertWindow window, CancellationToken cancellationToken = default)
    {
        _context.AlertWindows.Add(window);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Added alert window {WindowId}", window.Id);
    }

    /// <summary>
    /// Saves the aggregate with optimistic concurrency on <c>xmin</c>. The root is always marked as modified so that
    /// adding a signal (an insert into alert_signals only) also bumps the window version; a concurrent writer then
    /// gets <see cref="DbUpdateConcurrencyException"/> instead of silently overwriting quorum or status changes.
    /// </summary>
    public async Task UpdateAsync(AlertWindow window, CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(window);
        if (entry.State == EntityState.Detached)
            // Signals keep their key only while tracked; windows must be loaded by this repository instance.
            throw new InvalidOperationException("Alert windows must be loaded through the repository before updating.");

        if (entry.State == EntityState.Unchanged)
            entry.State = EntityState.Modified;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Updated alert window {WindowId}", window.Id);
    }
}