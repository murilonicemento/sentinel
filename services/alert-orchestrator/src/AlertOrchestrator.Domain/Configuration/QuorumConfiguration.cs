namespace AlertOrchestrator.Domain.Configuration;

public sealed record QuorumConfiguration(
    int MinimumSignals = 2,
    int RequiredDistinctSources = 1,
    bool RequireSensor = false,
    bool RequireSatellite = false
);