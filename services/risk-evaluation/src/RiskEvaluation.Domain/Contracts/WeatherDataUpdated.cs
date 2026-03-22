using MediatR;

namespace RiskEvaluation.Domain.Contracts;

public class WeatherDataUpdated : INotification
{
    public string Location { get; set; }
    public double Gust { get; set; }
    public double Precipitation { get; set; }
    public double Pressure { get; set; }
    public DateTime Timestamp { get; set; }
}