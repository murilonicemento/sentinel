using System.Text.RegularExpressions;
using AuditCompliance.Domain.Catalog;
using AuditCompliance.Domain.Exceptions;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.Domain.Aggregates;

/// <summary>
/// Registro imutável de uma ação ou evento auditável. Não expõe operações de alteração:
/// correções devem ser representadas por novos registros relacionados via CausationId.
/// </summary>
public sealed partial class AuditRecord
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxIdentifierLength = 128;

    // Tolerância para diferença de relógio entre o serviço de origem e a auditoria.
    public static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(5);

    private AuditRecord()
    {
        EventId = string.Empty;
        EventType = string.Empty;
        SourceService = string.Empty;
    }

    public Guid Id { get; private init; }
    public Guid? TenantId { get; private init; }
    public string EventId { get; private init; }
    public string EventType { get; private init; }
    public int EventVersion { get; private init; }
    public string? Action { get; private init; }
    public Actor? Actor { get; private init; }
    public string SourceService { get; private init; }
    public DateTimeOffset OccurredAt { get; private init; }
    public DateTimeOffset RecordedAt { get; private init; }
    public string? CorrelationId { get; private init; }
    public string? CausationId { get; private init; }
    public ResourceReference? Resource { get; private init; }
    public AuditPayload? Metadata { get; private init; }
    public AuditPayload? BeforeState { get; private init; }
    public AuditPayload? AfterState { get; private init; }
    public string? IntegrityHash { get; private set; }
    public int SchemaVersion { get; private init; }

    public static AuditRecord Create(
        string? eventId,
        string? eventType,
        int eventVersion,
        string? sourceService,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        Guid? tenantId = null,
        string? action = null,
        Actor? actor = null,
        string? correlationId = null,
        string? causationId = null,
        ResourceReference? resource = null,
        AuditPayload? metadata = null,
        AuditPayload? beforeState = null,
        AuditPayload? afterState = null)
    {
        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > MaxIdentifierLength)
            throw new AuditDomainException(AuditErrorCodes.InvalidEventId,
                $"Event id is required and must have at most {MaxIdentifierLength} characters.");

        if (!AuditEventCatalog.IsKnown(eventType))
            throw new AuditDomainException(AuditErrorCodes.UnknownEventType, "Event type is not part of the catalog.");

        if (!AuditEventCatalog.IsVersionSupported(eventType!, eventVersion))
            throw new AuditDomainException(AuditErrorCodes.UnsupportedEventVersion,
                $"Version {eventVersion} of event type {eventType} is not supported.");

        if (string.IsNullOrWhiteSpace(sourceService) || !SourceServicePattern().IsMatch(sourceService))
            throw new AuditDomainException(AuditErrorCodes.InvalidSourceService,
                "Source service is required and must be a kebab-case service name.");

        if (tenantId == Guid.Empty)
            throw new AuditDomainException(AuditErrorCodes.InvalidField, "Tenant id must not be empty when provided.");

        var normalizedOccurredAt = AuditTimestamp.Normalize(occurredAt);
        var normalizedRecordedAt = AuditTimestamp.Normalize(recordedAt);

        if (normalizedOccurredAt > normalizedRecordedAt.Add(AllowedClockSkew))
            throw new AuditDomainException(AuditErrorCodes.InvalidTimestamp, "Occurrence timestamp is in the future.");

        return new AuditRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventId = eventId.Trim(),
            EventType = eventType!,
            EventVersion = eventVersion,
            Action = ValidateOptionalIdentifier(action, nameof(Action)),
            Actor = actor,
            SourceService = sourceService,
            OccurredAt = normalizedOccurredAt,
            RecordedAt = normalizedRecordedAt,
            CorrelationId = ValidateOptionalIdentifier(correlationId, nameof(CorrelationId)),
            CausationId = ValidateOptionalIdentifier(causationId, nameof(CausationId)),
            Resource = resource,
            Metadata = metadata,
            BeforeState = beforeState,
            AfterState = afterState,
            SchemaVersion = CurrentSchemaVersion
        };
    }

    /// <summary>
    /// Reconstrói um registro já persistido, sem gerar novos identificadores ou timestamps.
    /// </summary>
    public static AuditRecord Rehydrate(
        Guid id,
        Guid? tenantId,
        string eventId,
        string eventType,
        int eventVersion,
        string? action,
        Actor? actor,
        string sourceService,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        string? correlationId,
        string? causationId,
        ResourceReference? resource,
        AuditPayload? metadata,
        AuditPayload? beforeState,
        AuditPayload? afterState,
        string? integrityHash,
        int schemaVersion)
    {
        return new AuditRecord
        {
            Id = id,
            TenantId = tenantId,
            EventId = eventId,
            EventType = eventType,
            EventVersion = eventVersion,
            Action = action,
            Actor = actor,
            SourceService = sourceService,
            OccurredAt = AuditTimestamp.Normalize(occurredAt),
            RecordedAt = AuditTimestamp.Normalize(recordedAt),
            CorrelationId = correlationId,
            CausationId = causationId,
            Resource = resource,
            Metadata = metadata,
            BeforeState = beforeState,
            AfterState = afterState,
            IntegrityHash = integrityHash,
            SchemaVersion = schemaVersion
        };
    }

    public void SealIntegrity(string integrityHash)
    {
        if (IntegrityHash is not null)
            throw new AuditDomainException(AuditErrorCodes.IntegrityAlreadySealed,
                "Integrity hash has already been assigned to this record.");

        if (string.IsNullOrWhiteSpace(integrityHash))
            throw new AuditDomainException(AuditErrorCodes.InvalidField, "Integrity hash must not be empty.");

        IntegrityHash = integrityHash;
    }

    private static string? ValidateOptionalIdentifier(string? value, string fieldName)
    {
        if (value is null) return null;

        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdentifierLength)
            throw new AuditDomainException(AuditErrorCodes.InvalidField,
                $"{fieldName} must not be blank and must have at most {MaxIdentifierLength} characters.");

        return value.Trim();
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{1,63}$")]
    private static partial Regex SourceServicePattern();
}
