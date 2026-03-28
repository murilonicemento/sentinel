using MediatR;

namespace RiskEvaluation.Domain.Contracts;

public class SensorEventDetected : INotification
{
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public double Gust { get; set; }
    public double Precipitation { get; set; }
    public double Pressure { get; set; }
    public DateTime Timestamp { get; set; }
}