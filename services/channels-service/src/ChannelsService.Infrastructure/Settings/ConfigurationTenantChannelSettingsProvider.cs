using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Configuration;

namespace ChannelsService.Infrastructure.Settings;

public sealed class ConfigurationTenantChannelSettingsProvider : ITenantChannelSettingsProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationTenantChannelSettingsProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TenantChannelSettings GetSettings(string tenantId)
    {
        var tenantSection = _configuration.GetSection($"ChannelsService:Tenants:{tenantId}");
        if (tenantSection.Exists())
        {
            var settings = tenantSection.Get<TenantChannelSettings>();
            if (settings != null)
            {
                return settings;
            }
        }

        var defaultSection = _configuration.GetSection("ChannelsService:DefaultTenantSettings");
        var defaultSettings = defaultSection.Get<TenantChannelSettings>() ?? GetFallbackDefault();
        return defaultSettings;
    }

    private static TenantChannelSettings GetFallbackDefault() => new()
    {
        TenantId = "default",
        EnabledChannels = new List<ChannelType> { ChannelType.Sms, ChannelType.Email, ChannelType.Push },
        PriorityOrder = new Dictionary<ChannelType, int>
        {
            [ChannelType.Sms] = 1,
            [ChannelType.Email] = 2,
            [ChannelType.Push] = 3,
            [ChannelType.WhatsApp] = 4,
            [ChannelType.Siren] = 5
        },
        MaxRetries = new Dictionary<ChannelType, int>
        {
            [ChannelType.Sms] = 3,
            [ChannelType.Email] = 2,
            [ChannelType.Push] = 2,
            [ChannelType.WhatsApp] = 2,
            [ChannelType.Siren] = 3
        },
        FallbackOrder = new List<ChannelType> { ChannelType.Sms, ChannelType.Push, ChannelType.Email }
    };
}
