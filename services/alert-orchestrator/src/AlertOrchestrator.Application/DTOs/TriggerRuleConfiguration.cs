namespace AlertOrchestrator.Application.DTOs;

public sealed record TriggerRuleConfiguration(
    double Threshold,
    TimeSpan WindowDuration,
    int MinimumQuorumSignals,
    int RequiredDistinctSources,
    bool RequireSensor,
    bool RequireSatellite,
    // Tenant-specific overrides
    Dictionary<string, double>? TenantThresholdOverrides = null,
    Dictionary<string, TimeSpan>? TenantWindowDurationOverrides = null,
    // Dynamic threshold configuration
    bool EnableDynamicThreshold = false,
    double? DynamicThresholdAdjustmentFactor = null,
    int? HistoricalWindowCountForAdjustment = null,
    // Cooldown configuration
    TimeSpan? AlertCooldownDuration = null,
    // Channels Service integration
    List<string>? TargetChannels = null,
    string? Priority = null,
    Dictionary<string, string>? ChannelSpecificSettings = null,
    // Escalation configuration
    List<TimeSpan>? EscalationIntervals = null
);