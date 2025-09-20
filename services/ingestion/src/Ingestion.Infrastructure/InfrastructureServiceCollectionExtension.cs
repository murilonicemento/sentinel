using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;
using Ingestion.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Ingestion.Infrastructure;

public static class InfrastructureServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureServiceCollection(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        return services
            .AddSingleton<IngestionDbContext>()
            .AddRepositories()
            .AddMinioConfiguration(configuration);
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services
            .AddScoped<IDataSourceRepository, DataSourceRepository>()
            .AddScoped<IDataCollectionRepository, DataCollectionRepository>()
            .AddScoped<ISampleSensorRepository, SampleSensorRepository>();
    }

    private static IServiceCollection AddMinioConfiguration(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        return services.AddMinio(cfg =>
        {
            cfg.WithEndpoint(configuration["MinIO:Host"])
                .WithCredentials(configuration["MinIO:AccessKey"], configuration["MinIO:SecretKey"])
                .Build();
        });
    }
}