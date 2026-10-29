using System.Net.Http.Headers;
using Confluent.Kafka;
using Ingestion.Application.Interfaces.Deduplicators;
using Ingestion.Application.Interfaces.HttpClients;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Application.Interfaces.Publishers;
using Ingestion.Application.Interfaces.Repositories;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Infrastructure.Write.Cache.Deduplicators;
using Ingestion.Infrastructure.Write.HostedServices;
using Ingestion.Infrastructure.Write.HttpClients;
using Ingestion.Infrastructure.Write.Messaging.Publishers;
using Ingestion.Infrastructure.Write.Persistence;
using Ingestion.Infrastructure.Write.Persistence.DbContext;
using Ingestion.Infrastructure.Write.Persistence.Repositories;
using Ingestion.Infrastructure.Write.Providers;
using Ingestion.Infrastructure.Write.Security;
using Ingestion.Infrastructure.Write.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Minio;
using Npgsql;
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
            .AddWriteDatabase(configuration)
            .AddRepositories()
            .AddProviders(configuration)
            .AddPublishers(configuration)
            .AddEvents(configuration)
            .AddHostedServices()
            .AddHttpClients(configuration);


    /// <summary>One pooled data source for the process; each scope opens its own connection from it.</summary>
    private static IServiceCollection AddWriteDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IngestionWriteDatabase");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configuration 'ConnectionStrings:IngestionWriteDatabase' is required.");

        return services
            .AddSingleton(_ => NpgsqlDataSource.Create(connectionString))
            .AddScoped<WriteDbContext>()
            .AddScoped<IUnitOfWork, UnitOfWork>();
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services) =>
        services
            .AddScoped<ITenantRepository, TenantRepository>()
            .AddScoped<IDataSourceRepository, DataSourceRepository>()
            .AddScoped<IDataCollectionRepository, DataCollectionRepository>()
            .AddScoped<ISampleSensorRepository, SampleSensorRepository>()
            .AddScoped<IEventTypePermissionRepository, EventTypePermissionRepository>()
            .AddScoped<IOutboxRepository, OutboxRepository>();

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
            .AddScoped<IObjectStorageProvider>(sp => new MinioProvider(
                sp.GetRequiredService<IMinioClient>(),
                configuration["MinIO:BucketName"]
                ?? throw new InvalidOperationException("MinIO:BucketName configuration is required"),
                sp.GetRequiredService<ILogger<MinioProvider>>()));

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
            // One multiplexer per process: it is thread-safe and meant to be shared.
            .AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configurationOptions = ConfigurationOptions.Parse(configuration["ConnectionStrings:Redis"]
                    ?? throw new InvalidOperationException("Configuration 'ConnectionStrings:Redis' is required."));
                configurationOptions.AbortOnConnectFail = false;

                return ConnectionMultiplexer.Connect(configurationOptions);
            })
            .AddScoped<IEventDeduplicator, RedisEventDeduplicator>();

    private static IServiceCollection AddHostedServices(this IServiceCollection services) =>
        services
            .AddHostedService<OutboxHostedService>();
    // .AddHostedService<SensorPollingHostedService>();

    private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var billingBaseUrl = configuration["TenantsBilling:BaseUrl"]
                            ?? "http://tenants-billing:8080";

        var geospatialUrl = configuration["Geospatial:BaseUrl"]
                            ?? throw new InvalidOperationException("Geospatial:BaseUrl configuration is required");
        var fireSensorPollingUrl = configuration["Polling:Fire:BaseUrl"]
                                   ?? throw new InvalidOperationException(
                                       "SensorPolling:BaseUrl configuration is required");
        var earthquakeSensorPollingUrl = configuration["Polling:Earthquake:BaseUrl"]
                                         ?? throw new InvalidOperationException(
                                             "SensorPolling:BaseUrl configuration is required");
        var floodSensorPollingUrl = configuration["Polling:Flood:BaseUrl"]
                                    ?? throw new InvalidOperationException(
                                        "Polling:Flood:BaseUrl configuration is required");
        var landslideSensorPollingUrl = configuration["Polling:Landslide:BaseUrl"]
                                        ?? throw new InvalidOperationException(
                                            "Polling:Landslide:BaseUrl configuration is required");
        var temperatureAnomalySensorPollingUrl = configuration["Polling:TemperatureAnomaly:BaseUrl"]
                                                 ?? throw new InvalidOperationException(
                                                     "Polling:TemperatureAnomaly:BaseUrl configuration is required");
        var humidityAnomalySensorPollingUrl = configuration["Polling:HumidityAnomaly:BaseUrl"]
                                              ?? throw new InvalidOperationException(
                                                  "Polling:HumidityAnomaly:BaseUrl configuration is required");
        var windGustSensorPollingUrl = configuration["Polling:WindGust:BaseUrl"]
                                       ?? throw new InvalidOperationException(
                                           "Polling:WindGust:BaseUrl configuration is required");
        var rainfallSensorPollingUrl = configuration["Polling:Rainfall:BaseUrl"]
                                       ?? throw new InvalidOperationException(
                                           "Polling:Rainfall:BaseUrl configuration is required");
        var pressureChangeSensorPollingUrl = configuration["Polling:PressureChange:BaseUrl"]
                                             ?? throw new InvalidOperationException(
                                                 "Polling:PressureChange:BaseUrl configuration is required");

        services.AddServiceTokens(configuration);

        services
            .AddHttpClient<ITenantBillingGateway, TenantBillingGateway>(client =>
            {
                client.BaseAddress = new Uri(billingBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();

        services
            .AddHttpClient<IGeospatialClient, GeospatialClient>(client =>
            {
                client.BaseAddress = new Uri(geospatialUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services
            .AddHttpClient("Fire", client =>
            {
                client.BaseAddress = new Uri(fireSensorPollingUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient("Earthquake", client =>
            {
                client.BaseAddress = new Uri(earthquakeSensorPollingUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient("Flood", client =>
            {
                var token = configuration["Polling:HumidityAnomaly:Token"] ??
                            throw new ArgumentException("Token is required in config");
                client.BaseAddress = new Uri(floodSensorPollingUrl);
                client.DefaultRequestHeaders.Add("token", token);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient("Landslide", client =>
            {
                client.BaseAddress = new Uri(landslideSensorPollingUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient("TemperatureAnomaly", client =>
            {
                var token = configuration["Polling:HumidityAnomaly:Token"] ??
                            throw new ArgumentException("Token is required in config");
                client.BaseAddress = new Uri(temperatureAnomalySensorPollingUrl);
                client.DefaultRequestHeaders.Add("token", token);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient("HumidityAnomaly", client =>
            {
                var token = configuration["Polling:HumidityAnomaly:Token"] ??
                            throw new ArgumentException("Token is required in config");
                client.BaseAddress = new Uri(humidityAnomalySensorPollingUrl);
                client.DefaultRequestHeaders.Add("token", token);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient("WindGust", client =>
            {
                var apiKey = configuration["Polling:WindGust:APIKey"] ??
                             throw new ArgumentException("APIKey is required in config");
                client.BaseAddress = new Uri(windGustSensorPollingUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient("Rainfall", client =>
            {
                var apiKey = configuration["Polling:Rainfall:APIKey"] ??
                             throw new ArgumentException("APIKey is required in config");
                client.BaseAddress = new Uri(rainfallSensorPollingUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());
        services
            .AddHttpClient("PressureChange", client =>
            {
                var apiKey = configuration["Polling:PressureChange:APIKey"] ??
                             throw new ArgumentException("APIKey is required in config");
                client.BaseAddress = new Uri(pressureChangeSensorPollingUrl);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(CreateRetryPolicy());

        return services
            .AddSingleton<ISensorPollingClientFactory, SensorPollingClientFactory>();
    }

    private static AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => (int)r.StatusCode == 429)
            // 2 retries = 3 attempts including the first (platform limit).
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt =>
                {
                    var backoff = TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt - 1));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
                    return backoff + jitter;
                });
}