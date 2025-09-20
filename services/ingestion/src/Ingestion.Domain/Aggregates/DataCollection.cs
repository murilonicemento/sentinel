using System.Runtime.InteropServices;
using Ingestion.Domain.AggregateRoots;

namespace Ingestion.Domain.Aggregates;

public class DataCollection(
    Guid id,
    Guid dataSourceId,
    DateTime collectedAt,
    string payload,
    Guid tenantId
)
{
    public Guid Id { get; } = id;
    public Guid DataSourceId { get; } = dataSourceId;
    public DateTime CollectedAt { get; } = collectedAt;
    public string Payload { get; } = payload;
    public Guid TenantId { get; } = tenantId;
    public DateTime CreatedAt { get; } = DateTime.Now;
    public DataSource? DataSource { get; set; }
    public IEnumerable<SampleSensor> SampleSensors { get; set; } = [];

    // TODO: Calcular intensidade com base no tipo de sensor e no tipo de medidas
    public double CalculateIntensity(string dataSourceType)
    {
        return dataSourceType switch
        {
            "Sensor" => SampleSensors.Average(sampleSensor => sampleSensor.SensorValue),
            "Api" => SampleSensors.First().SensorValue,
            "File" => SampleSensors.Max(sampleSensor => sampleSensor.SensorValue),
            _ => throw new NotSupportedException(
                $"Tipo de fonte {DataSource?.DataSourceType} não suportado para cálculo de intensidade.")
        };
    }
}