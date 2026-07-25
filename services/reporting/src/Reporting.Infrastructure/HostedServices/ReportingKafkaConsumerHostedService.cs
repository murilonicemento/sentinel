using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reporting.Application.Interfaces;
using Reporting.Domain.Entities;
using Reporting.Domain.Interfaces;

namespace Reporting.Infrastructure.HostedServices;

public sealed class ReportingKafkaConsumerHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReportingKafkaConsumerHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReportingKafkaConsumerHostedService(
        IConfiguration configuration,
        ILogger<ReportingKafkaConsumerHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"];
        if (string.IsNullOrWhiteSpace(bootstrapServers))
        {
            _logger.LogWarning("Kafka bootstrap servers are not configured. Reporting consumer will not start.");
            return;
        }

        var topic = _configuration["Kafka:Topic"] ?? "reporting-events";
        var groupId = _configuration["Kafka:ConsumerGroupId"] ?? "reporting-service-group";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true,
            EnablePartitionEof = true
        };

        var deadLetterTopic = _configuration["Kafka:DeadLetterTopic"] ?? "reporting-events-dead-letter";
        var maxAttempts = int.TryParse(_configuration["Kafka:MaxProcessingAttempts"], out var attempts) && attempts > 0 ? attempts : 3;
        var retryDelaySeconds = int.TryParse(_configuration["Kafka:ProcessingRetryDelaySeconds"], out var delaySeconds) && delaySeconds > 0 ? delaySeconds : 2;
        var maxRetryDelaySeconds = int.TryParse(_configuration["Kafka:ProcessingRetryMaxDelaySeconds"], out var maxDelaySeconds) && maxDelaySeconds > 0 ? maxDelaySeconds : 30;
        var backoffFactor = double.TryParse(_configuration["Kafka:ProcessingRetryBackoffFactor"], out var factor) && factor > 1 ? factor : 2.0;

        using var consumer = new ConsumerBuilder<Ignore, string>(config)
            .SetErrorHandler((_, error) => _logger.LogError("Kafka consumer error: {Reason}", error.Reason))
            .Build();

        consumer.Subscribe(topic);
        _logger.LogInformation("Reporting Kafka consumer subscribed to topic {Topic} with DLQ {DeadLetterTopic}", topic, deadLetterTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(stoppingToken);
                    if (consumeResult?.Message?.Value is null)
                    {
                        continue;
                    }

                    var envelopes = ParseEnvelopes(consumeResult.Message.Value);
                    if (envelopes.Count == 0)
                    {
                        _logger.LogWarning("Received invalid reporting envelope payload on topic {Topic}. Sending to dead-letter queue.", topic);
                        await PublishDeadLetterAsync(consumeResult.Message.Value, deadLetterTopic, topic, "Invalid payload structure", stoppingToken);
                        consumer.Commit(consumeResult);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var eventConsumer = scope.ServiceProvider.GetRequiredService<IReportingEventConsumer>();
                    var auditRepository = scope.ServiceProvider.GetRequiredService<IReportingAuditRepository>();

                    if (await TryProcessEnvelopesAsync(
                        eventConsumer,
                        auditRepository,
                        envelopes,
                        topic,
                        consumeResult.Partition.Value,
                        consumeResult.Offset.Value,
                        maxAttempts,
                        retryDelaySeconds,
                        maxRetryDelaySeconds,
                        backoffFactor,
                        stoppingToken))
                    {
                        consumer.Commit(consumeResult);
                    }
                    else
                    {
                        await PublishDeadLetterAsync(consumeResult.Message.Value, deadLetterTopic, topic, "Processing failed after retries", stoppingToken);
                        consumer.Commit(consumeResult);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Error consuming reporting event from Kafka");
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }

    public static List<ReportingEventEnvelope> ParseEnvelopes(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return new List<ReportingEventEnvelope>();
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.EnumerateArray()
                    .Select(element => JsonSerializer.Deserialize<ReportingEventEnvelope>(element.GetRawText(), CreateJsonOptions()))
                    .Where(envelope => envelope is not null)
                    .Cast<ReportingEventEnvelope>()
                    .ToList();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (TryGetProperty(root, "events", out var eventsElement))
                {
                    if (eventsElement.ValueKind == JsonValueKind.Array)
                    {
                        return eventsElement.EnumerateArray()
                            .Select(element => JsonSerializer.Deserialize<ReportingEventEnvelope>(element.GetRawText(), CreateJsonOptions()))
                            .Where(envelope => envelope is not null)
                            .Cast<ReportingEventEnvelope>()
                            .ToList();
                    }
                }

                var singleEnvelope = JsonSerializer.Deserialize<ReportingEventEnvelope>(root.GetRawText(), CreateJsonOptions());
                return singleEnvelope is not null
                    ? new List<ReportingEventEnvelope> { singleEnvelope }
                    : new List<ReportingEventEnvelope>();
            }
        }
        catch (JsonException)
        {
            // Invalid payload will be handled by the caller.
        }

        return new List<ReportingEventEnvelope>();
    }

    private async Task<bool> TryProcessEnvelopesAsync(
        IReportingEventConsumer eventConsumer,
        IReportingAuditRepository auditRepository,
        IReadOnlyCollection<ReportingEventEnvelope> envelopes,
        string sourceTopic,
        int partition,
        long offset,
        int maxAttempts,
        int retryDelaySeconds,
        int maxRetryDelaySeconds,
        double backoffFactor,
        CancellationToken cancellationToken)
    {
        var allSucceeded = true;

        foreach (var envelope in envelopes)
        {
            if (!await TryProcessEnvelopeWithRetriesAsync(
                eventConsumer,
                auditRepository,
                envelope,
                sourceTopic,
                partition,
                offset,
                maxAttempts,
                retryDelaySeconds,
                maxRetryDelaySeconds,
                backoffFactor,
                cancellationToken))
            {
                allSucceeded = false;
            }
        }

        return allSucceeded;
    }

    private async Task<bool> TryProcessEnvelopeWithRetriesAsync(
        IReportingEventConsumer eventConsumer,
        IReportingAuditRepository auditRepository,
        ReportingEventEnvelope envelope,
        string sourceTopic,
        int partition,
        long offset,
        int maxAttempts,
        int retryDelaySeconds,
        int maxRetryDelaySeconds,
        double backoffFactor,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var retryDelay = TimeSpan.FromSeconds(retryDelaySeconds);
        Exception? lastFailure = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            attempt++;
            try
            {
                await eventConsumer.ProcessAsync(envelope, cancellationToken);
                _logger.LogInformation("Successfully processed reporting event {EventId} on attempt {Attempt}", envelope.EventId, attempt);

                await SaveAuditAsync(auditRepository, envelope.EventId, sourceTopic, partition, offset, "Success", attempt, null, cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                lastFailure = exception;
                _logger.LogWarning(exception, "Attempt {Attempt}/{MaxAttempts} failed for reporting event {EventId}", attempt, maxAttempts, envelope.EventId);

                if (attempt >= maxAttempts)
                {
                    _logger.LogError(exception, "Exceeded max processing attempts for reporting event {EventId}", envelope.EventId);
                    await SaveAuditAsync(auditRepository, envelope.EventId, sourceTopic, partition, offset, "Failed", attempt, exception.Message, cancellationToken);
                    return false;
                }

                await Task.Delay(retryDelay, cancellationToken);
                retryDelay = TimeSpan.FromSeconds(Math.Min(maxRetryDelaySeconds, retryDelay.TotalSeconds * backoffFactor));
            }
        }

        if (lastFailure is not null)
        {
            await SaveAuditAsync(auditRepository, envelope.EventId, sourceTopic, partition, offset, "Failed", attempt, lastFailure.Message, cancellationToken);
        }

        return false;
    }

    private static Task SaveAuditAsync(
        IReportingAuditRepository auditRepository,
        string eventId,
        string sourceTopic,
        int partition,
        long offset,
        string status,
        int attemptCount,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var audit = new ReportingEventProcessingAudit(
            eventId,
            sourceTopic,
            partition,
            offset,
            status,
            attemptCount,
            errorMessage,
            DateTime.UtcNow);

        return auditRepository.SaveAsync(audit, cancellationToken);
    }

    private async Task PublishDeadLetterAsync(string payload, string deadLetterTopic, string sourceTopic, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _configuration["Kafka:BootstrapServers"],
                EnableIdempotence = true,
                Acks = Acks.All,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 1000
            };

            using var producer = new ProducerBuilder<Null, string>(producerConfig).Build();
            var headers = new Headers();
            headers.Add("source-topic", System.Text.Encoding.UTF8.GetBytes(sourceTopic));
            headers.Add("dead-letter-reason", System.Text.Encoding.UTF8.GetBytes(reason));
            headers.Add("dead-lettered-at", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("o")));

            var message = new Message<Null, string>
            {
                Value = payload,
                Headers = headers
            };

            var deliveryResult = await producer.ProduceAsync(deadLetterTopic, message, cancellationToken);
            _logger.LogWarning(
                "Published dead letter for source topic {SourceTopic} to {DeadLetterTopic} at partition {Partition} offset {Offset}",
                sourceTopic,
                deadLetterTopic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to publish dead letter payload to topic {DeadLetterTopic}", deadLetterTopic);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        foreach (var child in element.EnumerateObject())
        {
            if (string.Equals(child.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = child.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}
