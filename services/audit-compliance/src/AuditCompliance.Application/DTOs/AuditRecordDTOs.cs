using System.Text.Json;
using AuditCompliance.Domain.Aggregates;

namespace AuditCompliance.Application.DTOs;

public sealed record ActorDTO(string Type, string Id);

public sealed record ResourceDTO(string Type, string Id);

public sealed record AuditRecordSummaryDTO
{
    public Guid Id { get; init; }
    public Guid? TenantId { get; init; }
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public int EventVersion { get; init; }
    public string? Action { get; init; }
    public ActorDTO? Actor { get; init; }
    public required string SourceService { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset RecordedAt { get; init; }
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public ResourceDTO? Resource { get; init; }

    public static AuditRecordSummaryDTO FromDomain(AuditRecord record)
    {
        return new AuditRecordSummaryDTO
        {
            Id = record.Id,
            TenantId = record.TenantId,
            EventId = record.EventId,
            EventType = record.EventType,
            EventVersion = record.EventVersion,
            Action = record.Action,
            Actor = record.Actor is null ? null : new ActorDTO(record.Actor.Type.ToString(), record.Actor.Id),
            SourceService = record.SourceService,
            OccurredAt = record.OccurredAt,
            RecordedAt = record.RecordedAt,
            CorrelationId = record.CorrelationId,
            CausationId = record.CausationId,
            Resource = record.Resource is null ? null : new ResourceDTO(record.Resource.Type, record.Resource.Id)
        };
    }
}

public sealed record AuditRecordDetailDTO
{
    public required AuditRecordSummaryDTO Record { get; init; }
    public JsonElement? Metadata { get; init; }
    public JsonElement? BeforeState { get; init; }
    public JsonElement? AfterState { get; init; }

    /// <summary>
    /// Indica se os estados anterior/posterior foram omitidos por falta de permissão do solicitante.
    /// </summary>
    public bool StatesRedacted { get; init; }

    public string? IntegrityHash { get; init; }

    /// <summary>
    /// Resultado da verificação do hash de integridade no momento da leitura.
    /// Um hash válido não prova ausência de adulteração por quem possui acesso privilegiado ao banco.
    /// </summary>
    public bool? IntegrityVerified { get; init; }

    public int SchemaVersion { get; init; }
}
