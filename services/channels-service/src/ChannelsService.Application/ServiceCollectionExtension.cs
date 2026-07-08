using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelsService.Application;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddApplicationServiceCollection(this IServiceCollection services) =>
        services
            .AddScoped<IChannelDeliveryService, ChannelDeliveryService>()
            .AddScoped<IRetryPolicyEngine, RetryPolicyEngine>()
            .AddScoped<IFallbackExecutor, FallbackExecutor>();
}