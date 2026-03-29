namespace AlertOrchestrator.Application.Commands;

public sealed record TriggerAlertCommand(
    Guid AlertWindowId,
    string Region,
    string RiskType,
    double RiskScore,
    int SignalCount,
    List<string> SignalSources,
    DateTime TriggeredAt,
    // Tenant context for multi-tenancy
    string? TenantId = null,
    // Severity/Priority for Channels Service routing
    string Severity = "high",
    string Priority = "urgent",
    // Channels Service integration
    List<string>? TargetChannels = null,
    Dictionary<string, string>? ChannelSpecificSettings = null,
    // Content for notifications
    string? Title = null,
    string? Message = null,
    string? RecommendedAction = null,
    // Geospatial data
    GeoLocation? Location = null,
    // Affected population estimate
    int? EstimatedAffectedPopulation = null
);

public sealed record GeoLocation(
    double Latitude,
    double Longitude,
    double? RadiusKm = null,
    string? PolygonWkt = null
);
