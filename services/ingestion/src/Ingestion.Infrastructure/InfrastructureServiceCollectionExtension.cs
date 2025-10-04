using Confluent.Kafka;
using Ingestion.Application.Interfaces.Deduplicators;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Application.Interfaces.Publishers;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;
using Ingestion.Infrastructure.Deduplicators;
using Ingestion.Infrastructure.HostedServices;
using Ingestion.Infrastructure.Providers;
using Ingestion.Infrastructure.Publishers;
using Ingestion.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using StackExchange.Redis;

namespace Ingestion.Infrastructure;

public static class InfrastructureServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureServiceCollection(
        this IServiceCollection services,
        IConfiguration configuration
    ) =>
        services
            .AddSingleton<IngestionDbContext>()
            .AddRepositories()
            .AddProviders(configuration)
            .AddPublishers(configuration)
            .AddEvents(configuration)
            .AddHostedServices();


    private static IServiceCollection AddRepositories(this IServiceCollection services) =>
        services
            .AddScoped<IDataSourceRepository, DataSourceRepository>()
            .AddScoped<IDataCollectionRepository, DataCollectionRepository>()
            .AddScoped<ISampleSensorRepository, SampleSensorRepository>()
            .AddSingleton<IOutboxRepository, OutboxRepository>();

    private static IServiceCollection AddProviders(
        this IServiceCollection services,
        IConfiguration configuration
    ) =>
        services.AddMinio(cfg =>
            {
                cfg.WithEndpoint(configuration["MinIO:Host"], Convert.ToInt32(configuration["MinIO:Port"]))
                    .WithCredentials(configuration["MinIO:AccessKey"], configuration["MinIO:SecretKey"])
                    .WithSSL(false)
                    .Build();
            })
            .AddScoped<IObjectStorageProvider, MinioProvider>();

    private static IServiceCollection AddPublishers(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton<IProducer<Null, string>>(sp =>
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = configuration["ConnectionStrings:Kafka"]!,
                    Acks = Acks.All,
                    EnableIdempotence = true,
                    MessageTimeoutMs = 5000
                };

                return new ProducerBuilder<Null, string>(config).Build();
            })
            .AddSingleton<IPublisher, KafkaPublisher>();


    private static IServiceCollection AddEvents(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddScoped<IConnectionMultiplexer>(sp =>
            {
                var configurationOptions = ConfigurationOptions.Parse(configuration["ConnectionStrings:Redis"]!);
                configurationOptions.AbortOnConnectFail = false;

                return ConnectionMultiplexer.Connect(configurationOptions);
            })
            .AddScoped<IEventDeduplicator, RedisEventDeduplicator>();

    private static IServiceCollection AddHostedServices(this IServiceCollection services) =>
        services.AddHostedService<OutboxHostedService>();
}