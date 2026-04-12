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

    public Task AddAsync(AlertWindow window, CancellationToken cancellationToken = default);
    public Task UpdateAsync(AlertWindow window, CancellationToken cancellationToken = default);
}