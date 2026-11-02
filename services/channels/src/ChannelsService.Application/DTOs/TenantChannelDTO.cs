using ChannelsService.Domain.Enums;

namespace ChannelsService.Application.DTOs;

public sealed class TenantChannelDTO
{
    public string TenantId { get; set; } = string.Empty;
    public List<ChannelTypeEnum> EnabledChannels { get; set; } = new();
    public Dictionary<ChannelTypeEnum, int> PriorityOrder { get; set; } = new();
    public Dictionary<ChannelTypeEnum, int> MaxRetries { get; set; } = new();
    public List<ChannelTypeEnum> FallbackOrder { get; set; } = new();
}