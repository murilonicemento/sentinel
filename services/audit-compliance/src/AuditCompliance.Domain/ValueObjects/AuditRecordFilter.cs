using AuditCompliance.Domain.Exceptions;

namespace AuditCompliance.Domain.ValueObjects;

/// <summary>
/// Critérios explícitos de seleção de registros. Toda seleção é restrita a um único tenant e a um
/// intervalo de ocorrência fechado-aberto [From, To).
/// </summary>
public sealed record AuditRecordFilter
{
    private AuditRecordFilter()
    {
    }

    public Guid TenantId { get; private init; }
    public DateTimeOffset From { get; private init; }
    public DateTimeOffset To { get; private init; }
    public string? EventType { get; private init; }
    public string? ActorId { get; private init; }
    public string? SourceService { get; private init; }
    public string? ResourceType { get; private init; }
    public string? ResourceId { get; private init; }
    public string? CorrelationId { get; private init; }

    public static AuditRecordFilter Create(
        Guid tenantId,
        DateTimeOffset from,
        DateTimeOffset to,
        string? eventType = null,
        string? actorId = null,
        string? sourceService = null,
        string? resourceType = null,
        string? resourceId = null,
        string? correlationId = null)
    {
        if (tenantId == Guid.Empty)
            throw new AuditDomainException(AuditErrorCodes.InvalidFilter, "Tenant id is required.");

        var normalizedFrom = AuditTimestamp.Normalize(from);
        var normalizedTo = AuditTimestamp.Normalize(to);

        if (normalizedFrom >= normalizedTo)
            throw new AuditDomainException(AuditErrorCodes.InvalidFilter, "'from' must be earlier than 'to'.");

        return new AuditRecordFilter
        {
            TenantId = tenantId,
            From = normalizedFrom,
            To = normalizedTo,
            EventType = Normalize(eventType),
            ActorId = Normalize(actorId),
            SourceService = Normalize(sourceService),
            ResourceType = Normalize(resourceType),
            ResourceId = Normalize(resourceId),
            CorrelationId = Normalize(correlationId)
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
