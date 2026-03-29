using RiskEvaluation.Application.Interfaces;
using Confluent.Kafka;
using MediatR;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RiskEvaluation.Domain.Contracts;
using Microsoft.Extensions.Logging;

namespace RiskEvaluation.Infrastructure.Messaging;

public class KafkaMessageConsumer : IMessageConsumer
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KafkaMessageConsumer> _logger;

    public KafkaMessageConsumer(
        string bootstrapServers,
        string groupId,
        IServiceScopeFactory scopeFactory,
        ILogger<KafkaMessageConsumer> logger)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting Kafka message consumer. Subscribing to topics: disaster-event-detected, climatic-event-detected, risk-catalog-published");
        _consumer.Subscribe(["disaster-event-detected", "climatic-event-detected", "risk-catalog-published"]);

        var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(5));

        if (consumeResult == null)
        {
            _logger.LogInformation("No messages received yet");
            return;
        }

        _logger.LogInformation(
            "Consumed message from topic: {Topic}, Key: {Key}, Partition: {Partition}, Offset: {Offset}",
            consumeResult.Topic, consumeResult.Message.Key, consumeResult.Partition, consumeResult.Offset);

        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        switch (consumeResult.Topic)
        {
            case "disaster-event-detected" or "climatic-event-detected":
            {
                var sensorEvent = JsonSerializer.Deserialize<SensorEventDetected>(consumeResult.Message.Value);

                if (sensorEvent != null)
                {
                    _logger.LogInformation("Processing sensor event for Lat: {Latitude}, Lon: {Longitude}",
                        sensorEvent.Latitude, sensorEvent.Longitude);
                    await mediator.Publish(sensorEvent, cancellationToken);
                    _logger.LogInformation(
                        "Sensor event processed successfully for Lat: {Latitude}, Lon: {Longitude}",
                        sensorEvent.Latitude, sensorEvent.Longitude);
                }

                break;
            }
            case "risk-catalog-published":
            {
                var riskCatalog =
                    JsonSerializer.Deserialize<RiskCatalogPublishedEvent>(consumeResult.Message.Value);

                if (riskCatalog != null)
                {
                    _logger.LogInformation("Processing risk catalog published event. Version: {Version}",
                        riskCatalog.Version);
                    await mediator.Publish(riskCatalog, cancellationToken);
                    _logger.LogInformation(
                        "Risk catalog published event processed successfully. Version: {Version}",
                        riskCatalog.Version);
                }

                break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing Kafka consumer");
        if (_consumer is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
    }
}