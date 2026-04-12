using ChannelsService.Domain.Models;

namespace ChannelsService.Application.Interfaces;

public interface ITenantChannelSettingsProvider
{
    TenantChannelSettings GetSettings(string tenantId);
}