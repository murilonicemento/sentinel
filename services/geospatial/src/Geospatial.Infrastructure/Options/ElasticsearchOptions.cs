namespace Geospatial.Infrastructure.Options;

public class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";
    public string Url { get; set; } = string.Empty;
    public string DefaultIndex { get; set; } = "geospatial-events";
}
