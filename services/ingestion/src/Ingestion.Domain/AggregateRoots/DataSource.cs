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

    public TEvent MapValueToEventType<TEvent>(double value, double baseline = 1013) where TEvent : Enum
    {
        // Dicionário que liga MeasurementType a função que retorna o enum
        var mapping = new Dictionary<string, Func<double, double, TEvent>>
        {
            // Climatic
            {
                "Temperature",
                (v, b) => (TEvent)(object)(v > 40 ? ClimaticEventEnum.TemperatureAnomaly : ClimaticEventEnum.Normal)
            },
            {
                "Humidity",
                (v, b) => (TEvent)(object)(v < 20 ? ClimaticEventEnum.HumidityAnomaly : ClimaticEventEnum.Normal)
            },
            { "WindSpeed", (v, b) => (TEvent)(object)(v > 80 ? ClimaticEventEnum.WindGust : ClimaticEventEnum.Normal) },
            { "Rainfall", (v, b) => (TEvent)(object)(v > 50 ? ClimaticEventEnum.Rainfall : ClimaticEventEnum.Normal) },
            {
                "Pressure",
                (v, b) => (TEvent)(object)((Math.Abs(v - b) > 20)
                    ? ClimaticEventEnum.PressureChange
                    : ClimaticEventEnum.Normal)
            },

            // Disaster
            { "Fire", (v, b) => (TEvent)(object)(v > 100 ? DisasterEventEnum.Wildfire : DisasterEventEnum.Normal) },
            {
                "Earthquake",
                (v, b) => (TEvent)(object)(v >= 4.0 ? DisasterEventEnum.Earthquake : DisasterEventEnum.Normal)
            },
        };

        if (!mapping.ContainsKey(MeasurementType))
            throw new ArgumentException($"Invalid measurement type: {MeasurementType}");

        return mapping[MeasurementType](value, baseline);
    }

    public bool CanSendEvent(string eventDomain, string eventType)
    {
        return EventPermissions.Any(p =>
            p.EventDomain.Equals(eventDomain, StringComparison.OrdinalIgnoreCase) &&
            p.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));
    }
}