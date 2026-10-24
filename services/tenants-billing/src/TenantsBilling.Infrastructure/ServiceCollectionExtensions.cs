using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantsBilling.Domain.Repositories;
using TenantsBilling.Infrastructure.HostedServices;
using TenantsBilling.Infrastructure.InMemory;
using TenantsBilling.Infrastructure.Persistence;
using TenantsBilling.Infrastructure.Persistence.Repositories;

namespace TenantsBilling.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTenantsBillingInfrastructure(
        this IServiceCollection services,
        string? connectionString,
        bool usePostgres = true)
    {
        if (usePostgres && !string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<TenantsBillingDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<ITenantRepository, PostgresTenantRepository>();
            services.AddScoped<IPlanRepository, PostgresPlanRepository>();
        }
        else
        {
            services.AddSingleton<ITenantRepository, InMemoryTenantRepository>();
            services.AddSingleton<IPlanRepository, InMemoryPlanRepository>();
        }

        services.AddHostedService<TenantUsageEventConsumerHostedService>();
        services.AddHostedService<TenantDeadLetterConsumerHostedService>();

        return services;
    }
}
