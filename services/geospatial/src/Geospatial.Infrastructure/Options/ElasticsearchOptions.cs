namespace Geospatial.Infrastructure.Options;

public class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";
    public string Url { get; set; } = "http://localhost:9200";
    public string DefaultIndex { get; set; } = "geospatial-events";
}
