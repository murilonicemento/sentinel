using System.Text.Json;
using AlertOrchestrator.Application.Interfaces.Messaging;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace AlertOrchestrator.Infrastructure.Messaging.Publishers;

public sealed class KafkaCommandPublisher : ICommandPublisher
{
    private readonly ILogger<KafkaCommandPublisher> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public KafkaCommandPublisher(string bootstrapServers, string topic, ILogger<KafkaCommandPublisher> logger)
    {
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        _producer = new ProducerBuilder<string, string>(config).Build();
        _topic = topic;
        _logger = logger;
        _logger.LogInformation(
            "KafkaCommandPublisher initialized. BootstrapServers: {BootstrapServers}, Topic: {Topic}", bootstrapServers,
            topic);
    }

    public async Task PublishAsync<T>(T command, CancellationToken cancellationToken = default) where T : class
    {
        var commandType = typeof(T).Name;
        _logger.LogInformation("Publishing command of type: {CommandType} to topic: {Topic}", commandType, _topic);

        var message = JsonSerializer.Serialize(command);
        var kafkaMessage = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = message
        };

        try
        {
            var result = await _producer.ProduceAsync(_topic, kafkaMessage, cancellationToken);
            _logger.LogInformation(
                "Command published successfully. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                result.Topic, result.Partition, result.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish command to topic: {Topic}", _topic);
            throw;
        }
    }
}