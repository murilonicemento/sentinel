using AlertOrchestrator.Application.Events;
using AlertOrchestrator.Application.Interfaces.Messaging;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AlertOrchestrator.Infrastructure.Messaging.Consumers;

public sealed class RiskUpdatedEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RiskUpdatedEventConsumer> _logger;
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
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                
                _logger.LogInformation("Received message from topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                    result.Topic, result.Partition, result.Offset);

                var @event = JsonSerializer.Deserialize<RiskUpdatedEvent>(result.Message.Value);
                
                if (@event is not null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await mediator.Publish(@event, stoppingToken);
                    
                    _consumer.Commit(result);
                    _logger.LogDebug("Processed and committed message: {MessageKey}", result.Message.Key);
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming message from topic: {Topic}", _topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing message");
            }
        }

        _consumer.Close();
    }
}

public sealed class RegionIntersectedEventConsumer : BackgroundService
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RegionIntersectedEventConsumer> _logger;
    private readonly string _topic;

    public RegionIntersectedEventConsumer(
        string bootstrapServers,
        string topic,
        string groupId,
        IServiceProvider serviceProvider,
        ILogger<RegionIntersectedEventConsumer> logger)
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
        _logger.LogInformation("RegionIntersectedEventConsumer started. Subscribed to topic: {Topic}", _topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);
                
                _logger.LogInformation("Received message from topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                    result.Topic, result.Partition, result.Offset);

                var @event = JsonSerializer.Deserialize<RegionIntersectedEvent>(result.Message.Value);
                
                if (@event is not null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await mediator.Publish(@event, stoppingToken);
                    
                    _consumer.Commit(result);
                    _logger.LogDebug("Processed and committed message: {MessageKey}", result.Message.Key);
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming message from topic: {Topic}", _topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing message");
            }
        }

        _consumer.Close();
    }
}
