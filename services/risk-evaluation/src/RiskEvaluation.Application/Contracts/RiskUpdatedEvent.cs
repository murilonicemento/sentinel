namespace RiskEvaluation.Application.Contracts;

/// <summary>
/// Payload of <c>risk-updated</c>, read by alert-orchestrator (X-24). <c>EventId</c> is the sensor event's id, so a
/// redelivered sensor event republishes the same id and the orchestrator discards it. <c>Region</c> is the catalog's
/// region code and <c>RiskType</c> the sensor event type in lower case.
/// </summary>
public sealed record RiskUpdatedEvent(
    Guid EventId,
    string Region,
    string RiskType,
    double RiskScore,
    string TenantId,
    DateTime Timestamp,
    Dictionary<string, string> Metadata);
