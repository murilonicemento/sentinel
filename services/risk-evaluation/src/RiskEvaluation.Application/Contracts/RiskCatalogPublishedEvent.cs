using MediatR;

namespace RiskEvaluation.Domain.Contracts;

public class RiskCatalogPublishedEvent : INotification
{
    public string Version { get; set; }
    public Dictionary<string, double> Parameters { get; set; }
    public string Formula { get; set; }
}