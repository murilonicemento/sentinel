using System.Diagnostics.Metrics;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Enums;

namespace ChannelsService.Infrastructure.Observability;

public sealed class ChannelMetrics : IChannelMetrics
{
    private static readonly Meter Meter = new("ChannelsService", "1.0.0");

    private readonly Counter<long> _notificationDeliveries;
    private readonly Counter<long> _channelAttempts;
    private readonly Histogram<double> _deliveryLatency;
    private readonly Counter<long> _deadLetterPublishes;

    public ChannelMetrics()
    {
        _notificationDeliveries = Meter.CreateCounter<long>(
            "channels_service_notifications_total",
            description: "Total number of notification deliveries processed");

        _channelAttempts = Meter.CreateCounter<long>(
            "channels_service_channel_attempts_total",
            description: "Total number of channel delivery attempts");

        _deliveryLatency = Meter.CreateHistogram<double>(
            "channels_service_delivery_latency_seconds",
            unit: "s",
            description: "Delivery latency in seconds for channel attempts");

        _deadLetterPublishes = Meter.CreateCounter<long>(
            "channels_service_dead_letter_publishes_total",
            description: "Total number of dead-letter publishes");
    }

    public void RecordNotificationDelivery(bool success, int attemptedChannelCount, int failedChannelCount)
    {
        _notificationDeliveries.Add(1,
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
    }

    public void RecordChannelAttempt(ChannelTypeEnum channel, string providerName, bool success, bool retried, int attemptCount)
    {
        _channelAttempts.Add(1,
            new KeyValuePair<string, object?>("channel", channel.ToString()),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"),
            new KeyValuePair<string, object?>("retried", retried.ToString()),
            new KeyValuePair<string, object?>("attempt_count", attemptCount));
    }

    public void RecordChannelLatency(ChannelTypeEnum channel, string providerName, double durationSeconds, string status)
    {
        _deliveryLatency.Record(durationSeconds,
            new KeyValuePair<string, object?>("channel", channel.ToString()),
            new KeyValuePair<string, object?>("provider", providerName),
            new KeyValuePair<string, object?>("status", status));
    }

    public void RecordDeadLetterPublished(bool success)
    {
        _deadLetterPublishes.Add(1,
            new KeyValuePair<string, object?>("result", success ? "success" : "failure"));
    }
}
