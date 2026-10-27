using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Contracts;
using RiskEvaluation.Infrastructure.Messaging;
using RiskEvaluation.Infrastructure.Messaging.Reliability;

namespace RiskEvaluation.Infrastructure.HostedServices;

public sealed record RiskEvaluationConsumerTopics(
    string DisasterEvents = "disaster-event-detected",
    string ClimaticEvents = "climatic-event-detected",
    string CatalogPublished = "risk-catalog-published");

/// <summary>
/// Consumes sensor events and catalog publications with at-least-once delivery: the offset is committed only after
/// the event was evaluated (or was already evaluated) or the message was published to the dead-letter topic. Sensor
/// events are claimed by <c>EventId</c> or, for producers that do not send one yet, by topic/partition/offset.
/// </summary>
public sealed class RiskEvaluationEventConsumer : BackgroundService
{
    private readonly IConsumer<string?, string> _consumer;
    private readonly IConsumedMessageGuard _guard;
    private readonly ILogger<RiskEvaluationEventConsumer> _logger;
    private readonly ConsumerReliabilityOptions _options;
    private readonly ReliableMessageProcessor _processor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RiskEvaluationConsumerTopics _topics;

    public RiskEvaluationEventConsumer(IConsumer<string?, string> consumer, RiskEvaluationConsumerTopics topics,
        IConsumedMessageGuard guard, ReliableMessageProcessor processor, ConsumerReliabilityOptions options,
        IServiceScopeFactory scopeFactory, ILogger<RiskEvaluationEventConsumer> logger)
    {
        _consumer = consumer;
        _topics = topics;
        _guard = guard;
        _processor = processor;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ReliableConsumerLoop.Start(_consumer,
            [_topics.DisasterEvents, _topics.ClimaticEvents, _topics.CatalogPublished], _processor, HandleAsync,
            _options, _logger, stoppingToken);

    private Task<MessageHandlingResult> HandleAsync(ConsumeResult<string?, string> result,
        CancellationToken cancellationToken) =>
        result.Topic == _topics.CatalogPublished
            ? HandleCatalogPublishedAsync(result, cancellationToken)
            : HandleSensorEventAsync(result, cancellationToken);

    private async Task<MessageHandlingResult> HandleSensorEventAsync(ConsumeResult<string?, string> result,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<SensorEventDetected>(result.Message.Value, out var sensorEvent) ||
            string.IsNullOrWhiteSpace(sensorEvent.EventType) ||
            sensorEvent.Latitude is < -90 or > 90 || sensorEvent.Longitude is < -180 or > 180)
            return MessageHandlingResult.Reject(MessageErrorCodes.InvalidPayload,
                "The payload is not a valid SensorEventDetected.");

        var messageKey = sensorEvent.EventId?.ToString() ??
                         $"{result.Topic}:{result.Partition.Value}:{result.Offset.Value}";

        switch (await _guard.TryBeginAsync(messageKey))
        {
            case ConsumedMessageClaim.AlreadyProcessed:
                _logger.LogInformation("Sensor event {MessageKey} already evaluated, skipping", messageKey);
                return MessageHandlingResult.Processed;
            case ConsumedMessageClaim.InProgress:
                throw new MessageInProgressException(messageKey);
        }

        try
        {
            await PublishAsync(sensorEvent, cancellationToken);
            await _guard.CompleteAsync(messageKey);
            return MessageHandlingResult.Processed;
        }
        catch
        {
            await _guard.AbandonAsync(messageKey);
            throw;
        }
    }

    // Saving a published catalog version is an upsert by version, so redelivery is harmless.
    private async Task<MessageHandlingResult> HandleCatalogPublishedAsync(ConsumeResult<string?, string> result,
        CancellationToken cancellationToken)
    {
        if (!TryDeserialize<RiskCatalogPublishedEvent>(result.Message.Value, out var catalog) ||
            string.IsNullOrWhiteSpace(catalog.Version))
            return MessageHandlingResult.Reject(MessageErrorCodes.InvalidPayload,
                "The payload is not a valid RiskCatalogPublishedEvent.");

        await PublishAsync(catalog, cancellationToken);
        return MessageHandlingResult.Processed;
    }

    private async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken)
        where TNotification : INotification
    {
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(notification, cancellationToken);
    }

    private static bool TryDeserialize<T>(string? payload, out T value) where T : class
    {
        value = null!;
        if (string.IsNullOrWhiteSpace(payload)) return false;
        try
        {
            value = JsonSerializer.Deserialize<T>(payload)!;
            return value is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
