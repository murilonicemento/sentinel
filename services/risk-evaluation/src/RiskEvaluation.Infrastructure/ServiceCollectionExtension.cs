using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Infrastructure.Messaging;
using RiskEvaluation.Infrastructure.Persistence;
using MediatR;

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
            return client.GetDatabase("RiskEvaluationDb");
        });

        // Repositories
        services.AddScoped<IRiskEvaluationRepository, RiskEvaluationRepository>();

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
