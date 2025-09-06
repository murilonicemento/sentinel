using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.DbContext;
using Ingestion.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Ingestion.Infrastructure;

public static class InfrastructureServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureServiceCollection(this IServiceCollection services)
    {
        return services
            .AddScoped<IngestionDbContext>()
            .AddScoped<IDataSourceRepository, DataSourceRepository>();
    }
}