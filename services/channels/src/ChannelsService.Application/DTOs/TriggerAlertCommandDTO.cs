namespace ChannelsService.Application.DTOs;

/// <summary>
/// The fields of alert-orchestrator's <c>TriggerAlertCommand</c> (topic <c>alert-commands</c>) that the delivery
/// needs. <c>EscalationLevel</c> is the orchestrator's level number (1 to 3).
/// </summary>
public sealed record TriggerAlertCommandDTO(
    Guid AlertWindowId,
    string Region,
    string RiskType,
    double RiskScore,
    int SignalCount,
    DateTime TriggeredAt,
    int EscalationLevel,
    string? TenantId = null,
    List<string>? TargetChannels = null,
    string? Title = null,
    string? Message = null);
