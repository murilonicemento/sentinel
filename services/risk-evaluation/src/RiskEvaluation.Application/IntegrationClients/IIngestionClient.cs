using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.Application.IntegrationClients;

/// <summary>
/// Client for integrating with Ingestion service to fetch raw sensor data.
/// </summary>
public interface IIngestionClient
{
    public Task<SensorDataDto?> GetLatestSensorDataAsync(int latitude, int longitude, CancellationToken cancellationToken = default);
    public Task<IReadOnlyList<SensorDataDto>> GetSensorDataHistoryAsync(int latitude, int longitude, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}

public class SensorDataDto
{
    public int Latitude { get; set; }
    public int Longitude { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double WindGust { get; set; }
    public double Rainfall { get; set; }
    public double Pressure { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> RawMetrics { get; set; } = new();
}
