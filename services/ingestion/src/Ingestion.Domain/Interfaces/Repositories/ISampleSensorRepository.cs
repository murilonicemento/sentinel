using Ingestion.Domain.Aggregates;

namespace Ingestion.Domain.Repositories;

public interface ISampleSensorRepository
{
    public Task<Guid> RegisterAsync(SampleSensor sampleSensor);
}