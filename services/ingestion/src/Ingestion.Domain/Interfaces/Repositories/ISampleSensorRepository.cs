using Ingestion.Domain.Aggregates;

namespace Ingestion.Domain.Interfaces.Repositories;

public interface ISampleSensorRepository
{
    public Task<Guid> RegisterAsync(SampleSensor sampleSensor);
}