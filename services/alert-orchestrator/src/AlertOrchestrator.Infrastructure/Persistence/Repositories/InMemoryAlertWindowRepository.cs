using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using AlertOrchestrator.Domain.ValueObjects;

namespace AlertOrchestrator.Infrastructure.Persistence.Repositories;

public sealed class InMemoryAlertWindowRepository : IAlertWindowRepository
{
    private readonly Dictionary<Guid, AlertWindow> _windows = new();
    private readonly Lock _lock = new();

    public Task<AlertWindow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _windows.TryGetValue(id, out var window);
            return Task.FromResult(window);
        }
    }

    public Task<AlertWindow?> GetOpenWindowAsync(string region, RiskType riskType, string? tenantId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var window = _windows.Values.FirstOrDefault(w =>
                w.Region == region &&
                w.RiskType == riskType &&
                w.Status == AlertStatus.Open &&
                w.ExpiresAt > DateTime.UtcNow &&
                w.TenantId == tenantId);

            return Task.FromResult(window);
        }
    }

    public Task<IReadOnlyList<AlertWindow>> GetByRegionAsync(string region, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var windows = _windows.Values.Where(w => w.Region == region).ToList();
            return Task.FromResult<IReadOnlyList<AlertWindow>>(windows);
        }
    }

    public Task<IReadOnlyList<AlertWindow>> GetByStatusAsync(AlertStatus status, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var windows = _windows.Values.Where(w => w.Status == status).ToList();
            return Task.FromResult<IReadOnlyList<AlertWindow>>(windows);
        }
    }

    public Task<IReadOnlyList<AlertWindow>> GetExpiredOpenWindowsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var windows = _windows.Values
                .Where(w => w.Status == AlertStatus.Open && w.ExpiresAt <= DateTime.UtcNow)
                .ToList();
            return Task.FromResult<IReadOnlyList<AlertWindow>>(windows);
        }
    }

    public Task AddAsync(AlertWindow window, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _windows[window.Id] = window;
        }
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AlertWindow window, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _windows[window.Id] = window;
        }
        return Task.CompletedTask;
    }
}
