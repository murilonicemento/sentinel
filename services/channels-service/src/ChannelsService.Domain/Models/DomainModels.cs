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

public sealed class NotificationMessage
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public sealed class DeliveryAttempt
{
    public string EventId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public ChannelType Channel { get; set; }
    public DeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class DeliveryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class TenantChannelSettings
{
    public string TenantId { get; set; } = string.Empty;
    public List<ChannelType> EnabledChannels { get; set; } = new();
    public Dictionary<ChannelType, int> PriorityOrder { get; set; } = new();
    public Dictionary<ChannelType, int> MaxRetries { get; set; } = new();
    public List<ChannelType> FallbackOrder { get; set; } = new();
}

public enum NotificationPriority
{
    Low,
    Medium,
    High,
    Critical
}
