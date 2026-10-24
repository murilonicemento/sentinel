namespace Reporting.Domain.Entities;

public sealed record ServiceHealthSnapshot(
    string ServiceName,
    string Status,
    DateTime Timestamp,
    int ProcessedEvents,
    int RejectedEvents,
    int CacheHits);
