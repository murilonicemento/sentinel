using Microsoft.Extensions.DependencyInjection;
using RiskCatalog.Application.Services;
using RiskCatalog.Application.Services.Interfaces;

namespace RiskCatalog.Application;

public static class ApplicationServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServiceCollection(this IServiceCollection services) =>
        services
            .AddScoped<IRiskModelService, RiskModelService>()
            .AddScoped<IEventTypeService, EventTypeService>();
}