using Ingestion.Domain.Enums;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>Daily average, maximum and minimum temperature (GHCND TAVG/TMAX/TMIN).</summary>
public class TemperatureAnomalyPollingHttpClient(HttpClient httpClient) : NoaaPollingClient(httpClient)
{
    public override string Source => PollingSources.TemperatureAnomaly;
    protected override string[] DataTypes => ["TAVG", "TMAX", "TMIN"];
    protected override string Unit => "C";
    protected override SensorDomainEnum Domain => SensorDomainEnum.Climatic;
}
