using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Enums;

namespace Ingestion.Domain.AggregateRoots;

public class DataSource
{
    public Guid Id { get; }
    public string Name { get; }
    public string Endpoint { get; }
    public string DataSourceType { get; }
    public string MeasurementType { get; }
    public string CollectionFrequency { get; }
    public Guid TenantId { get; }
    public DateTime CreatedAt { get; } = DateTime.Now;
    public IEnumerable<DataCollection> DataCollections { get; set; } = [];
    public IEnumerable<EventTypePermission> EventPermissions { get; set; } = [];

    public DataSource()
    {
    }

    public DataSource(
        Guid id,
        string name,
        string endpoint,
        string dataSourceType,
        string measurementType,
        string collectionFrequency,
        Guid tenantId
    )
    {
        Id = id;
        Name = name;
        Endpoint = endpoint;
        DataSourceType = dataSourceType;
        MeasurementType = measurementType;
        CollectionFrequency = collectionFrequency;
        TenantId = tenantId;
    }

    public ClimaticEventEnum MapValueToEventType(double value, double baseline = 1013)
    {
        return MeasurementType switch
        {
            "Temperature" when value > 40 => ClimaticEventEnum.TemperatureAnomaly,
            "Humidity" when value < 20 => ClimaticEventEnum.HumidityAnomaly,
            "WindSpeed" when value > 80 => ClimaticEventEnum.WindGust,
            "Rainfall" when value > 50 => ClimaticEventEnum.Rainfall,
            "Pressure" when Math.Abs(value - baseline) > 20 => ClimaticEventEnum.PressureChange,
            _ => ClimaticEventEnum.Normal
        };
    }

    public bool CanSendEvent(string eventDomain, string eventType)
    {
        return EventPermissions.Any(p =>
            p.EventDomain.Equals(eventDomain, StringComparison.OrdinalIgnoreCase) &&
            p.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));
    }
}