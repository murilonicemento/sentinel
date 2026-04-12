using System.Text.Json;
using AlertOrchestrator.Application.Events;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Consumers;

public sealed class RiskUpdatedEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<RiskUpdatedEventConsumer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly string _topic;

    public RiskUpdatedEventConsumer(
        string bootstrapServers,
        string topic,
        string groupId,
        IServiceProvider serviceProvider,
        ILogger<RiskUpdatedEventConsumer> logger)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _topic = topic;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_topic);
        _logger.LogInformation("RiskUpdatedEventConsumer started. Subscribed to topic: {Topic}", _topic);

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(5));

                if (consumeResult == null)
                {
                    _logger.LogInformation("No messages received yet");
                    return;
                }

                _logger.LogInformation("Received message from topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult.Topic, consumeResult.Partition, consumeResult.Offset);

                var @event = JsonSerializer.Deserialize<RiskUpdatedEvent>(consumeResult.Message.Value);

                if (@event is null) continue;

                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Publish(@event, stoppingToken);

                _consumer.Commit(consumeResult);
                _logger.LogDebug("Processed and committed message: {MessageKey}", consumeResult.Message.Key);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming message from topic: {Topic}", _topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing message");
            }

        _consumer.Close();
    }
}