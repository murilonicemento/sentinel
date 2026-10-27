using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace RiskEvaluation.Infrastructure.Messaging.Reliability;

/// <summary>
/// Poll, process and commit loop shared by the service's consumers. Offsets are committed only after
/// <see cref="ReliableMessageProcessor"/> reports a durable outcome; otherwise the consumer seeks back to the same
/// offset and pauses, so no message is skipped while a dependency is down.
/// </summary>
public static class ReliableConsumerLoop
{
    public static ConsumerConfig CreateConfig(string bootstrapServers, string groupId) => new()
    {
        BootstrapServers = bootstrapServers,
        GroupId = groupId,
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false,
        EnableAutoOffsetStore = false,
        EnablePartitionEof = false
    };

    /// <summary>Runs the loop on a dedicated thread, because <c>Consume</c> blocks.</summary>
    public static Task Start<TKey>(IConsumer<TKey, string> consumer, IReadOnlyCollection<string> topics,
        ReliableMessageProcessor processor,
        Func<ConsumeResult<TKey, string>, CancellationToken, Task<MessageHandlingResult>> handle,
        ConsumerReliabilityOptions options, ILogger logger, CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => RunAsync(consumer, topics, processor, handle, options, logger, stoppingToken),
            stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    public static async Task RunAsync<TKey>(IConsumer<TKey, string> consumer, IReadOnlyCollection<string> topics,
        ReliableMessageProcessor processor,
        Func<ConsumeResult<TKey, string>, CancellationToken, Task<MessageHandlingResult>> handle,
        ConsumerReliabilityOptions options, ILogger logger, CancellationToken stoppingToken)
    {
        consumer.Subscribe(topics);
        logger.LogInformation("Consumer subscribed to {Topics}", string.Join(", ", topics));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<TKey, string>? result;
                try
                {
                    result = consumer.Consume(options.PollTimeout);
                }
                catch (ConsumeException exception)
                {
                    logger.LogError(exception, "Error consuming from {Topics}: {Reason}", string.Join(", ", topics),
                        exception.Error.Reason);
                    continue;
                }

                if (result?.Message is null) continue;

                if (await processor.HandleAsync(result, ct => handle(result, ct), stoppingToken))
                {
                    TryCommit(consumer, result, logger);
                    continue;
                }

                consumer.Seek(result.TopicPartitionOffset);
                await Task.Delay(options.PauseAfterExhaustedRetries, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private static void TryCommit<TKey>(IConsumer<TKey, string> consumer, ConsumeResult<TKey, string> result,
        ILogger logger)
    {
        try
        {
            consumer.Commit(result);
        }
        catch (KafkaException exception)
        {
            // The message was handled; if the commit is lost (e.g. during a rebalance) it is redelivered and the
            // handlers' idempotency absorbs the duplicate.
            logger.LogWarning(exception, "Could not commit {Topic}[{Partition}]@{Offset}", result.Topic,
                result.Partition.Value, result.Offset.Value);
        }
    }
}
