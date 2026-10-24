namespace Reporting.Domain.Entities;

public sealed class ReportingEventProcessingAudit
{
    public ReportingEventProcessingAudit()
    {
    }

    public ReportingEventProcessingAudit(
        string eventId,
        string sourceTopic,
        int partition,
        long offset,
        string status,
        int attemptCount,
        string? errorMessage,
        DateTime processedAt)
    {
        EventId = eventId;
        SourceTopic = sourceTopic;
        Partition = partition;
        Offset = offset;
        Status = status;
        AttemptCount = attemptCount;
        ErrorMessage = errorMessage;
        ProcessedAt = processedAt;
    }

    public string EventId { get; init; } = string.Empty;
    public string SourceTopic { get; init; } = string.Empty;
    public int Partition { get; init; }
    public long Offset { get; init; }
    public string Status { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}
