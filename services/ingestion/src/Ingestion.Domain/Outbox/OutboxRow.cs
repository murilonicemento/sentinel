namespace Ingestion.Domain.Outbox;

public class OutboxRow
{
    public Guid Id { get; }
    public string OutboxType { get; } = string.Empty;
    public string Payload { get; } = string.Empty;

    /// <summary>W3C <c>traceparent</c> of the request that wrote the row; null for rows written before X-19.</summary>
    public string? TraceParent { get; }
}
