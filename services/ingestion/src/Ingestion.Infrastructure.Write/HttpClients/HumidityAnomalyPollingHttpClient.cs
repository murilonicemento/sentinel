using Ingestion.Domain.Enums;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>Daily average relative humidity (GHCND RHAV); not every station reports it.</summary>
public class HumidityAnomalyPollingHttpClient(HttpClient httpClient) : NoaaPollingClient(httpClient)
{
    public override string Source => PollingSources.HumidityAnomaly;
    protected override string[] DataTypes => ["RHAV"];
    protected override string Unit => "%";
    protected override SensorDomainEnum Domain => SensorDomainEnum.Climatic;
}
