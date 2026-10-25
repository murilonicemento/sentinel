using AlertOrchestrator.Application.DTOs;

namespace AlertOrchestrator.Application.Interfaces.Services;

public interface IAlertWindowQueryService
{
    public Task<AlertWindowDTO?> GetByIdAsync(Guid id, AlertWindowAccessScope scope,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlertWindowDTO>> GetByRegionAsync(string region, AlertWindowAccessScope scope,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlertWindowDTO>> GetByStatusAsync(string status, AlertWindowAccessScope scope,
        CancellationToken cancellationToken = default);
}