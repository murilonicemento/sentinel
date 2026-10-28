using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantsBilling.Domain.Events;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.HostedServices;
using TenantsBilling.Infrastructure.InMemory;
using TenantsBilling.Infrastructure.Messaging;
using TenantsBilling.Infrastructure.Persistence;
using TenantsBilling.Infrastructure.Persistence.Repositories;

namespace TenantsBilling.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTenantsBillingInfrastructure(
        this IServiceCollection services,
        string? connectionString,
        string kafkaBootstrapServers,
        string kafkaTopic,
        bool usePostgres = true)
    {
        if (usePostgres && !string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<TenantsBillingDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<ITenantRepository, PostgresTenantRepository>();
            services.AddScoped<IPlanRepository, PostgresPlanRepository>();
            services.AddScoped<ITenantUsageRepository, PostgresTenantUsageRepository>();
        }
        else
        {
            services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
            services.AddSingleton<IPlanRepository, InMemoryPlanRepository>();
            services.AddSingleton<ITenantUsageRepository, InMemoryTenantUsageRepository>();
        }

        services.AddSingleton<IProducer<Null, string>>(_ =>
            new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = kafkaBootstrapServers }).Build());
        services.AddSingleton<ITenantEventPublisher>(sp =>
            new KafkaTenantEventPublisher(sp.GetRequiredService<IProducer<Null, string>>(), kafkaTopic));

        services.AddHostedService<TenantUsageEventConsumerHostedService>();
        services.AddHostedService<TenantDeadLetterConsumerHostedService>();

        return services;
    }
}
