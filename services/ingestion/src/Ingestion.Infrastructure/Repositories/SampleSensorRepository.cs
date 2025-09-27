using Dapper;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;

namespace Ingestion.Infrastructure.Repositories;

public class SampleSensorRepository : ISampleSensorRepository
{
    private readonly IngestionDbContext _ingestionDbContext;

    public SampleSensorRepository(IngestionDbContext ingestionDbContext)
    {
        _ingestionDbContext = ingestionDbContext;
    }

    public async Task<Guid> RegisterAsync(SampleSensor sampleSensor)
    {
        var query = @"INSERT INTO 
                        sample_sensor 
                            (id, data_collection_id, sensor_value, unit, latitude, longitude, recorded_at) 
                        VALUES 
                            (@Id, @DataCollectionId, @SensorValue, @Unit, @Latitude, @Longitude, @RecordedAt)";

        await _ingestionDbContext.Connection.ExecuteAsync(query, sampleSensor);

        return sampleSensor.Id;
    }
}