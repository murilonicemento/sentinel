using Confluent.Kafka;
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
                options.UseNpgsql(configuration.GetConnectionString("IngestionReadDatabase"));
            })
            .AddRedisCache(configuration)
            .AddKafkaPublisher(configuration)
            .AddRepositories();

    private static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration["ConnectionStrings:Redis"];

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
        var kafkaConnectionString = configuration["ConnectionStrings:Kafka"];

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
            .AddScoped<IEventTypeRepository, EventTypeRepository>()
            .AddScoped<IIDFCurveRepository, IDFCurveRepository>()
            .AddScoped<IRegionalParameterRepository, RegionalParameterRepository>()
            .AddScoped<IRiskMatrixRepository, RiskMatrixRepository>()
            .AddScoped<ISeverityRepository, SeverityRepository>();
}