using Ingestion.Application.DTO;
using Ingestion.Application.Events;

namespace Ingestion.Application.Interfaces.Repositories;

public interface IDetectedEventReadRepository
{
    public Task<IReadOnlyList<SensorEventDetected>> GetLatestAsync(int limit, CancellationToken cancellationToken);

    public Task<CollectionStatisticsResponseDTO?> GetStatisticsAsync(
        DateTime? initialDate,
        DateTime? endDate,
        CancellationToken cancellationToken);

    /// <summary>Detected events of the scoped tenants, newest first (ties broken by event id).</summary>
    public Task<PagedResultDTO<DetectedEventDTO>> GetPageAsync(
        TenantAccessScope scope,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    public Task<DetectedEventStatisticsDTO> GetStatisticsAsync(
        TenantAccessScope scope,
        DateTime? initialDate,
        DateTime? endDate,
        CancellationToken cancellationToken);

    public Task AddAsync(DetectedEventDTO detectedEvent, CancellationToken cancellationToken);
}
