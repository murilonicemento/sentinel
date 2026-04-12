using Dapper;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Infrastructure.Write.Persistence.DbContext;

namespace Ingestion.Infrastructure.Write.Persistence.Repositories;

public class SampleSensorRepository : ISampleSensorRepository
{
    private readonly WriteDbContext _writeDbContext;

    public SampleSensorRepository(WriteDbContext writeDbContext)
    {
        _writeDbContext = writeDbContext;
    }

    public async Task<Guid> RegisterAsync(SampleSensor sampleSensor)
    {
        var query = @"INSERT INTO 
                        sample_sensor 
                            (id, data_collection_id, sensor_value, unit, latitude, longitude, recorded_at) 
                        VALUES 
                            (@Id, @DataCollectionId, @SensorValue, @Unit, @Latitude, @Longitude, @RecordedAt)";

        await _writeDbContext.Connection.ExecuteAsync(query, sampleSensor);

        return sampleSensor.Id;
    }
}