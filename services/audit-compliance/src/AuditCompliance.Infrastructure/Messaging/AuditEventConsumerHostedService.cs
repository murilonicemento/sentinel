using System.Diagnostics;
using System.Text;
using AuditCompliance.Application.Common;
using AuditCompliance.Application.Ingestion;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Infrastructure.Observability;
using AuditCompliance.Infrastructure.Options;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditCompliance.Infrastructure.Messaging;

/// <summary>
/// Consome os tópicos de auditoria e de replay com entrega pelo menos uma vez:
/// <list type="bullet">
/// <item>o offset só é confirmado após persistência durável ou publicação confirmada no dead-letter;</item>
/// <item>mensagens inválidas vão para o dead-letter e nunca são descartadas silenciosamente;</item>
/// <item>falhas transitórias são retentadas com backoff exponencial e jitter; se persistirem, o consumo
/// volta ao mesmo offset e pausa, sem perder nem pular mensagens.</item>
/// </list>
/// </summary>
public sealed class AuditEventConsumerHostedService : BackgroundService
{
    private readonly KafkaDeadLetterPublisher _deadLetterPublisher;
    private readonly ILogger<AuditEventConsumerHostedService> _logger;
    private readonly IAuditMetrics _metrics;
    private readonly KafkaOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public AuditEventConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        KafkaDeadLetterPublisher deadLetterPublisher,
        IAuditMetrics metrics,
        IOptions<KafkaOptions> options,
        TimeProvider timeProvider,
        ILogger<AuditEventConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _deadLetterPublisher = deadLetterPublisher;
        _metrics = metrics;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consume() é bloqueante; executa fora do thread de inicialização do host.
        return Task.Factory.StartNew(() => ConsumeLoopAsync(stoppingToken), stoppingToken,
            TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string?, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            EnablePartitionEof = false
        }).Build();

        consumer.Subscribe([_options.AuditEventsTopic, _options.ReplayTopic]);
        _logger.LogInformation("Audit event consumer subscribed to {Topic} and {ReplayTopic}",
            _options.AuditEventsTopic, _options.ReplayTopic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string?, string> result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException exception)
                {
                    _logger.LogError(exception, "Error consuming audit events: {Reason}", exception.Error.Reason);
                    continue;
                }

                if (result?.Message is null) continue;

                if (await TryHandleAsync(result, stoppingToken))
                {
                    consumer.Commit(result);
                    continue;
                }

                // Não confirma: volta ao offset atual e aguarda antes de tentar novamente.
                consumer.Seek(result.TopicPartitionOffset);
                await Task.Delay(TimeSpan.FromSeconds(_options.StorageUnavailablePauseSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    /// <summary>
    /// Retorna true quando a mensagem foi tratada de forma durável (persistida, duplicada ou enviada ao
    /// dead-letter) e o offset pode ser confirmado.
    /// </summary>
    private async Task<bool> TryHandleAsync(ConsumeResult<string?, string> result, CancellationToken stoppingToken)
    {
        using var activity = StartActivity(result);

        for (var attempt = 1; attempt <= _options.MaxProcessingAttempts; attempt++)
            try
            {
                var outcome = await IngestAsync(result, stoppingToken);

                if (outcome.Status == IngestionStatusEnum.Rejected)
                    await DeadLetterAsync(result, outcome.RejectionCode!, outcome.RejectionReason!, stoppingToken);

                activity?.SetTag("audit.outcome", outcome.Status.ToString());
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException &&
                                              !TransientFailureClassifier.IsTransient(exception))
            {
                // Erro inesperado e não transitório: retentar indefinidamente bloquearia a partição.
                _logger.LogError(exception,
                    "Unexpected error processing audit message at {Topic}[{Partition}]@{Offset}; sending to dead-letter",
                    result.Topic, result.Partition.Value, result.Offset.Value);
                activity?.SetStatus(ActivityStatusCode.Error);

                return await TryDeadLetterUnexpectedAsync(result, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _metrics.ProcessingFailed("persistence");

                if (attempt == _options.MaxProcessingAttempts)
                {
                    _logger.LogError(exception,
                        "Audit message at {Topic}[{Partition}]@{Offset} could not be processed after {Attempts} attempts; consumption paused",
                        result.Topic, result.Partition.Value, result.Offset.Value, attempt);
                    activity?.SetStatus(ActivityStatusCode.Error);
                    return false;
                }

                var delay = ComputeBackoff(attempt);
                _logger.LogWarning(exception,
                    "Transient failure processing audit message at {Topic}[{Partition}]@{Offset}, attempt {Attempt}/{MaxAttempts}; retrying in {DelayMs} ms",
                    result.Topic, result.Partition.Value, result.Offset.Value, attempt, _options.MaxProcessingAttempts,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, stoppingToken);
            }

        return false;
    }

    private async Task<IngestionOutcome> IngestAsync(ConsumeResult<string?, string> result,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IngestAuditEventHandler>();

        var brokerTimestamp = result.Message.Timestamp.Type == TimestampType.NotAvailable
            ? (DateTimeOffset?)null
            : new DateTimeOffset(result.Message.Timestamp.UtcDateTime);

        return await handler.HandleAsync(
            new IncomingAuditMessage(result.Message.Value ?? string.Empty, result.Topic, result.Partition.Value,
                result.Offset.Value, brokerTimestamp),
            cancellationToken);
    }

    private async Task<bool> TryDeadLetterUnexpectedAsync(ConsumeResult<string?, string> result,
        CancellationToken cancellationToken)
    {
        try
        {
            await DeadLetterAsync(result, ApplicationErrorCodes.UnexpectedProcessingError,
                "The message could not be processed due to an unexpected error.", cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _metrics.ProcessingFailed("dead_letter");
            _logger.LogError(exception, "Failed to publish audit message to the dead-letter topic");
            return false;
        }
    }

    private async Task DeadLetterAsync(ConsumeResult<string?, string> result, string code, string reason,
        CancellationToken cancellationToken)
    {
        await _deadLetterPublisher.PublishAsync(new DeadLetterEnvelope
        {
            OriginalTopic = result.Topic,
            Partition = result.Partition.Value,
            Offset = result.Offset.Value,
            Key = result.Message.Key,
            Payload = result.Message.Value ?? string.Empty,
            ErrorCode = code,
            ErrorReason = reason,
            FailedAt = _timeProvider.GetUtcNow(),
            ConsumerGroup = _options.ConsumerGroupId
        }, result.Message.Headers, cancellationToken);

        _metrics.EventDeadLettered(code);
        _logger.LogWarning("Audit message at {Topic}[{Partition}]@{Offset} sent to dead-letter with code {ErrorCode}",
            result.Topic, result.Partition.Value, result.Offset.Value, code);
    }

    private TimeSpan ComputeBackoff(int attempt)
    {
        var baseDelay = _options.InitialRetryDelayMilliseconds * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.NextDouble() * _options.InitialRetryDelayMilliseconds;
        return TimeSpan.FromMilliseconds(baseDelay + jitter);
    }

    private static Activity? StartActivity(ConsumeResult<string?, string> result)
    {
        ActivityContext parent = default;
        var headers = result.Message.Headers;

        if (headers is not null && headers.TryGetLastBytes("traceparent", out var traceParent))
        {
            headers.TryGetLastBytes("tracestate", out var traceState);
            ActivityContext.TryParse(Encoding.UTF8.GetString(traceParent),
                traceState is null ? null : Encoding.UTF8.GetString(traceState), out parent);
        }

        var activity = AuditTelemetry.ActivitySource.StartActivity("audit.ingest", ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination.name", result.Topic);
        activity?.SetTag("messaging.kafka.message.offset", result.Offset.Value);
        return activity;
    }
}
