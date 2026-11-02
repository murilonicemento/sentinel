using AlertOrchestrator.Domain.Aggregates;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.ValueObjects;

namespace AlertOrchestrator.Domain.Interfaces.Repositories;

public interface IAlertWindowRepository
{
    public Task<AlertWindow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    public Task<AlertWindow?> GetOpenWindowAsync(string region, RiskType riskType, string? tenantId,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlertWindow>> GetByRegionAsync(string region,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlertWindow>> GetByStatusAsync(AlertStatus status,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlertWindow>> GetExpiredOpenWindowsAsync(CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlertWindow>> GetTriggeredUnconfirmedWindowsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Windows matching the filters, newest first (ties broken by id). <paramref name="tenantIds"/> null means every
    /// tenant; otherwise only windows of those tenants (compared case-insensitively) are returned.
    /// </summary>
    public Task<AlertWindowPage> SearchAsync(string? region, AlertStatus? status,
        IReadOnlyCollection<string>? tenantIds, int page, int pageSize, CancellationToken cancellationToken = default);

    public Task AddAsync(AlertWindow window, CancellationToken cancellationToken = default);
    public Task UpdateAsync(AlertWindow window, CancellationToken cancellationToken = default);
}

public sealed record AlertWindowPage(IReadOnlyList<AlertWindow> Items, long TotalItems);
