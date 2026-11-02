using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace RiskEvaluation.Infrastructure.Security;

public static class ServiceTokenServiceCollectionExtensions
{
    public static IServiceCollection AddServiceTokens(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ServiceTokenOptions>()
            .Bind(configuration.GetSection(ServiceTokenOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "ServiceToken:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "ServiceToken:Audience is required.")
            .Validate(options => options.SigningKey.Length >= 32,
                "ServiceToken:SigningKey is required and must have at least 32 characters.")
            .Validate(options => options.Roles.Count > 0, "ServiceToken:Roles requires at least one role.")
            .Validate(options => options.LifetimeMinutes is >= 1 and <= 60,
                "ServiceToken:LifetimeMinutes must be between 1 and 60.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ServiceTokenProvider>();
        services.AddTransient<ServiceTokenHandler>();

        return services;
    }
}
