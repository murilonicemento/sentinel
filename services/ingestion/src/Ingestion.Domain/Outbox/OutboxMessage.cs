namespace Ingestion.Domain.Outbox;

public class OutboxMessage(
    Guid id,
    Guid aggregateId,
    string outboxType,
    string payload
)
{
    public Guid Id { get; } = id;
    public Guid AggregateId { get; } = aggregateId;
    public string OutboxType { get; } = outboxType;
    public string Payload { get; } = payload;
}