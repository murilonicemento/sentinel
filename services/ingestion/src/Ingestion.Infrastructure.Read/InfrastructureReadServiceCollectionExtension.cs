using Ingestion.Infrastructure.Read.Persistence.DbContext;
using Microsoft.Extensions.DependencyInjection;

namespace Ingestion.Infrastructure.Read;

public static class InfrastructureServiceCollectionExtension
{
    public static IServiceCollection AddReadInfrastructureServiceCollection(
        this IServiceCollection services
    ) =>
        services
            .AddSingleton<ReadDbContext>();
}