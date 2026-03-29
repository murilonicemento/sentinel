using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.IntegrationClients;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Infrastructure.Caching;
using RiskEvaluation.Infrastructure.IntegrationClients;
using RiskEvaluation.Infrastructure.Messaging;
using RiskEvaluation.Infrastructure.Persistence;
using MediatR;
using StackExchange.Redis;

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
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            return ConnectionMultiplexer.Connect(connectionString);
        });

        services.AddHttpClient<IRiskCatalogClient, RiskCatalogHttpClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:RiskCatalog:BaseUrl"] ?? "http://localhost:5002/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<IGeospatialClient, GeospatialHttpClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Services:Geospatial:BaseUrl"] ?? "http://localhost:5003/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("X-Api-Key", configuration["Services:Geospatial:ApiKey"] ?? string.Empty);
        });

        // Repositories
        services.AddScoped<IRiskEvaluationRepository, RiskEvaluationRepository>();
        services.AddScoped<IRiskModelRepository, RiskModelRepository>();

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
        services.AddSingleton<IMessageConsumer>(sp =>
        {
            var bootstrapServers = configuration["Kafka:BootstrapServers"];
            var groupId = configuration["Kafka:GroupId"];
            var mediator = sp.GetRequiredService<IMediator>();
            var logger = sp.GetRequiredService<ILogger<KafkaMessageConsumer>>();
            
            return new KafkaMessageConsumer(bootstrapServers!, groupId!, mediator, logger);
        });

        return services;
    }
}
