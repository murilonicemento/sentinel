using Ingestion.Application.DTO;

namespace Ingestion.Application.Interfaces.Services;

public interface ISensorCollectionService
{
    public Task<Guid> ProcessSensorCollection<TEvent>(
        Guid dataSourceId,
        Guid tenantId,
        DateTime collectedAt,
        string payload,
        List<SampleSensorDTO> samples,
        string domain,
        CancellationToken cancellationToken) where TEvent : class;
}