using AuditCompliance.Application.Common;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.Catalog;
using AuditCompliance.Domain.ValueObjects;

namespace AuditCompliance.Application.Records;

public sealed record AuditFilterInput
{
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

public static class AuditFilterValidator
{
    public static Result<AuditRecordFilter> Validate(Guid tenantId, AuditFilterInput input, int maxRangeDays,
        IDictionary<string, string[]>? fieldErrors = null)
    {
        var errors = fieldErrors ?? new Dictionary<string, string[]>();

        var hasFrom = ParseTimestamp(errors, "from", input.From, out var from);
        var hasTo = ParseTimestamp(errors, "to", input.To, out var to);

        if (hasFrom && hasTo)
        {
            if (from >= to)
                errors["to"] = ["'to' must be later than 'from'."];
            else if (to - from > TimeSpan.FromDays(maxRangeDays))
                errors["to"] = [$"The interval between 'from' and 'to' must not exceed {maxRangeDays} days."];
        }

        if (input.EventType is not null && !AuditEventCatalog.IsKnown(input.EventType))
            errors["eventType"] = ["Unknown event type."];

        ValidateLength(errors, "actorId", input.ActorId);
        ValidateLength(errors, "sourceService", input.SourceService);
        ValidateLength(errors, "resourceType", input.ResourceType);
        ValidateLength(errors, "resourceId", input.ResourceId);
        ValidateLength(errors, "correlationId", input.CorrelationId);

        if (errors.Count > 0)
            return new Error(ApplicationErrorCodes.ValidationError, "One or more validation errors occurred.",
                ErrorTypeEnum.Validation, new Dictionary<string, string[]>(errors));

        return AuditRecordFilter.Create(tenantId, from, to, input.EventType, input.ActorId,
            input.SourceService, input.ResourceType, input.ResourceId, input.CorrelationId);
    }

    private static bool ParseTimestamp(IDictionary<string, string[]> errors, string field, string? value,
        out DateTimeOffset result)
    {
        if (value is null)
        {
            errors[field] = [$"'{field}' is required."];
            result = default;
            return false;
        }

        if (IsoTimestamp.TryParse(value, out result)) return true;

        errors[field] = [$"'{field}' must be an ISO 8601 timestamp with an explicit offset (e.g. 2026-01-01T00:00:00Z)."];
        return false;
    }

    private static void ValidateLength(IDictionary<string, string[]> errors, string field, string? value)
    {
        if (value is not null && value.Length > AuditRecord.MaxIdentifierLength)
            errors[field] = [$"'{field}' must have at most {AuditRecord.MaxIdentifierLength} characters."];
    }
}
