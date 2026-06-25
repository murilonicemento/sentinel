using ChannelsService.Domain.Enums;

namespace ChannelsService.Domain.Entities;

public sealed class TenantChannelSettings
{
    public string TenantId { get; set; } = string.Empty;
    public List<ChannelTypeEnum> EnabledChannels { get; set; } = new();
    public Dictionary<ChannelTypeEnum, int> PriorityOrder { get; set; } = new();
    public Dictionary<ChannelTypeEnum, int> MaxRetries { get; set; } = new();
    public List<ChannelTypeEnum> FallbackOrder { get; set; } = new();
}