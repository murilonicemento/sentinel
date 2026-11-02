namespace AlertOrchestrator.Application.DTOs;

public sealed record AlertWindowDTO(
    Guid Id,
    string Region,
    string RiskType,
    string? TenantId,
    DateTime OpenedAt,
    DateTime ExpiresAt,
    string Status,
    double Threshold,
    int SignalCount,
    DateTime? TriggeredAt,
    double? FinalRiskScore,
    List<SignalDTO> Signals
);