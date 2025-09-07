using Ingestion.Domain.Aggregates;

namespace Ingestion.Domain.AggregateRoots;

public class DataSource(
    Guid id,
    string name,
    string endpoint,
    string dataSourceType,
    string measurementType,
    string collectionFrequency)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public string Endpoint { get; } = endpoint;
    public string DataSourceType { get; } = dataSourceType;
    public string MeasurementType { get; } = measurementType;
    public string CollectionFrequency { get; } = collectionFrequency;
    public IEnumerable<DataCollection> DataCollections { get; set; } = [];
}