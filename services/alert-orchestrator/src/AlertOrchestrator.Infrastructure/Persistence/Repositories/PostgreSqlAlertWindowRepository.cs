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

    public PostgreSqlAlertWindowRepository(AlertOrchestratorDbContext context, ILogger<PostgreSqlAlertWindowRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AlertWindow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .AsNoTracking()
            .Include(w => w.Signals)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<AlertWindow?> GetOpenWindowAsync(string region, RiskType riskType, string? tenantId, CancellationToken cancellationToken = default)
    {
        var query = _context.AlertWindows
            .AsNoTracking()
            .Include(w => w.Signals)
            .Where(w => w.Region == region
                && w.RiskType == riskType
                && w.Status == AlertStatus.Open
                && w.ExpiresAt > DateTime.UtcNow);

        if (!string.IsNullOrEmpty(tenantId))
        {
            query = query.Where(w => w.TenantId == tenantId);
        }
        else
        {
            query = query.Where(w => w.TenantId == null);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWindow>> GetByRegionAsync(string region, CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .AsNoTracking()
            .Include(w => w.Signals)
            .Where(w => w.Region == region)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWindow>> GetByStatusAsync(AlertStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .AsNoTracking()
            .Include(w => w.Signals)
            .Where(w => w.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWindow>> GetExpiredOpenWindowsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AlertWindows
            .AsNoTracking()
            .Include(w => w.Signals)
            .Where(w => w.Status == AlertStatus.Open && w.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AlertWindow window, CancellationToken cancellationToken = default)
    {
        _context.AlertWindows.Add(window);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Added alert window {WindowId}", window.Id);
    }

    public async Task UpdateAsync(AlertWindow window, CancellationToken cancellationToken = default)
    {
        _context.AlertWindows.Update(window);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogDebug("Updated alert window {WindowId}", window.Id);
    }
}
