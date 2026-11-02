using System.Text.Json;
using AlertOrchestrator.Infrastructure.Messaging.Reliability;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes one topic of <typeparamref name="TEvent"/> with at-least-once delivery: offsets are committed only after
/// the handler completes or the message is dead-lettered. Handlers are idempotent by event id.
/// </summary>
public abstract class KafkaEventConsumer<TEvent> : BackgroundService where TEvent : class, INotification
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger _logger;
    private readonly ConsumerReliabilityOptions _options;
    private readonly ReliableMessageProcessor _processor;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _topic;

    protected KafkaEventConsumer(IConsumer<string, string> consumer, string topic, IServiceProvider serviceProvider,
        ReliableMessageProcessor processor, ConsumerReliabilityOptions options, ILogger logger)
    {
        _consumer = consumer;
        _topic = topic;
        _serviceProvider = serviceProvider;
        _processor = processor;
        _options = options;
        _logger = logger;
    }

    /// <summary>False when the payload deserialized but lacks the fields the handler depends on.</summary>
    protected abstract bool IsValid(TEvent @event);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ReliableConsumerLoop.Start(_consumer, [_topic], _processor, HandleAsync, _options, _logger, stoppingToken);

    private async Task<MessageHandlingResult> HandleAsync(ConsumeResult<string, string> result,
        CancellationToken cancellationToken)
    {
        TEvent? @event;
        try
        {
            @event = JsonSerializer.Deserialize<TEvent>(result.Message.Value);
        }
        catch (JsonException)
        {
            return MessageHandlingResult.Reject(MessageErrorCodes.InvalidPayload,
                $"The payload is not a valid {typeof(TEvent).Name} JSON document.");
        }

        if (@event is null || !IsValid(@event))
            return MessageHandlingResult.Reject(MessageErrorCodes.InvalidPayload,
                $"The payload is missing required {typeof(TEvent).Name} fields.");

        using var scope = _serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(@event, cancellationToken);
        return MessageHandlingResult.Processed;
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
