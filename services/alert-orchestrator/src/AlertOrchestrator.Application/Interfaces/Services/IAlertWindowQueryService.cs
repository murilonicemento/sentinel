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

    /// <summary>
    /// Pages the windows matching the region and/or status filters, ordered by opening time (newest first) and id.
    /// At least one filter is required; an unknown status raises <see cref="ArgumentException"/>.
    /// </summary>
    public Task<PagedResultDTO<AlertWindowDTO>> SearchAsync(string? region, string? status,
        AlertWindowAccessScope scope, int page, int pageSize, CancellationToken cancellationToken = default);
}