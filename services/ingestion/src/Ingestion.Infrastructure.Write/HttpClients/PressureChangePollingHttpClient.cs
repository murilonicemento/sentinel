using Ingestion.Application.DTO;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>Hourly air pressure; the data source must measure Pressure.</summary>
public class PressureChangePollingHttpClient(HttpClient httpClient) : StormglassPollingClient(httpClient)
{
    public override string Source => PollingSources.PressureChange;
    protected override string Parameter => "pressure";
    protected override string Unit => "hPa";
    protected override Func<StormglassHourDTO, Dictionary<string, double>> Values => hour => hour.Pressure;
}
