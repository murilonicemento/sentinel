using Microsoft.Extensions.DependencyInjection;
using Reporting.Application.Interfaces;
using Reporting.Application.Services;

namespace Reporting.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<ReportingQueryService>();
        services.AddScoped<ReportingHealthService>();
        services.AddScoped<IReportingEventConsumer, ReportingEventConsumer>();
        services.AddScoped<IReportingEventNormalizer, ReportingEventNormalizer>();
        return services;
    }
}
