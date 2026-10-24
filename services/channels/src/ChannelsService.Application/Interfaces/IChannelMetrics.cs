using ChannelsService.Domain.Enums;

namespace ChannelsService.Application.Interfaces;

public interface IChannelMetrics
{
    void RecordNotificationDelivery(bool success, int attemptedChannelCount, int failedChannelCount);
    void RecordChannelAttempt(ChannelTypeEnum channel, string providerName, bool success, bool retried, int attemptCount);
    void RecordChannelLatency(ChannelTypeEnum channel, string providerName, double durationSeconds, string status);
    void RecordDeadLetterPublished(bool success);
}
