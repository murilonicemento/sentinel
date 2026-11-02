using Confluent.Kafka;
using Geospatial.Domain.Repositories;
using Geospatial.Domain.Services;
using Geospatial.Infrastructure.Elasticsearch;
using Geospatial.Infrastructure.GeometryEngine;
using Geospatial.Infrastructure.Messaging.Consumers;
using Geospatial.Infrastructure.Options;
using Geospatial.Infrastructure.Persistence.DbContext;
using Geospatial.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        var settings = new ConnectionSettings(new Uri(options.Url))
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
                EnablePartitionEof = true
            };

            return new ConsumerBuilder<Null, string>(config).Build();
        });

        services.AddSingleton<Geospatial.Application.Interfaces.Messaging.IRegionIntersectedPublisher, Messaging.Publishers.RegionIntersectedPublisher>();

        return services;
    }
}