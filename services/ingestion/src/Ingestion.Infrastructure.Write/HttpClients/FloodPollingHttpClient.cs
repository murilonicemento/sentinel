using Ingestion.Domain.Enums;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>Daily precipitation (GHCND PRCP); the data source must measure Rainfall.</summary>
public class FloodPollingHttpClient(HttpClient httpClient) : NoaaPollingClient(httpClient)
{
    public override string Source => PollingSources.Flood;
    protected override string[] DataTypes => ["PRCP"];
    protected override string Unit => "mm";
    protected override SensorDomainEnum Domain => SensorDomainEnum.Climatic;
}
