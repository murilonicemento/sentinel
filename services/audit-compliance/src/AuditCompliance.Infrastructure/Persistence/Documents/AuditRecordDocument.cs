using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Enums;
using AuditCompliance.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AuditCompliance.Infrastructure.Persistence.Documents;

/// <summary>
/// Metadata, BeforeState e AfterState são armazenados como texto JSON para preservar exatamente o
/// conteúdo sobre o qual o hash de integridade foi calculado.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class AuditRecordDocument
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid? TenantId { get; set; }

    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public int EventVersion { get; set; }
    public string? Action { get; set; }
    public ActorDocument? Actor { get; set; }
    public string SourceService { get; set; } = string.Empty;

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime OccurredAt { get; set; }

    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RecordedAt { get; set; }

    public string? CorrelationId { get; set; }
    public string? CausationId { get; set; }
    public ResourceDocument? Resource { get; set; }
    public string? Metadata { get; set; }
    public string? BeforeState { get; set; }
    public string? AfterState { get; set; }
    public string? IntegrityHash { get; set; }
    public int SchemaVersion { get; set; }

    public static AuditRecordDocument FromDomain(AuditRecord record)
    {
        return new AuditRecordDocument
        {
            Id = record.Id,
            TenantId = record.TenantId,
            EventId = record.EventId,
            EventType = record.EventType,
            EventVersion = record.EventVersion,
            Action = record.Action,
            Actor = record.Actor is null
                ? null
                : new ActorDocument { Type = record.Actor.Type.ToString(), Id = record.Actor.Id },
            SourceService = record.SourceService,
            OccurredAt = record.OccurredAt.UtcDateTime,
            RecordedAt = record.RecordedAt.UtcDateTime,
            CorrelationId = record.CorrelationId,
            CausationId = record.CausationId,
            Resource = record.Resource is null
                ? null
                : new ResourceDocument { Type = record.Resource.Type, Id = record.Resource.Id },
            Metadata = record.Metadata?.Json,
            BeforeState = record.BeforeState?.Json,
            AfterState = record.AfterState?.Json,
            IntegrityHash = record.IntegrityHash,
            SchemaVersion = record.SchemaVersion
        };
    }

    public AuditRecord ToDomain()
    {
        return AuditRecord.Rehydrate(
            Id,
            TenantId,
            EventId,
            EventType,
            EventVersion,
            Action,
            Actor is null ? null : Domain.ValueObjects.Actor.Create(Enum.Parse<ActorTypeEnum>(Actor.Type), Actor.Id),
            SourceService,
            new DateTimeOffset(DateTime.SpecifyKind(OccurredAt, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(RecordedAt, DateTimeKind.Utc)),
            CorrelationId,
            CausationId,
            Resource is null ? null : ResourceReference.Create(Resource.Type, Resource.Id),
            Metadata is null ? null : AuditPayload.FromJson(Metadata),
            BeforeState is null ? null : AuditPayload.FromJson(BeforeState),
            AfterState is null ? null : AuditPayload.FromJson(AfterState),
            IntegrityHash,
            SchemaVersion);
    }
}

public sealed class ActorDocument
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public sealed class ResourceDocument
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}
