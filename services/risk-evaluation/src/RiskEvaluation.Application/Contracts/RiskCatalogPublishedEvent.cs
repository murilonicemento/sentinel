using MediatR;

namespace RiskEvaluation.Application.Contracts;

/// <summary>Payload of <c>risk-catalog-published</c>, as published by risk-catalog.</summary>
public class RiskCatalogPublishedEvent : INotification
{
    public int Version { get; set; }
    public string? Notes { get; set; }
    public DateTime PublishedAt { get; set; }
    public Guid PublishedBy { get; set; }
}
