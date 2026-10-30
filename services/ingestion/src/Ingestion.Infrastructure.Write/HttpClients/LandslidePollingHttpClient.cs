using System.Text.Json;
using Ingestion.Application.DTO;
using Ingestion.Domain.Enums;
using Ingestion.Infrastructure.Write.Options;

namespace Ingestion.Infrastructure.Write.HttpClients;

/// <summary>
/// Precipitation accumulated over the whole window (GHCND PRCP), the trigger the landslide source watches for; one
/// reading per station and window end day. The data source must measure Rainfall.
/// </summary>
public class LandslidePollingHttpClient(HttpClient httpClient) : NoaaPollingClient(httpClient)
{
    public override string Source => PollingSources.Landslide;
    protected override string[] DataTypes => ["PRCP"];
    protected override string Unit => "mm";
    protected override SensorDomainEnum Domain => SensorDomainEnum.Climatic;

    protected override List<SensorCollectionDTO> Map(PollingSubscription subscription, PollingWindow window,
        List<NoaaResultDTO> results)
    {
        var day = window.To.Date;
        return
        [
            Collection(subscription, DateTime.SpecifyKind(day, DateTimeKind.Utc),
                $"{subscription.StationId}:PRCP-ACCUMULATED:{window.From:yyyy-MM-dd}:{day:yyyy-MM-dd}",
                results.Sum(r => r.Value), JsonSerializer.Serialize(results))
        ];
    }
}
