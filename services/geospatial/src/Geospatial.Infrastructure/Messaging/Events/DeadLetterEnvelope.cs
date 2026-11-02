namespace Geospatial.Infrastructure.Messaging.Events;

public record DeadLetterEnvelope
{
    public string OriginalTopic { get; init; } = default!;
    public int OriginalPartition { get; init; }
    public long OriginalOffset { get; init; }
    public string Payload { get; init; } = default!;
    public DateTime FailedAt { get; init; }
}