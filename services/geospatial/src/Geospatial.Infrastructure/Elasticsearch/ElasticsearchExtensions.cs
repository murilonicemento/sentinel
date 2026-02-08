using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;

namespace Geospatial.Infrastructure.Elasticsearch;

public static class ElasticsearchExtensions
{
    public static IServiceCollection AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ElasticsearchOptions.SectionName).Get<ElasticsearchOptions>()
            ?? new ElasticsearchOptions();

        var settings = new ConnectionSettings(new Uri(options.Url))
            .DefaultIndex(options.DefaultIndex)
            .DefaultMappingFor<GeospatialEventDocument>(m => m.IndexName("geospatial-events"));

        services.AddSingleton<IElasticClient>(new ElasticClient(settings));
        return services;
    }

    public static async Task EnsureIndexExistsAsync(this IElasticClient client, CancellationToken cancellationToken = default)
    {
        const string indexName = "geospatial-events";
        var indexExists = await client.Indices.ExistsAsync(indexName, ct: cancellationToken);
        if (!indexExists.Exists)
        {
            await client.Indices.CreateAsync(indexName, c => c
                .Map<GeospatialEventDocument>(m => m.AutoMap()), cancellationToken);
        }
    }
}
