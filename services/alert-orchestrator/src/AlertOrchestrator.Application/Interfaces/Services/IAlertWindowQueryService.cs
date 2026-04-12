using AlertOrchestrator.Application.DTOs;

namespace AlertOrchestrator.Application.Interfaces.Services;

public interface IAlertWindowQueryService
{
    public Task<AlertWindowDTO?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlertWindowDTO>> GetByRegionAsync(string region,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<AlertWindowDTO>> GetByStatusAsync(string status,
        CancellationToken cancellationToken = default);
}