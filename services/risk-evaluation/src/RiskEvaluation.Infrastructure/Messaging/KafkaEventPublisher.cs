using RiskEvaluation.Application.Interfaces;
using Confluent.Kafka;
using System.Text.Json;

namespace RiskEvaluation.Infrastructure.Messaging;

public class KafkaEventPublisher : IEventPublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaEventPublisher(string bootstrapServers, string topic)
    {
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
        _topic = topic;
    }

    public async Task PublishAsync<T>(T @event) where T : class
    {
        var message = JsonSerializer.Serialize(@event);
        var kafkaMessage = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = message
        };

        await _producer.ProduceAsync(_topic, kafkaMessage);
    }
}