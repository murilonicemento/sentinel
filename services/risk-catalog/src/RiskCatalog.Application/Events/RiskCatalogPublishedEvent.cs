namespace RiskCatalog.Application.Events;

public record RiskCatalogPublishedEvent
{
    public int Version { get; init; }
    public string Notes { get; init; }
    public DateTime PublishedAt { get; init; }
    public Guid PublishedBy { get; init; }
}

