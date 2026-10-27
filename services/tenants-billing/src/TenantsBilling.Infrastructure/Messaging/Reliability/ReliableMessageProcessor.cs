using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace TenantsBilling.Infrastructure.Messaging.Reliability;

/// <summary>
/// Applies the platform's at-least-once policy to one consumed message:
/// <list type="bullet">
/// <item>transient failures are retried with exponential backoff and jitter, up to <see cref="ConsumerReliabilityOptions.MaxProcessingAttempts"/>;</item>
/// <item>rejected messages and non-transient failures go to the dead-letter topic and are never dropped silently;</item>
/// <item>the caller may commit only when this returns true (processed or durably dead-lettered).</item>
/// </list>
/// </summary>
public sealed class ReliableMessageProcessor
{
    private readonly string _consumerGroup;
    private readonly IDeadLetterSink _deadLetterSink;
    private readonly ILogger _logger;
    private readonly ConsumerReliabilityOptions _options;
    private readonly TimeProvider _timeProvider;

    public ReliableMessageProcessor(IDeadLetterSink deadLetterSink, string consumerGroup,
        ConsumerReliabilityOptions options, ILogger logger, TimeProvider? timeProvider = null)
    {
        _deadLetterSink = deadLetterSink;
        _consumerGroup = consumerGroup;
        _options = options;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<bool> HandleAsync<TKey>(ConsumeResult<TKey, string> result,
        Func<CancellationToken, Task<MessageHandlingResult>> handle, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _options.MaxProcessingAttempts; attempt++)
            try
            {
                var outcome = await handle(cancellationToken);
                if (!outcome.IsRejected) return true;

                return await TryDeadLetterAsync(result, outcome.ErrorCode!, outcome.ErrorReason!, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException ||
                                              !cancellationToken.IsCancellationRequested)
            {
                if (!TransientFailureClassifier.IsTransient(exception))
                {
                    // Retrying a permanent failure would block the partition forever.
                    _logger.LogError(exception,
                        "Unexpected error processing {Topic}[{Partition}]@{Offset}; sending to dead-letter",
                        result.Topic, result.Partition.Value, result.Offset.Value);
                    return await TryDeadLetterAsync(result, MessageErrorCodes.UnexpectedProcessingError,
                        "The message could not be processed due to an unexpected error.", cancellationToken);
                }

                if (attempt == _options.MaxProcessingAttempts)
                {
                    _logger.LogError(exception,
                        "{Topic}[{Partition}]@{Offset} failed after {Attempts} attempts; consumption paused",
                        result.Topic, result.Partition.Value, result.Offset.Value, attempt);
                    return false;
                }

                var delay = Backoff(attempt);
                _logger.LogWarning(exception,
                    "Transient failure processing {Topic}[{Partition}]@{Offset}, attempt {Attempt}/{MaxAttempts}; retrying in {DelayMs} ms",
                    result.Topic, result.Partition.Value, result.Offset.Value, attempt,
                    _options.MaxProcessingAttempts, (int)delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }

        return false;
    }

    private async Task<bool> TryDeadLetterAsync<TKey>(ConsumeResult<TKey, string> result, string errorCode,
        string errorReason, CancellationToken cancellationToken)
    {
        try
        {
            await _deadLetterSink.PublishAsync(new DeadLetterEnvelope
            {
                OriginalTopic = result.Topic,
                Partition = result.Partition.Value,
                Offset = result.Offset.Value,
                Key = result.Message.Key?.ToString(),
                Payload = result.Message.Value ?? string.Empty,
                ErrorCode = errorCode,
                ErrorReason = errorReason,
                FailedAt = _timeProvider.GetUtcNow(),
                ConsumerGroup = _consumerGroup
            }, result.Message.Headers, cancellationToken);

            _logger.LogWarning("{Topic}[{Partition}]@{Offset} sent to dead-letter with code {ErrorCode}",
                result.Topic, result.Partition.Value, result.Offset.Value, errorCode);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException ||
                                          !cancellationToken.IsCancellationRequested)
        {
            // Not committing keeps the message; it is read again after the pause.
            _logger.LogError(exception, "Failed to publish {Topic}[{Partition}]@{Offset} to the dead-letter topic",
                result.Topic, result.Partition.Value, result.Offset.Value);
            return false;
        }
    }

    private TimeSpan Backoff(int attempt)
    {
        var baseDelay = _options.InitialRetryDelayMilliseconds * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.NextDouble() * _options.InitialRetryDelayMilliseconds;
        return TimeSpan.FromMilliseconds(baseDelay + jitter);
    }
}
