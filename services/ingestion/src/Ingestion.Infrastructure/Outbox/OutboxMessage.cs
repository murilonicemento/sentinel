namespace Ingestion.Infrastructure.Outbox;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid AggregateId { get; set; }
    public string OutboxType { get; set; }
    public string Payload { get; set; }
    public bool Processed { get; set; }
    public DateTime ProcessedAt { get; set; }
}