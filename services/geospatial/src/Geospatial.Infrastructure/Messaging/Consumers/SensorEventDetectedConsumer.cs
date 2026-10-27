using System.Text.Json;
using Confluent.Kafka;
using Geospatial.Domain.Events;
using Geospatial.Domain.Repositories;
using Geospatial.Infrastructure.Messaging.Events;
using Geospatial.Infrastructure.Messaging.Reliability;
using Geospatial.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Geospatial.Infrastructure.Messaging.Consumers;

/// <summary>
/// Indexes sensor events in Elasticsearch with at-least-once delivery: the offset is committed only after the event is
/// indexed or published to <c>geospatial-dead-letter</c>. Indexing by event id is idempotent, so redelivery is
/// harmless; Elasticsearch outages are retried instead of dead-lettering every message.
/// </summary>
public sealed class SensorEventDetectedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly IConsumer<Null, string> _consumer;
    private readonly ILogger<SensorEventDetectedConsumer> _logger;
    private readonly KafkaConsumerOptions _options;
    private readonly ReliableMessageProcessor _processor;
    private readonly ConsumerReliabilityOptions _reliability;
    private readonly IGeospatialEventRepository _repository;

    public SensorEventDetectedConsumer(
        IConsumer<Null, string> consumer,
        ReliableMessageProcessor processor,
        ConsumerReliabilityOptions reliability,
        IGeospatialEventRepository repository,
        IOptions<KafkaConsumerOptions> options,
        ILogger<SensorEventDetectedConsumer> logger)
    {
        _consumer = consumer;
        _processor = processor;
        _reliability = reliability;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        ReliableConsumerLoop.Start(_consumer, [_options.TopicName], _processor, HandleAsync, _reliability, _logger,
            stoppingToken);

    private async Task<MessageHandlingResult> HandleAsync(ConsumeResult<Null, string> result,
        CancellationToken cancellationToken)
    {
        SensorEventDetected? evt;
        try
        {
            evt = JsonSerializer.Deserialize<SensorEventDetected>(result.Message.Value, JsonOptions);
        }
        catch (JsonException)
        {
            evt = null;
        }

        if (evt is null || !IsValidCoordinates(evt.Latitude, evt.Longitude))
            return MessageHandlingResult.Reject(MessageErrorCodes.InvalidPayload,
                "The payload is not a valid SensorEventDetected.");

        await _repository.IndexAsync(new GeospatialOperationEvent
        {
            CollectionId = evt.CollectionId,
            SourceEventId = evt.EventId,
            OperationType = _options.TopicName,
            Payload = evt,
            Timestamp = evt.CollectedAt,
            MainPoint = new GeoLocation { Lat = evt.Latitude, Lon = evt.Longitude }
        }, cancellationToken);

        return MessageHandlingResult.Processed;
    }

    private static bool IsValidCoordinates(double lat, double lon)
        => lat is >= -90 and <= 90 && lon is >= -180 and <= 180;
}
