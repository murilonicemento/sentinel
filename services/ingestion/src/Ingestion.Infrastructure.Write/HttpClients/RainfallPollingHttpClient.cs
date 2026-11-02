using Ingestion.Application.DTO;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>Hourly precipitation (kg/m²/h, i.e. mm per hour); the data source must measure Rainfall.</summary>
public class RainfallPollingHttpClient(HttpClient httpClient) : StormglassPollingClient(httpClient)
{
    public override string Source => PollingSources.Rainfall;
    protected override string Parameter => "precipitation";
    protected override string Unit => "mm";
    protected override Func<StormglassHourDTO, Dictionary<string, double>> Values => hour => hour.Precipitation;
}
