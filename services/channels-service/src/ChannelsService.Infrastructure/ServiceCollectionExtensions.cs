using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Services;
using ChannelsService.Infrastructure.HostedServices;
using ChannelsService.Infrastructure.Messaging;
using ChannelsService.Infrastructure.Observability;
using ChannelsService.Infrastructure.Options;
using ChannelsService.Infrastructure.Persistence;
using ChannelsService.Infrastructure.Persistence.Repositories;
using ChannelsService.Infrastructure.Providers;
using ChannelsService.Infrastructure.Settings;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelsService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelsServiceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient();

        return services
            .AddDatabases(configuration)
            .AddOptions()
            .AddProviders()
            .AddPublishers(configuration)
            .AddServices();
    }

    private static IServiceCollection AddDatabases(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ChannelsServiceDatabase");
        var redisConnection = configuration["Redis:Configuration"];

        return services
            .AddDbContext<ChannelsServiceDbContext>(options => options.UseNpgsql(connectionString))
            .AddScoped<IDeliveryRepository, PostgresDeliveryRepository>()
            .AddStackExchangeRedisCache(options => options.Configuration = redisConnection)
            .AddSingleton<ConfigurationTenantChannelSettingsProvider>()
            .AddSingleton<ITenantChannelSettingsProvider, RedisTenantChannelSettingsProvider>();
    }

    private static IServiceCollection AddOptions(this IServiceCollection services, IConfiguration configuration) =>
        services
            .Configure<ChannelProviderSettings<PushProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:Push"))
            .Configure<ChannelProviderSettings<SirenProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:Siren"))
            .Configure<ChannelProviderSettings<MqttProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:MQTT"))
            .Configure<ChannelProviderSettings<SendGridProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:Email"))
            .Configure<ChannelProviderSettings<TwilioProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:Sms"))
            .Configure<ChannelProviderSettings<TwilioProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:WhatsApp"));

    private static IServiceCollection AddProviders(this IServiceCollection services) =>
        services
            .AddScoped<IChannelProvider, WhatsAppChannelProvider>()
            .AddScoped<IChannelProvider, PushChannelProvider>()
            .AddScoped<IChannelProvider, SirenChannelProvider>()
            .AddSingleton<IChannelProvider, SendGridEmailChannelProvider>()
            .AddSingleton<IChannelProvider, TwilioSmsChannelProvider>()
            .AddSingleton<IChannelProvider, TwilioWhatsAppChannelProvider>()
            .AddSingleton<IChannelProvider, MqttChannelProvider>();

    private static IServiceCollection AddPublishers(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton<IProducer<Null, string>>(sp =>
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = configuration["Kafka:BootstrapServers"]!,
                    Acks = Acks.All,
                    EnableIdempotence = true,
                    MessageTimeoutMs = 5000
                };

                return new ProducerBuilder<Null, string>(config).Build();
            })
            .AddSingleton<INotificationPublisher, KafkaNotificationPublisher>()
            .AddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>();

    private static IServiceCollection AddServices(this IServiceCollection services) =>
        services
            .AddScoped<IChannelDeliveryService, ChannelDeliveryService>()
            .AddScoped<IRetryPolicyEngine, RetryPolicyEngine>()
            .AddScoped<IFallbackExecutor, FallbackExecutor>()
            .AddSingleton<IChannelMetrics, ChannelMetrics>()
            .AddHostedService<NotificationConsumerHostedService>();
}