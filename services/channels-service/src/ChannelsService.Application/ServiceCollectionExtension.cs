using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelsService.Application;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServiceCollection(this IServiceCollection services) =>
        services
            .AddScoped<IChannelDeliveryService, Services.ChannelDeliveryService>()
            .AddScoped<IRetryPolicyEngine, Services.RetryPolicyEngine>()
            .AddScoped<IFallbackExecutor, Services.FallbackExecutor>();
}
