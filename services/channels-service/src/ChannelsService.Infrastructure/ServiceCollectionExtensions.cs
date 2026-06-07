using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Services;
using ChannelsService.Domain.Interfaces;
using ChannelsService.Infrastructure.HostedServices;
using ChannelsService.Infrastructure.Messaging;
using ChannelsService.Infrastructure.Persistence;
using ChannelsService.Infrastructure.Providers;
using ChannelsService.Infrastructure.Repositories;
using ChannelsService.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChannelsService.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddChannelsServiceInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ChannelsServiceDatabase");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<ChannelsServiceDbContext>(options => options.UseNpgsql(connectionString));
            services.AddScoped<IDeliveryRepository, PostgresDeliveryRepository>();
        }
        else
        {
            services.AddSingleton<IDeliveryRepository, InMemoryDeliveryRepository>();
        }

        var redisConnection = configuration["Redis:Configuration"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redisConnection);
            services.AddSingleton<ConfigurationTenantChannelSettingsProvider>();
            services.AddSingleton<ITenantChannelSettingsProvider, RedisTenantChannelSettingsProvider>();
        }
        else
        {
            services.AddSingleton<ITenantChannelSettingsProvider, ConfigurationTenantChannelSettingsProvider>();
        }

        var emailProviderType = configuration["ChannelsService:Providers:Email:Type"];
        if (string.Equals(emailProviderType, "SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<ChannelProviderSettings<SendGridProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:Email"));
            services.AddSingleton<IChannelProvider, SendGridEmailChannelProvider>();
        }
        else
        {
            services.AddScoped<IChannelProvider, EmailChannelProvider>();
        }

        var smsProviderType = configuration["ChannelsService:Providers:Sms:Type"];
        if (string.Equals(smsProviderType, "Twilio", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<ChannelProviderSettings<TwilioProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:Sms"));
            services.AddSingleton<IChannelProvider, TwilioSmsChannelProvider>();
        }
        else
        {
            services.AddScoped<IChannelProvider, SmsChannelProvider>();
        }

        var whatsappProviderType = configuration["ChannelsService:Providers:WhatsApp:Type"];
        if (string.Equals(whatsappProviderType, "TwilioWhatsApp", StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<ChannelProviderSettings<TwilioProviderOptions>>(
                configuration.GetSection("ChannelsService:Providers:WhatsApp"));
            services.AddSingleton<IChannelProvider, TwilioWhatsAppChannelProvider>();
        }
        else
        {
            services.AddScoped<IChannelProvider, WhatsAppChannelProvider>();
        }

        var mqttProviderType = configuration["ChannelsService:Providers:MQTT:Type"];
        if (!string.Equals(mqttProviderType, "Mqtt", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mqttProviderType, "MQTT", StringComparison.OrdinalIgnoreCase))
            return services
                .AddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>()
                .AddSingleton<INotificationPublisher, KafkaNotificationPublisher>()
                .AddScoped<IChannelDeliveryService, ChannelDeliveryService>()
                .AddScoped<IRetryPolicyEngine, RetryPolicyEngine>()
                .AddScoped<IFallbackExecutor, FallbackExecutor>()
                .AddScoped<IChannelProvider, PushChannelProvider>()
                .AddScoped<IChannelProvider, SirenChannelProvider>()
                .AddHostedService<NotificationConsumerHostedService>();
        services.Configure<ChannelProviderSettings<MqttProviderOptions>>(
            configuration.GetSection("ChannelsService:Providers:MQTT"));
        services.AddSingleton<IChannelProvider, MqttChannelProvider>();

        return services
            .AddSingleton<IDeadLetterPublisher, KafkaDeadLetterPublisher>()
            .AddSingleton<INotificationPublisher, KafkaNotificationPublisher>()
            .AddScoped<IChannelDeliveryService, ChannelDeliveryService>()
            .AddScoped<IRetryPolicyEngine, RetryPolicyEngine>()
            .AddScoped<IFallbackExecutor, FallbackExecutor>()
            .AddScoped<IChannelProvider, PushChannelProvider>()
            .AddScoped<IChannelProvider, SirenChannelProvider>()
            .AddHostedService<NotificationConsumerHostedService>();
    }
}