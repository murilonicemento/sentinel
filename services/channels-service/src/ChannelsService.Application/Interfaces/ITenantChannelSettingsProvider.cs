using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface ITenantChannelSettingsProvider
{
    TenantChannelSettings GetSettings(string tenantId);
}