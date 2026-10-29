using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Infrastructure.Caching;
using RiskEvaluation.Infrastructure.Messaging;
using RiskEvaluation.Infrastructure.Persistence;
using MediatR;
using RiskEvaluation.Application.Interfaces.Messaging;
using StackExchange.Redis;
using RiskEvaluation.Infrastructure.Security;
using RiskEvaluation.Infrastructure.HttpClients;
using RiskEvaluation.Application.Interfaces.HttpClients;
using RiskEvaluation.Infrastructure.Messaging.Reliability;
using RiskEvaluation.Infrastructure.HostedServices;
using Confluent.Kafka;

namespace RiskEvaluation.Infrastructure;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureServiceCollection(this IServiceCollection services, IConfiguration configuration)
    {
        // MongoDB
        services.AddSingleton<IMongoClient>(sp =>
        {
            var connectionString = configuration.GetConnectionString("MongoDb");
            return new MongoClient(connectionString);
        });

        services.AddScoped(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase("RiskEvaluationDatabase");
        });

        // Redis
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException("Configuration 'ConnectionStrings:Redis' is required.");
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            // Keeps the service up while Redis is unavailable; the cache reconnects in the background.
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        // risk-catalog: snapshot copied after each publication (RE-15). No retry policy here: the consumer retries the
        // whole publication, and stacking retries would multiply the calls to risk-catalog.
        if (!Uri.TryCreate(configuration["Services:RiskCatalog:BaseUrl"], UriKind.Absolute, out var riskCatalogBaseUrl))
            throw new InvalidOperationException("Configuration 'Services:RiskCatalog:BaseUrl' must be an absolute URL.");
        services.AddServiceTokens(configuration);
        services.AddHttpClient<IRiskCatalogClient, RiskCatalogHttpClient>(client =>
            {
                client.BaseAddress = riskCatalogBaseUrl;
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddHostedService<CatalogSnapshotBootstrapper>();

        // Repositories
        services.AddScoped<IRiskEvaluationRepository, RiskEvaluationRepository>();
        services.AddScoped<IRiskModelRepository, RiskModelRepository>();
        services.AddScoped<ICatalogVersionRepository, CatalogVersionRepository>();
        services.AddScoped<ICatalogSnapshotRepository, CatalogSnapshotRepository>();
        services.AddScoped<IRiskFactorRepository, RiskFactorRepository>();

        // Cache
        services.AddScoped<IRecentScoresCache, RedisRecentScoresCache>();

        // Domain Services
        services.AddScoped<RiskCalculationService>();

        // Messaging
        services.AddSingleton<IEventPublisher>(sp =>
        {
            var bootstrapServers = configuration["Kafka:BootstrapServers"];
            var topic = configuration["Kafka:Topic"];
            var logger = sp.GetRequiredService<ILogger<KafkaEventPublisher>>();

            return new KafkaEventPublisher(bootstrapServers!, topic!, logger);
        });

        // Message Consumer
        var bootstrapServers = configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Configuration 'Kafka:BootstrapServers' is required.");
        var groupId = configuration["Kafka:GroupId"] ?? "risk-evaluation-group";
        var deadLetterTopic = configuration["Kafka:DeadLetterTopic"] ?? "risk-evaluation-dead-letter";
        var reliabilityOptions = new ConsumerReliabilityOptions();
        services.AddSingleton<IConsumedMessageGuard>(sp =>
            new RedisConsumedMessageGuard(sp.GetRequiredService<IConnectionMultiplexer>()));
        services.AddSingleton<IDeadLetterSink>(_ => new KafkaDeadLetterSink(bootstrapServers, deadLetterTopic));
        services.AddHostedService(sp => new RiskEvaluationEventConsumer(
            new ConsumerBuilder<string?, string>(ReliableConsumerLoop.CreateConfig(bootstrapServers, groupId)).Build(),
            new RiskEvaluationConsumerTopics(),
            sp.GetRequiredService<IConsumedMessageGuard>(),
            new ReliableMessageProcessor(sp.GetRequiredService<IDeadLetterSink>(), groupId, reliabilityOptions,
                sp.GetRequiredService<ILogger<ReliableMessageProcessor>>()),
            reliabilityOptions,
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<RiskEvaluationEventConsumer>>()));

        return services;
    }
}
