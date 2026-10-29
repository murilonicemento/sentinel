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

    public async Task RegisterManyAsync(IEnumerable<SampleSensor> sampleSensors)
    {
        var query = @"INSERT INTO
                        sensor_sample
                            (id, data_collection_id, sensor_value, unit, latitude, longitude, recorded_at)
                        VALUES
                            (@Id, @DataCollectionId, @SensorValue, @Unit, @Latitude, @Longitude, @RecordedAt)
                        ON CONFLICT (id) DO NOTHING";
        await _writeDbContext.Connection.ExecuteAsync(query, sampleSensors, _writeDbContext.Transaction);
    }
}
