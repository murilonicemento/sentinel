using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Services;
using ChannelsService.Domain.Interfaces;
using ChannelsService.Infrastructure.HostedServices;
using ChannelsService.Infrastructure.Providers;
using ChannelsService.Infrastructure.Repositories;
using ChannelsService.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelsService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelsServiceInfrastructure(this IServiceCollection services, IConfiguration configuration) =>
        services
            .AddSingleton<IDeliveryRepository, InMemoryDeliveryRepository>()
            .AddSingleton<ITenantChannelSettingsProvider, ConfigurationTenantChannelSettingsProvider>()
            .AddScoped<IChannelDeliveryService, ChannelDeliveryService>()
            .AddScoped<IRetryPolicyEngine, RetryPolicyEngine>()
            .AddScoped<IFallbackExecutor, FallbackExecutor>()
            .AddScoped<IChannelProvider, SmsChannelProvider>()
            .AddScoped<IChannelProvider, EmailChannelProvider>()
            .AddScoped<IChannelProvider, PushChannelProvider>()
            .AddScoped<IChannelProvider, WhatsAppChannelProvider>()
            .AddScoped<IChannelProvider, SirenChannelProvider>()
            .AddHostedService<NotificationConsumerHostedService>();
}
