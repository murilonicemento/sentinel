using Confluent.Kafka;
using Ingestion.Application.Interfaces.Events;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;
using Ingestion.Infrastructure.Events;
using Ingestion.Infrastructure.HostedServices;
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
    )
    {
        return services
            .AddSingleton<IngestionDbContext>()
            .AddRepositories()
            .AddMinioConfiguration(configuration)
            .AddPublishers(configuration)
            .AddEvents(configuration)
            .AddHostedServices();
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services
            .AddScoped<IDataSourceRepository, DataSourceRepository>()
            .AddScoped<IDataCollectionRepository, DataCollectionRepository>()
            .AddScoped<ISampleSensorRepository, SampleSensorRepository>()
            .AddScoped<IOutboxRepository, OutboxRepository>();
    }

    private static IServiceCollection AddMinioConfiguration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        return services.AddMinio(cfg =>
        {
            cfg.WithEndpoint(configuration["MinIO:Host"])
                .WithCredentials(configuration["MinIO:AccessKey"], configuration["MinIO:SecretKey"])
                .Build();
        });
    }

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
        });


    private static IServiceCollection AddEvents(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configurationOptions = ConfigurationOptions.Parse(configuration["ConnectionStrings:Redis"]!);
                configurationOptions.AbortOnConnectFail = false;

                return ConnectionMultiplexer.Connect(configurationOptions);
            })
            .AddSingleton<IEventDeduplicator, RedisEventDeduplicator>();

    private static IServiceCollection AddHostedServices(this IServiceCollection services) =>
        services.AddHostedService<OutboxHostedService>();
}