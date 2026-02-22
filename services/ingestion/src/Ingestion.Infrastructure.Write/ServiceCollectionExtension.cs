using Confluent.Kafka;
using Ingestion.Application.Events;
using Ingestion.Application.Interfaces.Deduplicators;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Application.Interfaces.Publishers;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.Write.Cache.Deduplicators;
using Ingestion.Infrastructure.Write.HostedServices;
using Ingestion.Infrastructure.Write.HttpClients;
using Ingestion.Infrastructure.Write.Messaging.Publishers;
using Ingestion.Infrastructure.Write.Persistence.DbContext;
using Ingestion.Infrastructure.Write.Persistence.Repositories;
using Ingestion.Infrastructure.Write.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using StackExchange.Redis;

namespace Ingestion.Infrastructure.Write;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureWriteServiceCollection(
        this IServiceCollection services,
        IConfiguration configuration
    ) =>
        services
            .AddSingleton<WriteDbContext>()
            .AddRepositories()
            .AddProviders(configuration)
            .AddPublishers(configuration)
            .AddEvents(configuration)
            .AddHostedServices()
            .AddHttpClients(configuration);


    private static IServiceCollection AddRepositories(this IServiceCollection services) =>
        services
            .AddScoped<ITenantRepository, TenantRepository>()
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
            .AddSingleton<IPublisher, SensorEventDetectedPublisher>();


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
        services
            .AddHostedService<OutboxHostedService>()
            .AddHostedService<SensorPollingHostedService>();

    private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var geospatialUrl = configuration["Geospatial:BaseUrl"]
                            ?? throw new InvalidOperationException("Geospatial:BaseUrl configuration is required");
        var fireSensorPollingUrl = configuration["Polling:Fire:BaseUrl"]
                                   ?? throw new InvalidOperationException(
                                       "SensorPolling:BaseUrl configuration is required");
        var earthquakeSensorPollingUrl = configuration["Polling:Earthquake:BaseUrl"]
                                         ?? throw new InvalidOperationException(
                                             "SensorPolling:BaseUrl configuration is required");

        services
            .AddHttpClient<IGeospatialClient, GeospatialClient>(client =>
            {
                client.BaseAddress = new Uri(geospatialUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
        services
            .AddHttpClient<ISensorPollingClient, FirePollingHttpClient>(client =>
            {
                client.BaseAddress = new Uri(fireSensorPollingUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient<ISensorPollingClient, EarthquakePollingHttpClient>(client =>
            {
                client.BaseAddress = new Uri(earthquakeSensorPollingUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());

        return services;
    }

    private static AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                {
                    var backoff = TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt - 1));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
                    return backoff + jitter;
                });
}