using Ingestion.Infrastructure.Read.Persistence.DbContext;
using Microsoft.Extensions.DependencyInjection;

namespace Ingestion.Infrastructure.Read;

public static class InfrastructureReadServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureReadServiceCollection(
        this IServiceCollection services
    ) =>
        services
            .AddSingleton<ReadDbContext>();
}