using RiskEvaluation.Application.Interfaces;
using Confluent.Kafka;
using MediatR;
using System.Text.Json;
using RiskEvaluation.Domain.Contracts;

namespace RiskEvaluation.Infrastructure.Messaging;

public class KafkaMessageConsumer : IMessageConsumer
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IMediator _mediator;

    public KafkaMessageConsumer(string bootstrapServers, string groupId, IMediator mediator)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _mediator = mediator;
    }

    public async Task StartConsumingAsync(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(new[] { "weather-data-updated", "risk-catalog-updated" });

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(cancellationToken);

                if (consumeResult.Message.Key == "weather-data-updated")
                {
                    var weatherData = JsonSerializer.Deserialize<WeatherDataUpdated>(consumeResult.Message.Value);
                    if (weatherData != null)
                    {
                        await _mediator.Publish(weatherData, cancellationToken);
                    }
                }
                else if (consumeResult.Message.Key == "risk-catalog-updated")
                {
                    var riskCatalog = JsonSerializer.Deserialize<RiskCatalogUpdated>(consumeResult.Message.Value);
                    if (riskCatalog != null)
                    {
                        await _mediator.Publish(riskCatalog, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error consuming message: {ex.Message}");
            }
        }
    }
}