using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TenantsBilling.Application.Services;
using TenantsBilling.Infrastructure.Messaging;
using TenantsBilling.Infrastructure.Messaging.Reliability;

namespace TenantsBilling.Infrastructure.HostedServices;

/// <summary>
/// Consumes <c>tenant-usage-events</c> and the replays scheduled on <c>tenant-usage-events-replay</c> with
/// at-least-once delivery: the offset is committed only after the usage is registered or the message is published to
/// <c>tenant-usage-events-dlq</c>. Invalid payloads are dead-lettered as "Invalid payload structure", which the DLQ
/// consumer treats as terminal.
/// </summary>
public sealed class TenantUsageEventConsumerHostedService : BackgroundService
{
    public const string InvalidPayloadReason = "Invalid payload structure";

    private readonly Func<IConsumer<Ignore, string>>? _consumerFactory;
    private readonly ILogger<TenantUsageEventConsumerHostedService> _logger;
    private readonly ConsumerReliabilityOptions _options;
    private readonly Func<ReliableMessageProcessor>? _processorFactory;
    private readonly string _replayTopic;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _topic;

    public TenantUsageEventConsumerHostedService(
        IConfiguration configuration,
        ILogger<TenantUsageEventConsumerHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = new ConsumerReliabilityOptions();
        _topic = configuration["Kafka:UsageTopic"] ?? "tenant-usage-events";
        _replayTopic = configuration["Kafka:ReplayTopic"] ?? "tenant-usage-events-replay";

        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers)) return;

        var groupId = configuration["Kafka:ConsumerGroupId"] ?? "tenants-billing-consumer-group";
        var deadLetterTopic = configuration["Kafka:DeadLetterTopic"] ?? "tenant-usage-events-dlq";

        _consumerFactory = () => new ConsumerBuilder<Ignore, string>(
                ReliableConsumerLoop.CreateConfig(bootstrapServers, groupId))
            .SetErrorHandler((_, error) => _logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();
        _processorFactory = () => new ReliableMessageProcessor(
            new TenantUsageDeadLetterSink(bootstrapServers, deadLetterTopic, _options.MaxProcessingAttempts),
            groupId, _options, logger);
    }

    /// <summary>Used by tests to drive the loop with a fake consumer and dead-letter sink.</summary>
    public TenantUsageEventConsumerHostedService(IConsumer<Ignore, string> consumer, string topic, string replayTopic,
        ReliableMessageProcessor processor, ConsumerReliabilityOptions options, IServiceScopeFactory scopeFactory,
        ILogger<TenantUsageEventConsumerHostedService> logger)
    {
        _consumerFactory = () => consumer;
        _processorFactory = () => processor;
        _topic = topic;
        _replayTopic = replayTopic;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_consumerFactory is null || _processorFactory is null)
        {
            _logger.LogWarning("Kafka bootstrap servers are not configured. Tenant usage consumer will not start.");
            return Task.CompletedTask;
        }

        return ReliableConsumerLoop.Start(_consumerFactory(), [_topic, _replayTopic], _processorFactory(), HandleAsync,
            _options, _logger, stoppingToken);
    }

    private async Task<MessageHandlingResult> HandleAsync(ConsumeResult<Ignore, string> result,
        CancellationToken cancellationToken)
    {
        var payload = result.Topic == _replayTopic ? ParseReplayPayload(result.Message.Value) : result.Message.Value;
        var usageEvent = payload is null ? null : ParseUsageEvent(payload);
        if (usageEvent is null)
            return MessageHandlingResult.Reject(MessageErrorCodes.InvalidPayload, InvalidPayloadReason);

        // Without an EventId, the source position identifies the message across redeliveries of the same offset.
        var sourceEventId = usageEvent.EventId?.ToString() ??
                            $"{result.Topic}:{result.Partition.Value}:{result.Offset.Value}";

        using var scope = _scopeFactory.CreateScope();
        var status = await scope.ServiceProvider.GetRequiredService<TenantManagementService>().RegisterUsageAsync(
            usageEvent.TenantId,
            usageEvent.EventsConsumed,
            usageEvent.AlertsTriggered,
            usageEvent.ApiRequests,
            usageEvent.ChannelUsage,
            sourceEventId,
            cancellationToken);

        _logger.LogInformation("Processed tenant usage event {SourceEventId} for tenant {TenantId}. Status: {Status}",
            sourceEventId, usageEvent.TenantId, status);
        return MessageHandlingResult.Processed;
    }

    /// <summary>Extracts the original usage payload from a replay scheduled by the DLQ consumer.</summary>
    public static string? ParseReplayPayload(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.TryGetProperty("originalPayload", out var original) &&
                   original.ValueKind == JsonValueKind.String
                ? original.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static TenantUsageEvent? ParseUsageEvent(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var eventPayload = JsonSerializer.Deserialize<TenantUsageEvent>(payload);
            if (eventPayload is null || eventPayload.TenantId == Guid.Empty)
            {
                return null;
            }

            return eventPayload with
            {
                EventsConsumed = Math.Max(0, eventPayload.EventsConsumed),
                AlertsTriggered = Math.Max(0, eventPayload.AlertsTriggered),
                ApiRequests = Math.Max(0, eventPayload.ApiRequests),
                ChannelUsage = Math.Max(0, eventPayload.ChannelUsage)
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record TenantUsageEvent(
    Guid TenantId,
    int EventsConsumed,
    int AlertsTriggered,
    int ApiRequests,
    int ChannelUsage,
    Guid? EventId = null);
