using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace ChannelsService.Infrastructure.Settings;

public sealed class ConfigurationTenantChannelSettingsProvider : ITenantChannelSettingsProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationTenantChannelSettingsProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TenantChannelDTO GetSettings(string tenantId)
    {
        var tenantSection = _configuration.GetSection($"ChannelsService:Tenants:{tenantId}");
        if (tenantSection.Exists())
        {
            var settings = tenantSection.Get<TenantChannelDTO>();
            if (settings != null)
            {
                return settings;
            }
        }

        var defaultSection = _configuration.GetSection("ChannelsService:DefaultTenantSettings");
        var defaultSettings = defaultSection.Get<TenantChannelDTO>() ?? GetFallbackDefault();
        return defaultSettings;
    }

    private static TenantChannelDTO GetFallbackDefault() => new()
    {
        TenantId = "default",
        EnabledChannels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email, ChannelTypeEnum.Push, ChannelTypeEnum.Mqtt },
        PriorityOrder = new Dictionary<ChannelTypeEnum, int>
        {
            [ChannelTypeEnum.Sms] = 1,
            [ChannelTypeEnum.Email] = 2,
            [ChannelTypeEnum.Push] = 3,
            [ChannelTypeEnum.Mqtt] = 4,
            [ChannelTypeEnum.WhatsApp] = 5,
            [ChannelTypeEnum.Siren] = 6
        },
        MaxRetries = new Dictionary<ChannelTypeEnum, int>
        {
            [ChannelTypeEnum.Sms] = 3,
            [ChannelTypeEnum.Email] = 2,
            [ChannelTypeEnum.Push] = 2,
            [ChannelTypeEnum.Mqtt] = 2,
            [ChannelTypeEnum.WhatsApp] = 2,
            [ChannelTypeEnum.Siren] = 3
        },
        FallbackOrder = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Push, ChannelTypeEnum.Email }
    };
}
