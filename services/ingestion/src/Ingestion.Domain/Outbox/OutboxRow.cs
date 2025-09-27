namespace Ingestion.Domain.Outbox;

public class OutboxRow
{
    public Guid Id { get; }
    public string OutboxType { get; }
    public string Payload { get; }
}