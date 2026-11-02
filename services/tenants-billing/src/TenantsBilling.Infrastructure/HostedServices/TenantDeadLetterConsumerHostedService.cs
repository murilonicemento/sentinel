using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TenantsBilling.Infrastructure.Messaging;
using TenantsBilling.Infrastructure.Messaging.Reliability;

namespace TenantsBilling.Infrastructure.HostedServices;

/// <summary>
/// Reads <c>tenant-usage-events-dlq</c> and schedules replays on <c>tenant-usage-events-replay</c> for entries that are
/// replayable. The offset is committed only after the replay is published (or the entry is classified as terminal),
/// so a broker failure never drops a dead letter.
/// </summary>
public sealed class TenantDeadLetterConsumerHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantDeadLetterConsumerHostedService> _logger;
    private readonly ConsumerReliabilityOptions _options = new();
    private IProducer<Null, string>? _replayProducer;
    private string _replayTopic = "tenant-usage-events-replay";

    public TenantDeadLetterConsumerHostedService(
        IConfiguration configuration,
        ILogger<TenantDeadLetterConsumerHostedService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            _logger.LogWarning("Kafka bootstrap servers are not configured. Tenant DLQ consumer will not start.");
            return Task.CompletedTask;
        }

        var deadLetterTopic = _configuration["Kafka:DeadLetterTopic"] ?? "tenant-usage-events-dlq";
        _replayTopic = _configuration["Kafka:ReplayTopic"] ?? "tenant-usage-events-replay";
        var groupId = _configuration["Kafka:DeadLetterConsumerGroupId"] ?? "tenants-billing-dlq-group";

        _replayProducer = new ProducerBuilder<Null, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 30_000
        }).Build();
        var consumer = new ConsumerBuilder<Ignore, string>(ReliableConsumerLoop.CreateConfig(bootstrapServers, groupId))
            .SetErrorHandler((_, error) => _logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();
        var processor = new ReliableMessageProcessor(new NoDeadLetterSink(), groupId, _options, _logger);

        return ReliableConsumerLoop.Start(consumer, [deadLetterTopic], processor, HandleAsync, _options, _logger,
            stoppingToken);
    }

    private async Task<MessageHandlingResult> HandleAsync(ConsumeResult<Ignore, string> result,
        CancellationToken cancellationToken)
    {
        var deadLetter = ParseDeadLetterEnvelope(result.Message.Value);
        if (deadLetter is null)
        {
            _logger.LogWarning("Skipping malformed dead-letter payload at {Topic}[{Partition}]@{Offset}",
                result.Topic, result.Partition.Value, result.Offset.Value);
            return MessageHandlingResult.Processed;
        }

        if (!ShouldReplay(deadLetter))
        {
            _logger.LogWarning(
                "Dead-letter payload from {SourceTopic} was classified as terminal. Reason: {Reason}, attempts: {Attempts}",
                deadLetter.SourceTopic, deadLetter.Reason, deadLetter.Attempts);
            return MessageHandlingResult.Processed;
        }

        var replayPayload = JsonSerializer.Serialize(new
        {
            sourceTopic = deadLetter.SourceTopic,
            originalPayload = deadLetter.OriginalPayload,
            replayRequestedAt = DateTime.UtcNow,
            replayRequestedBy = deadLetter.ReplayRequestedBy ?? "dlq-consumer"
        });

        var message = new Message<Null, string>
        {
            Value = replayPayload,
            Headers = new Headers
            {
                { "source-topic", Encoding.UTF8.GetBytes(deadLetter.SourceTopic) },
                { "replay-requested-at", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")) },
                { "replay-requested-by", Encoding.UTF8.GetBytes("dlq-consumer") }
            }
        };
        using var activity = KafkaTracing.StartPublish(_replayTopic, message.Headers);
        var delivery = await _replayProducer!.ProduceAsync(_replayTopic, message, cancellationToken);

        _logger.LogInformation("Scheduled replay for dead-letter payload from {SourceTopic} to {ReplayTopic} at offset {Offset}",
            deadLetter.SourceTopic, _replayTopic, delivery.Offset.Value);
        return MessageHandlingResult.Processed;
    }

    public override void Dispose()
    {
        _replayProducer?.Flush(TimeSpan.FromSeconds(5));
        _replayProducer?.Dispose();
        base.Dispose();
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

/// <summary>
/// Entries of the dead-letter topic are never dead-lettered again: a non-transient failure while replaying keeps the
/// offset uncommitted (consumption pauses) until an operator intervenes.
/// </summary>
internal sealed class NoDeadLetterSink : IDeadLetterSink
{
    public Task PublishAsync(DeadLetterEnvelope envelope, Headers? originalHeaders, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("The dead-letter consumer has no dead-letter topic of its own.");
}
