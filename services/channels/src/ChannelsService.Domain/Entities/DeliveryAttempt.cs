using ChannelsService.Domain.Enums;

namespace ChannelsService.Domain.Entities;

public sealed class DeliveryAttempt
{
    public string AttemptId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public ChannelTypeEnum Channel { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DeliveryStatusEnum StatusEnum { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}