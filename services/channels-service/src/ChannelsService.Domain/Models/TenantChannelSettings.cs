using ChannelsService.Domain.Enums;

namespace ChannelsService.Domain.Models;

public sealed class TenantChannelSettings
{
    public string TenantId { get; set; } = string.Empty;
    public List<ChannelType> EnabledChannels { get; set; } = new();
    public Dictionary<ChannelType, int> PriorityOrder { get; set; } = new();
    public Dictionary<ChannelType, int> MaxRetries { get; set; } = new();
    public List<ChannelType> FallbackOrder { get; set; } = new();
}