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
using AlertOrchestrator.Infrastructure.Messaging.Reliability;
using AlertOrchestrator.Infrastructure.Observability;
using AlertOrchestrator.Infrastructure.Options;
using AlertOrchestrator.Infrastructure.Persistence;
using AlertOrchestrator.Infrastructure.Persistence.Repositories;
using AlertOrchestrator.Infrastructure.Security;
using AlertOrchestrator.Infrastructure.Services;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

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
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var redisOptions = ConfigurationOptions.Parse(options.RedisConnectionString);
                redisOptions.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(redisOptions);
            });
            services.AddSingleton<IIdempotencyService>(sp =>
                new RedisIdempotencyService(sp.GetRequiredService<IConnectionMultiplexer>(),
                    options.IdempotencyExpiration));
        }
        else
        {
            services.AddSingleton<IIdempotencyService>(sp =>
                new InMemoryIdempotencyService(options.IdempotencyExpiration));
        }

        services.AddSingleton<IMemoryCache>(sp => new MemoryCache(new MemoryCacheOptions()));

        // Application Services
        services.AddScoped<IAlertWindowQueryService, AlertWindowQueryService>();

        // Tenant Billing Gateway
        if (!Uri.TryCreate(options.TenantsBillingBaseUrl, UriKind.Absolute, out var tenantsBillingBaseUrl))
            throw new InvalidOperationException("Configuration 'Services:TenantsBillingBaseUrl' must be an absolute URL.");
        services.AddHttpClient<TenantBillingGateway>((sp, client) =>
        {
            client.BaseAddress = tenantsBillingBaseUrl;
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddHttpMessageHandler<ServiceTokenHandler>();
        services.AddScoped<ITenantBillingGateway>(sp => 
            sp.GetRequiredService<TenantBillingGateway>());

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
        var reliabilityOptions = new ConsumerReliabilityOptions();
        services.AddSingleton<IDeadLetterSink>(_ =>
            new KafkaDeadLetterSink(options.KafkaBootstrapServers, options.DeadLetterTopic));
        services.AddSingleton(sp => new ReliableMessageProcessor(sp.GetRequiredService<IDeadLetterSink>(),
            options.ConsumerGroupId, reliabilityOptions,
            sp.GetRequiredService<ILoggerFactory>().CreateLogger<ReliableMessageProcessor>()));

        services.AddHostedService(sp => new RiskUpdatedEventConsumer(
            BuildConsumer(options),
            options.RiskUpdatedTopic,
            sp,
            sp.GetRequiredService<ReliableMessageProcessor>(),
            reliabilityOptions,
            sp.GetRequiredService<ILogger<RiskUpdatedEventConsumer>>()));

        services.AddHostedService(sp => new RegionIntersectedEventConsumer(
            BuildConsumer(options),
            options.RegionIntersectedTopic,
            sp,
            sp.GetRequiredService<ReliableMessageProcessor>(),
            reliabilityOptions,
            sp.GetRequiredService<ILogger<RegionIntersectedEventConsumer>>()));

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

    private static IConsumer<string, string> BuildConsumer(AlertOrchestratorInfrastructureOptions options) =>
        new ConsumerBuilder<string, string>(
            ReliableConsumerLoop.CreateConfig(options.KafkaBootstrapServers, options.ConsumerGroupId)).Build();
}