using System.Text.Json.Serialization;
using ChannelsService.Domain.Enums;

namespace ChannelsService.Domain.Entities;

public sealed class NotificationEvent
{
    public string EventId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public bool FallbackEnabled { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationPriority Priority { get; set; } = NotificationPriority.Medium;

    public NotificationMessage Message { get; set; } = new();
    public List<ChannelTypeEnum> Channels { get; set; } = new();
    public Dictionary<string, string>? Metadata { get; set; }
}