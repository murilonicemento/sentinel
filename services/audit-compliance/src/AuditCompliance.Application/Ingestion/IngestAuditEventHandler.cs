using System.Text.Json;
using AuditCompliance.Application.Common;
using AuditCompliance.Application.Contracts;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.Exceptions;
using AuditCompliance.Domain.Repositories;
using AuditCompliance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AuditCompliance.Application.Ingestion;

/// <summary>
/// Valida o envelope, aplica a minimização de dados e persiste o registro de forma idempotente.
/// Falhas de validação resultam em <see cref="IngestionStatusEnum.Rejected"/>; falhas de infraestrutura
/// são propagadas para que o consumidor aplique a política de retentativa sem confirmar a mensagem.
/// </summary>
public sealed class IngestAuditEventHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<IngestAuditEventHandler> _logger;
    private readonly IIntegrityHasher _integrityHasher;
    private readonly IAuditMetrics _metrics;
    private readonly IAuditRecordRepository _repository;
    private readonly TimeProvider _timeProvider;

    public IngestAuditEventHandler(
        IAuditRecordRepository repository,
        IIntegrityHasher integrityHasher,
        IAuditMetrics metrics,
        TimeProvider timeProvider,
        ILogger<IngestAuditEventHandler> logger)
    {
        _repository = repository;
        _integrityHasher = integrityHasher;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IngestionOutcome> HandleAsync(IncomingAuditMessage message, CancellationToken cancellationToken)
    {
        var recordedAt = _timeProvider.GetUtcNow();
        _metrics.EventReceived(message.Topic, message.BrokerTimestamp is null ? null : recordedAt - message.BrokerTimestamp);

        AuditRecord record;
        try
        {
            var envelope = Deserialize(message.Payload);
            record = BuildRecord(envelope, recordedAt);
        }
        catch (AuditDomainException exception)
        {
            return Reject(message, exception.Code, exception.Message);
        }

        record.SealIntegrity(_integrityHasher.Compute(record));

        var result = await _repository.AppendAsync(record, cancellationToken);

        if (result == AppendResultEnum.Duplicate)
        {
            _metrics.EventDuplicated(record.EventType);
            _logger.LogInformation(
                "Duplicate audit event ignored. SourceService: {SourceService}, EventId: {EventId}, EventType: {EventType}",
                record.SourceService, record.EventId, record.EventType);
            return new IngestionOutcome(IngestionStatusEnum.Duplicate);
        }

        _metrics.EventPersisted(record.EventType, record.RecordedAt - record.OccurredAt);
        _logger.LogDebug("Audit record {RecordId} persisted for event {EventType} from {SourceService}",
            record.Id, record.EventType, record.SourceService);

        return new IngestionOutcome(IngestionStatusEnum.Persisted, record.Id);
    }

    private IngestionOutcome Reject(IncomingAuditMessage message, string code, string reason)
    {
        _metrics.EventRejected(code);
        _logger.LogWarning(
            "Audit event rejected with code {RejectionCode}: {RejectionReason}. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
            code, reason, message.Topic, message.Partition, message.Offset);

        return IngestionOutcome.Rejected(code, reason);
    }

    private static AuditEventEnvelope Deserialize(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<AuditEventEnvelope>(payload, SerializerOptions)
                   ?? throw new AuditDomainException(ApplicationErrorCodes.MalformedMessage, "Message body is empty.");
        }
        catch (JsonException)
        {
            throw new AuditDomainException(ApplicationErrorCodes.MalformedMessage,
                "Message body is not a valid audit event envelope.");
        }
    }

    private AuditRecord BuildRecord(AuditEventEnvelope envelope, DateTimeOffset recordedAt)
    {
        if (envelope.EventVersion is null)
            throw new AuditDomainException(ApplicationErrorCodes.MissingRequiredField, "eventVersion is required.");

        var redactedFields = 0;

        var record = AuditRecord.Create(
            envelope.EventId,
            envelope.EventType,
            envelope.EventVersion.Value,
            envelope.SourceService,
            ParseOccurredAt(envelope.OccurredAt),
            recordedAt,
            ParseTenantId(envelope.TenantId),
            envelope.Action,
            ParseActor(envelope.Actor),
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.Resource is null
                ? null
                : ResourceReference.Create(envelope.Resource.Type, envelope.Resource.Id),
            ToPayload(envelope.Metadata, "metadata", ref redactedFields),
            ToPayload(envelope.BeforeState, "beforeState", ref redactedFields),
            ToPayload(envelope.AfterState, "afterState", ref redactedFields));

        if (redactedFields > 0) _metrics.FieldsRedacted(redactedFields);

        return record;
    }

    private static DateTimeOffset ParseOccurredAt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new AuditDomainException(ApplicationErrorCodes.MissingRequiredField, "occurredAt is required.");

        if (!IsoTimestamp.TryParse(value, out var parsed))
            throw new AuditDomainException(AuditErrorCodes.InvalidTimestamp,
                "occurredAt must be an ISO 8601 timestamp with an explicit offset (e.g. 2026-01-01T12:00:00Z).");

        return parsed;
    }

    private static Guid? ParseTenantId(string? value)
    {
        if (value is null) return null;

        if (!Guid.TryParse(value, out var tenantId) || tenantId == Guid.Empty)
            throw new AuditDomainException(AuditErrorCodes.InvalidField, "tenantId must be a valid non-empty UUID.");

        return tenantId;
    }

    private static Actor? ParseActor(ActorContract? actor)
    {
        if (actor is null) return null;

        if (string.IsNullOrWhiteSpace(actor.Type) ||
            !Enum.TryParse<ActorTypeEnum>(actor.Type, true, out var type) ||
            !Enum.GetNames<ActorTypeEnum>().Contains(actor.Type, StringComparer.OrdinalIgnoreCase))
            throw new AuditDomainException(AuditErrorCodes.InvalidActor, "actor.type must be User, Service or System.");

        return Actor.Create(type, actor.Id);
    }

    private static AuditPayload? ToPayload(JsonElement? element, string fieldName, ref int redactedFields)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null) return null;

        var sanitized = AuditDataSanitizer.Sanitize(element.Value, fieldName);
        redactedFields += sanitized.RedactedFields;

        return AuditPayload.FromJson(sanitized.Json);
    }
}
