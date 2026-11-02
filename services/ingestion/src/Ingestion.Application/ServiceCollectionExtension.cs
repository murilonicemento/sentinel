using Ingestion.Application.Handlers;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ingestion.Application;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServiceCollection(this IServiceCollection services)
    {
        return services
            .AddMediatR(config =>
            {
                config.RegisterServicesFromAssembly(typeof(RegisterDataSourceHandler).Assembly);
                config.RegisterServicesFromAssembly(typeof(RegisterClimaticEventHandler).Assembly);
                config.RegisterServicesFromAssembly(typeof(RegisterDisasterEventHandler).Assembly);
            })
            .AddScoped<ISensorCollectionService, SensorCollectionService>()
            .AddScoped<IGeospatialValidationService, GeospatialValidationService>();
    }
}