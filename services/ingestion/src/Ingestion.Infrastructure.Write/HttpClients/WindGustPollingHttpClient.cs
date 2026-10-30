using Ingestion.Application.DTO;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>Hourly wind gust; the data source must measure WindSpeed.</summary>
public class WindGustPollingHttpClient(HttpClient httpClient) : StormglassPollingClient(httpClient)
{
    public override string Source => PollingSources.WindGust;
    protected override string Parameter => "gust";
    protected override string Unit => "m/s";
    protected override Func<StormglassHourDTO, Dictionary<string, double>> Values => hour => hour.Gust;
}
