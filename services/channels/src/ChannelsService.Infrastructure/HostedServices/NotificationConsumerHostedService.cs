using System.Text.Json;
using System.Text.Json.Serialization;
using ChannelsService.Application.Interfaces;
using ChannelsService.Domain.Events;
using ChannelsService.Infrastructure.Messaging.Reliability;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ChannelsService.Infrastructure.HostedServices;

/// <summary>
/// Consumes <c>notification-events</c> with at-least-once delivery. The offset is committed only after the
/// notification was delivered, recorded as a delivery failure on <c>notification-events-dlq</c>, or (when it cannot be
/// processed at all) published to the consumer dead-letter topic. Redeliveries skip channels already delivered.
/// </summary>
public sealed class NotificationConsumerHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Func<IConsumer<Ignore, string>>? _consumerFactory;
    private readonly ILogger<NotificationConsumerHostedService> _logger;
    private readonly ConsumerReliabilityOptions _options;
    private readonly Func<ReliableMessageProcessor>? _processorFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _topic;

    public NotificationConsumerHostedService(
        IConfiguration configuration,
        ILogger<NotificationConsumerHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = new ConsumerReliabilityOptions();
        _topic = configuration["Kafka:Topic"] ?? "notification-events";

        var bootstrapServers = configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers)) return;

        var groupId = configuration["Kafka:ConsumerGroupId"] ?? "channels-service-group";
        var deadLetterTopic = configuration["Kafka:ConsumerDeadLetterTopic"] ?? "channels-dead-letter";

        _consumerFactory = () => new ConsumerBuilder<Ignore, string>(
                ReliableConsumerLoop.CreateConfig(bootstrapServers, groupId))
            .SetErrorHandler((_, error) => _logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();
        _processorFactory = () => new ReliableMessageProcessor(new KafkaDeadLetterSink(bootstrapServers, deadLetterTopic),
            groupId, _options, logger);
    }

    /// <summary>Used by tests to drive the loop with a fake consumer and dead-letter sink.</summary>
    public NotificationConsumerHostedService(IConsumer<Ignore, string> consumer, string topic,
        ReliableMessageProcessor processor, ConsumerReliabilityOptions options, IServiceScopeFactory scopeFactory,
        ILogger<NotificationConsumerHostedService> logger)
    {
        _consumerFactory = () => consumer;
        _processorFactory = () => processor;
        _topic = topic;
        _options = options;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_consumerFactory is null || _processorFactory is null)
        {
            _logger.LogWarning(
                "Kafka bootstrap servers are not configured. NotificationConsumerHostedService will not start.");
            return Task.CompletedTask;
        }

        return ReliableConsumerLoop.Start(_consumerFactory(), [_topic], _processorFactory(), HandleAsync, _options,
            _logger, stoppingToken);
    }

    private async Task<MessageHandlingResult> HandleAsync(ConsumeResult<Ignore, string> result,
        CancellationToken cancellationToken)
    {
        NotificationEvent? notification;
        try
        {
            notification = JsonSerializer.Deserialize<NotificationEvent>(result.Message.Value, JsonOptions);
        }
        catch (JsonException)
        {
            return MessageHandlingResult.Reject(MessageErrorCodes.InvalidPayload,
                "The payload is not a valid NotificationEvent JSON document.");
        }

        if (notification is null || string.IsNullOrWhiteSpace(notification.EventId) ||
            string.IsNullOrWhiteSpace(notification.TenantId))
            return MessageHandlingResult.Reject(MessageErrorCodes.InvalidPayload,
                "The notification is missing its EventId or TenantId.");

        _logger.LogInformation(
            "Received notification event {EventId} type {EventType} correlation {CorrelationId} from Kafka.",
            notification.EventId, notification.EventType, notification.CorrelationId);

        using var scope = _scopeFactory.CreateScope();
        // Delivery failures are published to notification-events-dlq by the dispatch service and count as handled.
        await scope.ServiceProvider.GetRequiredService<INotificationDispatchService>()
            .DispatchAsync(notification, cancellationToken);
        return MessageHandlingResult.Processed;
    }
}
