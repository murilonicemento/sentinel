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

    public Task AddAsync<TEvent>(string domain, TEvent detectedEvent, CancellationToken cancellationToken)
        where TEvent : class;
}
