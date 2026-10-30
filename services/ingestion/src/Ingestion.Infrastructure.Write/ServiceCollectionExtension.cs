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
using Ingestion.Infrastructure.Write.Options;
using Ingestion.Infrastructure.Write.Persistence;
using Ingestion.Infrastructure.Write.Persistence.DbContext;
using Ingestion.Infrastructure.Write.Persistence.Repositories;
using Ingestion.Infrastructure.Write.Providers;
using Ingestion.Infrastructure.Write.Security;
using Ingestion.Infrastructure.Write.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            .AddSingleton<OutboxMetrics>()
            .AddHostedService<OutboxHostedService>()
            .AddHostedService<SensorPollingHostedService>();

    private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var billingBaseUrl = configuration["TenantsBilling:BaseUrl"]
                             ?? throw new InvalidOperationException("Configuration 'TenantsBilling:BaseUrl' is required.");
        var geospatialUrl = configuration["Geospatial:BaseUrl"]
                            ?? throw new InvalidOperationException("Geospatial:BaseUrl configuration is required");

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

        return services.AddPolling(configuration);
    }

    /// <summary>
    /// Every source binds <c>Polling:&lt;Source&gt;</c> and is validated at startup when enabled; only enabled sources
    /// get a client, so a source without credentials stays off instead of failing.
    /// </summary>
    private static IServiceCollection AddPolling(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PollingMetrics>();
        services.AddSingleton<IValidateOptions<PollingSourceOptions>, PollingSourceOptionsValidator>();

        foreach (var source in PollingSources.All)
        {
            var section = configuration.GetSection($"Polling:{source}");
            services.AddOptions<PollingSourceOptions>(source).Bind(section).ValidateOnStart();

            var options = section.Get<PollingSourceOptions>() ?? new PollingSourceOptions();
            if (!options.Enabled) continue;

            services.AddHttpClient(source, client =>
                {
                    // A trailing slash keeps relative request paths under the base path (e.g. /v2 of Stormglass).
                    client.BaseAddress = new Uri(options.BaseUrl!.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromSeconds(30);
                    if (PollingSources.ByStation.Contains(source))
                        client.DefaultRequestHeaders.Add("token", options.ApiKey);
                    else if (source is PollingSources.WindGust or PollingSources.Rainfall or PollingSources.PressureChange)
                        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.ApiKey);
                })
                .AddPolicyHandler(CreateRetryPolicy());
            services.AddTransient<ISensorPollingClient>(sp => CreatePollingClient(source,
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(source), options));
        }

        return services;
    }

    private static ISensorPollingClient CreatePollingClient(string source, HttpClient http,
        PollingSourceOptions options) => source switch
        {
            PollingSources.Fire => new FirePollingHttpClient(http, options.ApiKey!),
            PollingSources.Earthquake => new EarthquakePollingHttpClient(http),
            PollingSources.Flood => new FloodPollingHttpClient(http),
            PollingSources.Landslide => new LandslidePollingHttpClient(http),
            PollingSources.TemperatureAnomaly => new TemperatureAnomalyPollingHttpClient(http),
            PollingSources.HumidityAnomaly => new HumidityAnomalyPollingHttpClient(http),
            PollingSources.WindGust => new WindGustPollingHttpClient(http),
            PollingSources.Rainfall => new RainfallPollingHttpClient(http),
            PollingSources.PressureChange => new PressureChangePollingHttpClient(http),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown polling source.")
        };

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