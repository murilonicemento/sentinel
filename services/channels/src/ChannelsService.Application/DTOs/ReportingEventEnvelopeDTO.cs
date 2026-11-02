using System.Globalization;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.DTOs;

/// <summary>
/// Payload of <c>reporting-events</c>, consumed by reporting (X-24). channels reports one <c>NotificationSent</c> or
/// <c>NotificationFailed</c> per channel it tried.
/// </summary>
public sealed record ReportingEventEnvelopeDTO(
    string EventId,
    string EventType,
    string TenantId,
    string Region,
    string Severity,
    string Channel,
    double RiskScore,
    string Status,
    DateTime Timestamp,
    string Source)
{
    /// <summary>
    /// The outcome of one channel. Region and risk score come from the notification's metadata (alert commands carry
    /// both); without them the region is <c>unknown</c> and the score 0. <c>EventId</c> derives from the tenant, the
    /// notification, the channel and the outcome, so a redelivered notification is stored once per outcome. Null
    /// without a tenant or a notification id.
    /// </summary>
    public static ReportingEventEnvelopeDTO? ForDelivery(NotificationEvent notification, ChannelTypeEnum channel,
        bool delivered, DateTime occurredAt)
    {
        if (string.IsNullOrWhiteSpace(notification.TenantId) || string.IsNullOrWhiteSpace(notification.EventId))
            return null;

        var metadata = notification.Metadata ?? new Dictionary<string, string>();
        var region = metadata.TryGetValue("region", out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : "unknown";
        var riskScore = metadata.TryGetValue("riskScore", out var score) &&
                        double.TryParse(score, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                        parsed is >= 0 and <= 1
            ? parsed
            : 0;
        var channelName = channel.ToString().ToLowerInvariant();
        var eventType = delivered ? "NotificationSent" : "NotificationFailed";

        return new ReportingEventEnvelopeDTO(
            $"{eventType}:{notification.TenantId}:{notification.EventId}:{channelName}",
            eventType,
            notification.TenantId,
            region,
            notification.PriorityEnum.ToString().ToUpperInvariant(),
            channelName,
            riskScore,
            delivered ? "SUCCESS" : "FAILED",
            occurredAt.ToUniversalTime(),
            "channels");
    }
}
