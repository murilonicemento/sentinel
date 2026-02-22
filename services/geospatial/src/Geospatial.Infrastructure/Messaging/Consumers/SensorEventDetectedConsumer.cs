using System.Text.Json;
using Confluent.Kafka;
using Geospatial.Application.Interfaces.Repositories;
using Geospatial.Application.Models;
using Geospatial.Domain.Repositories;
using Geospatial.Infrastructure.Messaging.Events;
using Geospatial.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Geospatial.Infrastructure.Messaging.Consumers;

public sealed class SensorEventDetectedConsumer : BackgroundService
{
    private readonly IConsumer<Null, string> _consumer;
    private readonly IProducer<Null, string> _producer;
    private readonly IGeospatialEventRepository _repository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly KafkaConsumerOptions _options;
    private readonly ILogger<SensorEventDetectedConsumer> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SensorEventDetectedConsumer(
        IConsumer<Null, string> consumer,
        IProducer<Null, string> producer,
        IGeospatialEventRepository repository,
        IOutboxRepository outboxRepository,
        IOptions<KafkaConsumerOptions> options,
        ILogger<SensorEventDetectedConsumer> logger)
    {
        _consumer = consumer;
        _producer = producer;
        _repository = repository;
        _outboxRepository = outboxRepository;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_options.TopicName);
        _logger.LogInformation("Subscribed to topic {Topic} for {EventType}", _options.TopicName,
            nameof(SensorEventDetected));

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<Null, string>? result = null;
            try
            {
                result = _consumer.Consume(stoppingToken);

                if (result?.Message?.Value is null)
                    continue;

                var processed = await ProcessMessageAsync(result.Message.Value, stoppingToken);
                if (!processed)
                    await SendToDeadLetterAsync(result, stoppingToken);

                _consumer.Commit(result);
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume error");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Fatal error in consumer loop");
                if (result != null)
                    await SendToDeadLetterAsync(result, stoppingToken);
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _consumer.Close();
    }

    private async Task<bool> ProcessMessageAsync(string messageValue, CancellationToken cancellationToken)
    {
        try
        {
            var evt = JsonSerializer.Deserialize<SensorEventDetected>(messageValue, JsonOptions);
            if (evt == null) return false;

            // exemplo de interface que TEvent deve ter
            if (!IsValidCoordinates(evt.Latitude, evt.Longitude)) return false;

            var geospatialEvent = new GeospatialOperationEvent
            {
                CollectionId = evt.CollectionId,
                OperationType = _options.TopicName,
                Payload = evt,
                Timestamp = evt.CollectedAt,
                MainPoint = new GeoLocation { Lat = evt.Latitude, Lon = evt.Longitude }
            };

            await _repository.IndexAsync(geospatialEvent, cancellationToken);
            await _outboxRepository.UpdateProcessed(evt.CollectionId);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidCoordinates(double lat, double lon)
        => lat is >= -90 and <= 90 && lon is >= -180 and <= 180;

    private async Task SendToDeadLetterAsync(ConsumeResult<Null, string> result, CancellationToken cancellationToken)
    {
        var dlqMessage = new DeadLetterEnvelope
        {
            OriginalTopic = result.Topic,
            OriginalPartition = result.Partition.Value,
            OriginalOffset = result.Offset.Value,
            Payload = result.Message.Value,
            FailedAt = DateTime.UtcNow
        };

        var serialized = JsonSerializer.Serialize(dlqMessage);
        await _producer.ProduceAsync(_options.DeadLetterTopic, new Message<Null, string> { Value = serialized },
            cancellationToken);
    }
}