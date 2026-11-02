using Geospatial.Infrastructure.Options;

namespace Geospatial.Api.Options;

public static class GeospatialOptionsExtensions
{
    /// <summary>
    /// Validated at startup: a missing topic or URL used to surface only when the consumer or the search first ran.
    /// </summary>
    public static IServiceCollection AddGeospatialOptions(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ElasticsearchOptions>()
            .Bind(configuration.GetSection(ElasticsearchOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.Url, UriKind.Absolute, out _),
                "Elasticsearch:Url must be an absolute URL.")
            .ValidateOnStart();
        services.AddOptions<KafkaConsumerOptions>()
            .Bind(configuration.GetSection("KafkaConsumerOptions"))
            .Validate(options => options.SubscribedTopics.Count > 0 &&
                                 options.SubscribedTopics.All(topic => !string.IsNullOrWhiteSpace(topic)) &&
                                 !string.IsNullOrWhiteSpace(options.DeadLetterTopic),
                "KafkaConsumerOptions:Topics and KafkaConsumerOptions:DeadLetterTopic are required.")
            .ValidateOnStart();
        return services;
    }
}
