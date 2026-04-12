using System.Text.Json;
using Confluent.Kafka;
using Geospatial.Application.Interfaces.Messaging;
using Geospatial.Domain.Events;
using Geospatial.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Geospatial.Infrastructure.Messaging.Publishers;

public sealed class RegionIntersectedPublisher : IRegionIntersectedPublisher
{
    private readonly IProducer<Null, string> _producer;
    private readonly KafkaProducerOptions _options;
    private readonly ILogger<RegionIntersectedPublisher> _logger;

    public RegionIntersectedPublisher(
        IProducer<Null, string> producer,
        IOptions<KafkaProducerOptions> options,
        ILogger<RegionIntersectedPublisher> logger)
    {
        _producer = producer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(GeospatialOperationEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt is null)
        {
            throw new ArgumentNullException(nameof(evt));
        }

        var payload = JsonSerializer.Serialize(evt, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        _logger.LogInformation("Publishing RegionIntersected event to topic {Topic} for event {EventId}", _options.RegionIntersectedTopic, evt.CollectionId);

        var message = new Message<Null, string> { Value = payload };
        var deliveryResult = await _producer.ProduceAsync(_options.RegionIntersectedTopic, message, cancellationToken);

        _logger.LogInformation("Published RegionIntersected event to topic {Topic} at partition {Partition} offset {Offset}",
            deliveryResult.Topic, deliveryResult.Partition, deliveryResult.Offset);
    }
}
