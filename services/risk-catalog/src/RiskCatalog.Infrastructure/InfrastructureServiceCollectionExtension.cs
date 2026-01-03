using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Infrastructure.Cache;
using RiskCatalog.Infrastructure.DatabaseContext;
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
                configurationOptions.ConnectTimeout = 5000;
                configurationOptions.SyncTimeout = 5000;

                return ConnectionMultiplexer.Connect(configurationOptions);
            })
            .AddScoped<ICacheService, RedisCacheService>();
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services) =>
        services
            .AddScoped<IEventTypeRepository, EventTypeRepository>()
            .AddScoped<IIDFCurveRepository, IDFCurveRepository>()
            .AddScoped<IRegionalParameterRepository, RegionalParameterRepository>()
            .AddScoped<IRiskMatrixRepository, RiskMatrixRepository>()
            .AddScoped<ISeverityRepository, SeverityRepository>();
}