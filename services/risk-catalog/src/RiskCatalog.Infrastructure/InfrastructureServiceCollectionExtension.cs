using Confluent.Kafka;
using Ingestion.Infrastructure.Write.HttpClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Infrastructure.Cache;
using RiskCatalog.Infrastructure.DatabaseContext;
using RiskCatalog.Infrastructure.Messaging;
using RiskCatalog.Infrastructure.Repositories;
using StackExchange.Redis;

namespace RiskCatalog.Infrastructure;

public static class InfrastructureServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureServiceCollection(this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddDbContext<RiskCatalogDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("RiskCatalogDatabase"));
            })
            .AddRedisCache(configuration)
            .AddKafkaPublisher(configuration)
            .AddRepositories()
            .AddGeospatialClient(configuration);

    private static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (string.IsNullOrEmpty(redisConnectionString))
            throw new InvalidOperationException("Redis connection string is not configured.");

        return services
            .AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var configurationOptions = ConfigurationOptions.Parse(redisConnectionString);

                configurationOptions.AbortOnConnectFail = false;
                configurationOptions.ConnectRetry = 3;
                configurationOptions.ConnectTimeout = 15000;
                configurationOptions.SyncTimeout = 15000;
                configurationOptions.AsyncTimeout = 15000;

                return ConnectionMultiplexer.Connect(configurationOptions);
            })
            .AddScoped<ICacheService, RedisCacheService>();
    }

    private static IServiceCollection AddKafkaPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        var kafkaConnectionString = configuration.GetConnectionString("Kafka");

        if (string.IsNullOrEmpty(kafkaConnectionString))
            throw new InvalidOperationException("Kafka connection string is not configured.");

        return services
            .AddSingleton<IProducer<Null, string>>(sp =>
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = kafkaConnectionString,
                    Acks = Acks.All,
                    EnableIdempotence = true,
                    MessageTimeoutMs = 5000
                };

                return new ProducerBuilder<Null, string>(config).Build();
            })
            .AddScoped<IPublisher, KafkaPublisher>();
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services) =>
        services
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<IEventTypeRepository, EventTypeRepository>()
            .AddScoped<IIDFCurveRepository, IDFCurveRepository>()
            .AddScoped<IRegionalParameterRepository, RegionalParameterRepository>()
            .AddScoped<IRiskMatrixRepository, RiskMatrixRepository>()
            .AddScoped<ISeverityRepository, SeverityRepository>();

    private static IServiceCollection AddGeospatialClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var geospatialUrl = configuration["Geospatial:BaseUrl"]
                            ?? throw new InvalidOperationException("Geospatial:BaseUrl configuration is required");

        services.AddHttpClient<IGeospatialClient, GeospatialClient>(client =>
        {
            client.BaseAddress = new Uri(geospatialUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}