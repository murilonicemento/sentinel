using Microsoft.Extensions.DependencyInjection;

namespace RiskCatalog.Application;

public static class ApplicationServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServiceCollection(this IServiceCollection services) =>
        services
            .AddMediatR(config =>
            {
                // config.RegisterServicesFromAssembly(typeof(RegisterDataSourceHandler).Assembly);
            });
}