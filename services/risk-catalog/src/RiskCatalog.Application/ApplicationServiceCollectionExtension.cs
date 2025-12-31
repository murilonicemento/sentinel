using Microsoft.Extensions.DependencyInjection;
using RiskCatalog.Application.Handlers;

namespace RiskCatalog.Application;

public static class ApplicationServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServiceCollection(this IServiceCollection services) =>
        services
            .AddMediatR(config =>
            {
                config.RegisterServicesFromAssembly(typeof(EventTypeByCodeHandler).Assembly);
                config.RegisterServicesFromAssembly(typeof(EventTypeHandler).Assembly);
                config.RegisterServicesFromAssembly(typeof(IDFCurvesForEventTypeHandler).Assembly);
                config.RegisterServicesFromAssembly(typeof(RegionalRiskParameterHandler).Assembly);
                config.RegisterServicesFromAssembly(typeof(RiskMatrixForEventTypeHandler).Assembly);
                config.RegisterServicesFromAssembly(typeof(SeverityCriterionForEventTypeHandler).Assembly);
            });
}