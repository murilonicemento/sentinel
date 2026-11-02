using System.Diagnostics.Metrics;

namespace Ingestion.Infrastructure.Write.HostedServices;

/// <summary>Outbox backlog (meter <see cref="MeterName"/>), updated after every relay pass.</summary>
public sealed class OutboxMetrics : IDisposable
{
    public const string MeterName = "Ingestion.Outbox";

    private readonly Meter _meter = new(MeterName);

    public OutboxMetrics()
    {
        _meter.CreateObservableGauge("ingestion_outbox_pending", () => Pending,
            description: "Outbox rows not yet published to Kafka");
    }

    public long Pending { get; set; }

    public void Dispose() => _meter.Dispose();
}
