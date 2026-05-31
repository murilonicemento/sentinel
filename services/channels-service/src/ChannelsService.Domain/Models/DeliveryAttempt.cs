using ChannelsService.Domain.Enums;

namespace ChannelsService.Domain.Models;

public sealed class DeliveryAttempt
{
    public string AttemptId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public ChannelType Channel { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}