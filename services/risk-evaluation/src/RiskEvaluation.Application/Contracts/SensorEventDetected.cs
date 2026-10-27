using MediatR;

namespace RiskEvaluation.Application.Contracts;

public class SensorEventDetected : INotification
{
    /// <summary>Set by producers that publish an event id; used to discard redelivered events.</summary>
    public Guid? EventId { get; set; }

    public Guid CollectionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public double Intensity { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CollectedAt { get; set; }
}