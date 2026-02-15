using Ingestion.Application.Handlers;
using Ingestion.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ingestion.Application;

public static class ApplicationServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServiceCollection(this IServiceCollection services)
    {
        return services
            .AddMediatR(config =>
            {
                config.RegisterServicesFromAssembly(typeof(RegisterDataSourceHandler).Assembly);
                config.RegisterServicesFromAssembly(typeof(RegisterSensorCollectionHandler).Assembly);
            })
            .AddScoped<IGeospatialValidationService, GeospatialValidationService>();
    }
}