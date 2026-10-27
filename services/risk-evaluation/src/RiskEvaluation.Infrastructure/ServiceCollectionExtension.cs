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
using RiskEvaluation.Application.Interfaces.HttpClients;
using RiskEvaluation.Application.Interfaces.Messaging;
using RiskEvaluation.Infrastructure.HttpClients;
using RiskEvaluation.Infrastructure.Security;
using StackExchange.Redis;
using RiskEvaluation.Infrastructure.Messaging.Reliability;
using RiskEvaluation.Infrastructure.HostedServices;
using Confluent.Kafka;
using Polly;
using Polly.Extensions.Http;

namespace RiskEvaluation.Infrastructure;

public static class ServiceCollectionExtension
{
    /// <summary>Overall budget per call to risk-catalog or geospatial, retries included.</summary>
    private static readonly TimeSpan DependencyTimeout = TimeSpan.FromSeconds(10);

    // Every call to risk-catalog and geospatial is a read-only query, so retrying is safe.
    // 2 retries = 3 attempts including the first (platform limit).
    private static IAsyncPolicy<HttpResponseMessage> QueryRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => (int)response.StatusCode == 429)
            .WaitAndRetryAsync(2, retryAttempt =>
                TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt - 1)) +
                TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250)));

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
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = ConfigurationOptions.Parse(configuration.GetConnectionString("Redis") ?? "localhost:6379");
            // Keeps the service up while Redis is unavailable; the cache reconnects in the background.
            redisOptions.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(redisOptions);
        });

        services.AddServiceTokens(configuration);

        services.AddHttpClient<IRiskCatalogClient, RiskCatalogHttpClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:RiskCatalog:BaseUrl"] ?? "http://localhost:5002/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = DependencyTimeout;
        })
        .AddHttpMessageHandler<ServiceTokenHandler>()
        .AddPolicyHandler(QueryRetryPolicy());

        services.AddHttpClient<IGeospatialClient, GeospatialHttpClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:Geospatial:BaseUrl"] ?? "http://localhost:5003/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = DependencyTimeout;
        })
        .AddHttpMessageHandler<ServiceTokenHandler>()
        .AddPolicyHandler(QueryRetryPolicy());

        // Repositories
        services.AddScoped<IRiskEvaluationRepository, RiskEvaluationRepository>();
        services.AddScoped<IRiskModelRepository, RiskModelRepository>();
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
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
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
