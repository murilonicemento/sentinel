using Confluent.Kafka;
using Geospatial.Domain.Repositories;
using Geospatial.Domain.Services;
using Geospatial.Infrastructure.Elasticsearch;
using Geospatial.Infrastructure.GeometryEngine;
using Geospatial.Infrastructure.Messaging.Consumers;
using Geospatial.Infrastructure.Messaging.Reliability;
using Geospatial.Infrastructure.Messaging;
using Geospatial.Infrastructure.Options;
using Geospatial.Infrastructure.Persistence.DbContext;
using Geospatial.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;

namespace Geospatial.Infrastructure;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureWriteServiceCollection(
        this IServiceCollection services,
        IConfiguration configuration
    ) =>
        services
            .AddSingleton<ApplicationDbContext>()
            .AddRepositories()
            .AddGeospatialCalculator()
            .AddElasticsearch(configuration)
            .AddHostedServices()
            .AddConsumer(configuration);

    private static IServiceCollection AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ElasticsearchOptions.SectionName).Get<ElasticsearchOptions>()
                      ?? new ElasticsearchOptions();

        if (!Uri.TryCreate(options.Url, UriKind.Absolute, out var url))
            throw new InvalidOperationException("Configuration 'Elasticsearch:Url' must be an absolute URL.");

        var settings = new ConnectionSettings(url)
            .DefaultIndex(options.DefaultIndex)
            .DefaultMappingFor<GeospatialEventDocument>(m => m.IndexName("geospatial-events"));

        services.AddSingleton<IElasticClient>(new ElasticClient(settings));
        return services;
    }

    private static IServiceCollection AddHostedServices(this IServiceCollection services) =>
        services
            .AddHostedService<SensorEventDetectedConsumer>();

    private static IServiceCollection AddRepositories(this IServiceCollection services) =>
        services
            .AddSingleton<IGeospatialEventRepository, ElasticsearchGeospatialEventRepository>() ;

    private static IServiceCollection AddGeospatialCalculator(this IServiceCollection services) =>
        services.AddScoped<IGeospatialCalculator, NetTopologyGeospatialCalculator>();

    private static IServiceCollection AddConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        var kafkaConnectionString = configuration.GetConnectionString("Kafka")
                                    ?? throw new InvalidOperationException(
                                        "Kafka connection string is not configured.");

        services.AddSingleton<IProducer<Null, string>>(sp =>
        {
            var config = new ProducerConfig
            {
                BootstrapServers = kafkaConnectionString,
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageTimeoutMs = 5000
            };

            return new ProducerBuilder<Null, string>(config).Build();
        });

        services.AddSingleton<IConsumer<Null, string>>(sp =>
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = kafkaConnectionString,
                GroupId = "geospatial-service-consumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false,
                EnablePartitionEof = false
            };

            return new ConsumerBuilder<Null, string>(config).Build();
        });

        services.AddSingleton<Geospatial.Application.Interfaces.Messaging.IRegionIntersectedPublisher, Messaging.Publishers.RegionIntersectedPublisher>();

        var reliability = new ConsumerReliabilityOptions();
        services.AddSingleton(reliability);
        services.AddSingleton<IDeadLetterSink>(sp => new GeospatialDeadLetterSink(
            sp.GetRequiredService<IProducer<Null, string>>(),
            sp.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value.DeadLetterTopic));
        services.AddSingleton(sp => new ReliableMessageProcessor(sp.GetRequiredService<IDeadLetterSink>(),
            "geospatial-service-consumer", reliability,
            sp.GetRequiredService<ILogger<ReliableMessageProcessor>>()));

        return services;
    }
}