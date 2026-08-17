using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TenantsBilling.Infrastructure.HostedServices;

public sealed class TenantDeadLetterConsumerHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantDeadLetterConsumerHostedService> _logger;

    public TenantDeadLetterConsumerHostedService(
        IConfiguration configuration,
        ILogger<TenantDeadLetterConsumerHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            _logger.LogWarning("Kafka bootstrap servers are not configured. Tenant dead-letter consumer will not start.");
            return;
        }

        var deadLetterTopic = _configuration["Kafka:DeadLetterTopic"] ?? "tenant-usage-events-dlq";
        var replayTopic = _configuration["Kafka:ReplayTopic"] ?? "tenant-usage-events-replay";
        var groupId = _configuration["Kafka:DeadLetterConsumerGroupId"] ?? "tenants-billing-dlq-group";

        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        })
        .SetErrorHandler((_, error) => _logger.LogError("Kafka DLQ consumer error: {Reason}", error.Reason))
        .Build();

        consumer.Subscribe(deadLetterTopic);
        _logger.LogInformation("Tenant DLQ consumer subscribed to topic {Topic}. Replay topic configured as {ReplayTopic}", deadLetterTopic, replayTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    if (consumeResult?.Message?.Value is null)
                    {
                        continue;
                    }

                    var deadLetter = ParseDeadLetterEnvelope(consumeResult.Message.Value);
                    if (deadLetter is null)
                    {
                        _logger.LogWarning("Skipping malformed dead-letter payload from topic {Topic}", deadLetterTopic);
                        consumer.Commit(consumeResult);
                        continue;
                    }

                    if (!ShouldReplay(deadLetter))
                    {
                        _logger.LogWarning(
                            "Dead-letter payload from {SourceTopic} was classified as terminal. Reason: {Reason}, attempts: {Attempts}",
                            deadLetter.SourceTopic,
                            deadLetter.Reason,
                            deadLetter.Attempts);
                        consumer.Commit(consumeResult);
                        continue;
                    }

                    var replayPayload = JsonSerializer.Serialize(new
                    {
                        sourceTopic = deadLetter.SourceTopic,
                        originalPayload = deadLetter.OriginalPayload,
                        replayRequestedAt = DateTime.UtcNow,
                        replayRequestedBy = deadLetter.ReplayRequestedBy ?? "dlq-consumer"
                    });

                    await PublishReplayAsync(replayTopic, replayPayload, deadLetter.SourceTopic, stoppingToken);
                    _logger.LogInformation("Scheduled replay for dead-letter payload from {SourceTopic} to {ReplayTopic}", deadLetter.SourceTopic, replayTopic);
                    consumer.Commit(consumeResult);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error processing tenant dead-letter message");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    public static TenantDeadLetterEnvelope? ParseDeadLetterEnvelope(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            return new TenantDeadLetterEnvelope(
                GetString(root, "sourceTopic"),
                GetString(root, "reason"),
                GetInt(root, "attempts"),
                GetInt(root, "partition"),
                GetLong(root, "offset"),
                GetString(root, "originalPayload"),
                GetBoolean(root, "replayRequested"),
                GetString(root, "replayRequestedBy"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool ShouldReplay(TenantDeadLetterEnvelope? deadLetter)
    {
        if (deadLetter is null)
        {
            return false;
        }

        if (deadLetter.ReplayRequested)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(deadLetter.OriginalPayload))
        {
            return false;
        }

        if (string.Equals(deadLetter.Reason, "Invalid payload structure", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return deadLetter.Attempts > 0 && (
            deadLetter.Reason.Contains("failed after retries", StringComparison.OrdinalIgnoreCase) ||
            deadLetter.Reason.Contains("retry", StringComparison.OrdinalIgnoreCase));
    }

    private async Task PublishReplayAsync(string replayTopic, string replayPayload, string sourceTopic, CancellationToken cancellationToken)
    {
        try
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"],
                EnableIdempotence = true,
                Acks = Acks.All,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 1000
            };

            using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();
            var result = await producer.ProduceAsync(replayTopic, new Message<Null, string>
            {
                Value = replayPayload,
                Headers = new Headers
                {
                    { "source-topic", Encoding.UTF8.GetBytes(sourceTopic) },
                    { "replay-requested-at", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")) },
                    { "replay-requested-by", Encoding.UTF8.GetBytes("dlq-consumer") }
                }
            }, cancellationToken);

            _logger.LogInformation("Published replay payload to topic {ReplayTopic} at partition {Partition} offset {Offset}", replayTopic, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to publish replay payload to topic {ReplayTopic}", replayTopic);
        }
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            ? property.TryGetInt32(out var value) ? value : 0
            : 0;
    }

    private static long GetLong(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            ? property.TryGetInt64(out var value) ? value : 0
            : 0;
    }

    private static bool GetBoolean(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.True;
    }
}

public sealed record TenantDeadLetterEnvelope(
    string SourceTopic,
    string Reason,
    int Attempts,
    int Partition,
    long Offset,
    string OriginalPayload,
    bool ReplayRequested,
    string? ReplayRequestedBy);
