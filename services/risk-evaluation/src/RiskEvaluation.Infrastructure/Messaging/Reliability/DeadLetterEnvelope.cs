namespace RiskEvaluation.Infrastructure.Messaging.Reliability;

/// <summary>
/// Message published to the service's dead-letter topic. Same shape as audit-compliance's envelope: it keeps the
/// original payload so operators can inspect and replay it. Access to the topic must be restricted to operators.
/// </summary>
public sealed record DeadLetterEnvelope
{
    public required string OriginalTopic { get; init; }
    public int Partition { get; init; }
    public long Offset { get; init; }
    public string? Key { get; init; }
    public required string Payload { get; init; }
    public required string ErrorCode { get; init; }
    public required string ErrorReason { get; init; }
    public DateTimeOffset FailedAt { get; init; }
    public required string ConsumerGroup { get; init; }
}
