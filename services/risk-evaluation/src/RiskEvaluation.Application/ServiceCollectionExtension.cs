using Microsoft.Extensions.DependencyInjection;
using RiskEvaluation.Application.Handlers;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Interfaces.Services;
using RiskEvaluation.Application.Services;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;

namespace RiskEvaluation.Application;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServiceCollection(this IServiceCollection services)
    {
        return services
            .AddMediatR(config =>
            {
                config.RegisterServicesFromAssembly(typeof(EvaluateRiskCommandHandler).Assembly);
            })
            .AddScoped<IRiskEvaluationService, RiskEvaluationService>();
    }
}
