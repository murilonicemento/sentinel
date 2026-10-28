namespace TenantsBilling.Infrastructure.Persistence.Entities;

public sealed class TenantUsageEntity
{
    public Guid TenantId { get; set; }
    public string Period { get; set; } = string.Empty;
    public long EventsConsumed { get; set; }
    public long AlertsTriggered { get; set; }
    public long ApiRequests { get; set; }
    public long ChannelUsage { get; set; }
    public int QuotaStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
    public uint Version { get; set; }
}

public sealed class RecordedUsageEventEntity
{
    public string SourceEventId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public string Period { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}
