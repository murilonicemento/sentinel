namespace ChannelsService.Infrastructure.Messaging.Reliability;

public sealed class ConsumerReliabilityOptions
{
    /// <summary>Attempts per message for transient failures, including the first one (platform limit: 3).</summary>
    public int MaxProcessingAttempts { get; init; } = 3;

    public int InitialRetryDelayMilliseconds { get; init; } = 500;

    /// <summary>Pause before re-reading a message whose processing is still failing after every attempt.</summary>
    public TimeSpan PauseAfterExhaustedRetries { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan PollTimeout { get; init; } = TimeSpan.FromSeconds(1);
}
