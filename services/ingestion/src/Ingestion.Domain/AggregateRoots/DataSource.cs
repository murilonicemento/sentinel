using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Enums;

namespace Ingestion.Domain.AggregateRoots;

public class DataSource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public int CollectionFrequency { get; set; }
    public ICollection<DataCollection> DataCollections { get; set; } = [];
}