using System.Text.Json.Serialization;
using ChannelsService.Domain.Enums;

namespace ChannelsService.Domain.Models;

public sealed class NotificationEvent
{
    public string EventId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public NotificationPriority Priority { get; set; } = NotificationPriority.Medium;

    public NotificationMessage Message { get; set; } = new();
    public List<ChannelType> Channels { get; set; } = new();
    public Dictionary<string, string>? Metadata { get; set; }
}