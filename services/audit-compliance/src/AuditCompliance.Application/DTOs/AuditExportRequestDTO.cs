namespace AuditCompliance.Application.DTOs;

public sealed record AuditExportRequestDTO
{
    /// <summary>csv ou json.</summary>
    public string? Format { get; init; }

    /// <summary>Início inclusivo, ISO 8601 com offset explícito.</summary>
    public string? From { get; init; }

    /// <summary>Fim exclusivo, ISO 8601 com offset explícito.</summary>
    public string? To { get; init; }

    public string? EventType { get; init; }
    public string? ActorId { get; init; }
    public string? SourceService { get; init; }
    public string? ResourceType { get; init; }
    public string? ResourceId { get; init; }
    public string? CorrelationId { get; init; }
}
