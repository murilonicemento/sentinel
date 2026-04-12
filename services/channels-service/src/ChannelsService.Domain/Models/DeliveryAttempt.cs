using ChannelsService.Domain.Enums;

namespace ChannelsService.Domain.Models;

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