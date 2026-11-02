using AlertOrchestrator.Application.Interfaces.Cooldown;
using AlertOrchestrator.Application.Interfaces.Messaging;
using AlertOrchestrator.Application.Interfaces.Observability;
using AlertOrchestrator.Application.Interfaces.Services;
using AlertOrchestrator.Application.Ports;
using AlertOrchestrator.Application.Services;
using AlertOrchestrator.Domain.Interfaces.Repositories;
using AlertOrchestrator.Infrastructure.Cooldown;
using AlertOrchestrator.Infrastructure.HostedServices;
using AlertOrchestrator.Infrastructure.Idempotency;
using AlertOrchestrator.Infrastructure.Messaging.Consumers;
using AlertOrchestrator.Infrastructure.Messaging.Publishers;
using AlertOrchestrator.Infrastructure.Observability;
using AlertOrchestrator.Infrastructure.Options;
using AlertOrchestrator.Infrastructure.Persistence;
using AlertOrchestrator.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAlertOrchestratorInfrastructure(
        this IServiceCollection services,
        AlertOrchestratorInfrastructureOptions options)
    {
        // Observability
        services.AddSingleton<IAlertMetrics, AlertMetrics>();

        // Persistence
        if (options.UsePostgreSql)
        {
            services.AddDbContext<AlertOrchestratorDbContext>(dbOptions =>
            {
                dbOptions.UseNpgsql(options.PostgreSqlConnectionString);
            });
            services.AddScoped<IAlertWindowRepository, PostgreSqlAlertWindowRepository>();
        }
        else
        {
            services.AddSingleton<IAlertWindowRepository, InMemoryAlertWindowRepository>();
        }

        // Caching
        if (options.UseRedis)
        {
            services.AddStackExchangeRedisCache(redisOptions =>
            {
                redisOptions.Configuration = options.RedisConnectionString;
            });
            services.AddSingleton<IIdempotencyService>(sp =>
            {
                var cache = sp.GetRequiredService<IDistributedCache>();
                return new RedisIdempotencyService(cache, options.IdempotencyExpiration);
            });
        }
        else
        {
            services.AddSingleton<IIdempotencyService>(sp =>
                new InMemoryIdempotencyService(options.IdempotencyExpiration));
        }

        services.AddSingleton<IMemoryCache>(sp => new MemoryCache(new MemoryCacheOptions()));

        // Application Services
        services.AddScoped<IAlertWindowQueryService, AlertWindowQueryService>();

        // Cooldown Service
        services.AddSingleton<IAlertCooldownService, DistributedAlertCooldownService>();

        // Configuration
        services.AddSingleton<IAlertConfigurationPort, InMemoryConfigurationPort>();

        // Messaging
        services.AddSingleton<IEventPublisher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KafkaEventPublisher>>();
            return new KafkaEventPublisher(options.KafkaBootstrapServers, options.EventTopic, logger);
        });

        services.AddSingleton<ICommandPublisher>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<KafkaCommandPublisher>>();
            return new KafkaCommandPublisher(options.KafkaBootstrapServers, options.CommandTopic, logger);
        });

        // Consumers (Background Services)
        services.AddHostedService<RiskUpdatedEventConsumer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RiskUpdatedEventConsumer>>();
            return new RiskUpdatedEventConsumer(
                options.KafkaBootstrapServers,
                options.RiskUpdatedTopic,
                options.ConsumerGroupId,
                sp,
                logger);
        });

        services.AddHostedService<RegionIntersectedEventConsumer>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RegionIntersectedEventConsumer>>();
            return new RegionIntersectedEventConsumer(
                options.KafkaBootstrapServers,
                options.RegionIntersectedTopic,
                options.ConsumerGroupId,
                sp,
                logger);
        });

        // Background Workers
        services.AddHostedService<AlertWindowExpirationWorker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AlertWindowExpirationWorker>>();
            return new AlertWindowExpirationWorker(sp, logger, options.ExpirationCheckInterval);
        });

        services.AddHostedService<AlertEscalationWorker>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AlertEscalationWorker>>();
            return new AlertEscalationWorker(sp, logger, options.EscalationCheckInterval ?? TimeSpan.FromMinutes(1));
        });

        return services;
    }
}