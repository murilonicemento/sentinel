using Dapper;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;

namespace Ingestion.Infrastructure.Repositories;

public class SampleSensorRepository : ISampleSensorRepository
{
    private readonly IngestionDbContext _context;

    public SampleSensorRepository(IngestionDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> RegisterAsync(SampleSensor sampleSensor)
    {
        var query = @"INSERT INTO 
                        sample_sensor 
                            (id, data_collection_id, sensor_value, unit, recorded_at) 
                        VALUES 
                            (@Id, @DataCollectionId, @SensorValue, @Unit, @RecordedAt)";

        await _context.Connection.ExecuteAsync(query, sampleSensor);

        return sampleSensor.Id;
    }
}