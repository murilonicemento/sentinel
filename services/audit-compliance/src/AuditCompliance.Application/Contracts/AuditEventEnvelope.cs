using System.Text.Json;

namespace AuditCompliance.Application.Contracts;

/// <summary>
/// Envelope padronizado (v1) dos eventos auditáveis publicados no tópico de auditoria.
/// Campos desconhecidos são ignorados para permitir evolução aditiva do contrato.
/// </summary>
public sealed record AuditEventEnvelope
{
    public string? EventId { get; init; }
    public string? EventType { get; init; }
    public int? EventVersion { get; init; }
    public string? SourceService { get; init; }

    /// <summary>ISO 8601 com offset explícito, preferencialmente UTC (sufixo Z).</summary>
    public string? OccurredAt { get; init; }

    public string? TenantId { get; init; }
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string? Action { get; init; }
    public ActorContract? Actor { get; init; }
    public ResourceContract? Resource { get; init; }
    public JsonElement? Metadata { get; init; }
    public JsonElement? BeforeState { get; init; }
    public JsonElement? AfterState { get; init; }
}

public sealed record ActorContract
{
    /// <summary>User, Service ou System.</summary>
    public string? Type { get; init; }

    public string? Id { get; init; }
}

public sealed record ResourceContract
{
    public string? Type { get; init; }
    public string? Id { get; init; }
}
