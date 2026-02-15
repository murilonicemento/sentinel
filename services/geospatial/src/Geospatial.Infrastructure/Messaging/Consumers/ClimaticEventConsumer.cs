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

public sealed class ClimaticEventConsumer : BackgroundService
{
    private readonly IConsumer<Null, string> _consumer;
    private readonly IProducer<Null, string> _producer;
    private readonly IGeospatialEventRepository _repository;
    private readonly KafkaConsumerOptions _options;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<ClimaticEventConsumer> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public ClimaticEventConsumer(
        IConsumer<Null, string> consumer,
        IProducer<Null, string> producer,
        IGeospatialEventRepository repository,
        IOptions<KafkaConsumerOptions> options,
        IOutboxRepository outboxRepository,
        ILogger<ClimaticEventConsumer> logger)
    {
        _consumer = consumer;
        _producer = producer;
        _repository = repository;
        _options = options.Value;
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_options.TopicName);
        _logger.LogInformation("Subscribed to topic {Topic}", _options.TopicName);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Null, string>? result = null;

                try
                {
                    result = _consumer.Consume(stoppingToken);

                    if (result?.Message?.Value is null)
                    {
                        _logger.LogWarning(
                            "Null message received at offset {Offset}",
                            result?.Offset);
                        continue;
                    }

                    var processed = await ProcessMessageAsync(
                        result.Message.Value,
                        stoppingToken);

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
                    _logger.LogCritical(ex,
                        "Unexpected fatal error in consumer loop");

                    if (result != null)
                    {
                        await SendToDeadLetterAsync(result, stoppingToken);
                        _consumer.Commit(result);
                    }
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
        finally
        {
            _consumer.Close();
            _logger.LogInformation("Kafka consumer closed");
        }
    }

    private async Task<bool> ProcessMessageAsync(
        string messageValue,
        CancellationToken cancellationToken)
    {
        try
        {
            var climaticEvent = JsonSerializer.Deserialize<
                ClimaticEventDetectedEvent>(
                messageValue,
                JsonOptions);

            if (climaticEvent is null)
            {
                _logger.LogWarning("Deserialization returned null. Message: {Message}", messageValue);
                return false;
            }

            var eventIsPending = await _outboxRepository.ExistsPending(climaticEvent.EventId);

            if (!eventIsPending)
            {
                _logger.LogInformation("Event already processed. Skipping. EventId: {EventId}", climaticEvent.EventId);
                return true;
            }

            if (!IsValidCoordinates(climaticEvent.Latitude, climaticEvent.Longitude))
            {
                _logger.LogWarning("Invalid coordinates for EventId {EventId}", climaticEvent.EventId);
                return false;
            }

            var geospatialEvent = new GeospatialOperationEvent
            {
                EventId = climaticEvent.EventId,
                OperationType = _options.TopicName,
                Payload = climaticEvent,
                Timestamp = climaticEvent.CollectedAt,
                MainPoint = new GeoLocation
                {
                    Lat = climaticEvent.Latitude,
                    Lon = climaticEvent.Longitude
                }
            };

            await _repository.IndexAsync(geospatialEvent, cancellationToken);
            await _outboxRepository.UpdateProcessed(climaticEvent.EventId);

            _logger.LogInformation("Event indexed successfully. EventId: {EventId}", climaticEvent.EventId);
            _logger.LogInformation("Event processed successfully. EventId: {EventId}", climaticEvent.EventId);

            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON payload. Message: {Message}", messageValue);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process event");

            return false;
        }
    }

    private async Task SendToDeadLetterAsync(ConsumeResult<Null, string> result, CancellationToken cancellationToken)
    {
        try
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

            await _producer.ProduceAsync(
                _options.DeadLetterTopic,
                new Message<Null, string> { Value = serialized },
                cancellationToken);

            _logger.LogWarning("Message sent to DLQ. Offset: {Offset}", result.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to publish message to DLQ. Offset: {Offset}", result.Offset);
        }
    }

    private static bool IsValidCoordinates(double lat, double lon)
        => lat is >= -90 and <= 90
           && lon is >= -180 and <= 180;

    public override void Dispose()
    {
        _consumer.Dispose();
        _producer.Dispose();
        base.Dispose();
    }
}