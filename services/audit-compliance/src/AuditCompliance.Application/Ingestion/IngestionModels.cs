namespace AuditCompliance.Application.Ingestion;

public sealed record IncomingAuditMessage(
    string Payload,
    string Topic,
    int Partition,
    long Offset,
    DateTimeOffset? BrokerTimestamp);

public enum IngestionStatusEnum
{
    Persisted,
    Duplicate,
    Rejected
}

public sealed record IngestionOutcome(
    IngestionStatusEnum Status,
    Guid? RecordId = null,
    string? RejectionCode = null,
    string? RejectionReason = null)
{
    public static IngestionOutcome Rejected(string code, string reason)
    {
        return new IngestionOutcome(IngestionStatusEnum.Rejected, RejectionCode: code, RejectionReason: reason);
    }
}
