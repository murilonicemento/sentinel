using System.Diagnostics.Metrics;

namespace Ingestion.Infrastructure.Write.HostedServices;

/// <summary>Polling metrics (meter <see cref="MeterName"/>), labeled by source and outcome only.</summary>
public sealed class PollingMetrics : IDisposable
{
    public const string MeterName = "Ingestion.Polling";

    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _requests;
    private readonly Counter<long> _readings;

    public PollingMetrics()
    {
        _requests = _meter.CreateCounter<long>("ingestion_polling_requests",
            description: "Provider requests per source and result (success, failure)");
        _readings = _meter.CreateCounter<long>("ingestion_polling_readings",
            description: "Polled readings per source and result (processed, rejected, failed)");
    }

    public void Request(string source, bool success) =>
        _requests.Add(1, new("source", source), new("result", success ? "success" : "failure"));

    public void Reading(string source, string result) =>
        _readings.Add(1, new("source", source), new("result", result));

    public void Dispose() => _meter.Dispose();
}
