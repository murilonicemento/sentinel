using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Infrastructure.DatabaseContext;
using RiskCatalog.Infrastructure.Repositories;

namespace RiskCatalog.Infrastructure;

public static class InfrastructureServiceCollectionExtension
{
    public static IServiceCollection AddInfrastructureServiceCollection(this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddDbContext<RiskCatalogDbContext>(options =>
            {
                options.UseNpgsql(configuration.GetConnectionString("IngestionReadDatabase"));
            })
            .AddRepositories();

    private static IServiceCollection AddRepositories(this IServiceCollection services) =>
        services
            .AddScoped<IEventTypeRepository, EventTypeRepository>()
            .AddScoped<IIDFCurveRepository, IDFCurveRepository>()
            .AddScoped<IRegionalParameterRepository, RegionalParameterRepository>()
            .AddScoped<IRiskMatrixRepository, RiskMatrixRepository>();
}