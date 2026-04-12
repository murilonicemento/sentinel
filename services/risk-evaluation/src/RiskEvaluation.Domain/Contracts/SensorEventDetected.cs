using MediatR;

namespace RiskEvaluation.Domain.Contracts;

public class SensorEventDetected : INotification
{
    public Guid CollectionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public double Intensity { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime CollectedAt { get; set; }
}