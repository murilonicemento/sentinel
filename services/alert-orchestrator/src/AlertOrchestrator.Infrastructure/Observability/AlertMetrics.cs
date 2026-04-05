using System.Diagnostics.Metrics;
using AlertOrchestrator.Application.Interfaces.Observability;
using AlertOrchestrator.Domain.Enums;

namespace AlertOrchestrator.Infrastructure.Observability;

public sealed class AlertMetrics : IAlertMetrics
{
    private readonly Histogram<double> _alertLatency;
    private readonly Counter<long> _alertsConfirmed;
    private readonly Counter<long> _alertsEscalated;
    private readonly Counter<long> _alertsExpired;
    private readonly Counter<long> _alertsTriggered;
    private readonly Meter _meter;
    private readonly Counter<long> _quorumFailures;
    private readonly Counter<long> _signalsAdded;
    private readonly Counter<long> _windowsClosed;
    private readonly Counter<long> _windowsOpened;

    public AlertMetrics()
    {
        _meter = new Meter("AlertOrchestrator", "1.0.0");

        _windowsOpened = _meter.CreateCounter<long>(
            "alert_windows_opened",
            description: "Number of alert windows opened");

        _windowsClosed = _meter.CreateCounter<long>(
            "alert_windows_closed",
            description: "Number of alert windows closed");

        _alertsTriggered = _meter.CreateCounter<long>(
            "alerts_triggered",
            description: "Number of alerts triggered");

        _alertsExpired = _meter.CreateCounter<long>(
            "alerts_expired",
            description: "Number of alert windows expired");

        _quorumFailures = _meter.CreateCounter<long>(
            "quorum_failures",
            description: "Number of quorum validation failures");

        _alertsConfirmed = _meter.CreateCounter<long>(
            "alerts_confirmed",
            description: "Number of alerts confirmed");

        _alertsEscalated = _meter.CreateCounter<long>(
            "alerts_escalated",
            description: "Number of alerts escalated");

        _signalsAdded = _meter.CreateCounter<long>(
            "signals_added",
            description: "Number of signals added to windows");

        _alertLatency = _meter.CreateHistogram<double>(
            "alert_latency_seconds",
            "s",
            "Time from window open to alert trigger");
    }

    public void AlertWindowOpened(string region, string riskType)
    {
        _windowsOpened.Add(1,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType));
    }

    public void AlertWindowClosed(string region, string riskType)
    {
        _windowsClosed.Add(1,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType));
    }

    public void AlertTriggered(string region, string riskType, int signalCount)
    {
        _alertsTriggered.Add(1,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType),
            new KeyValuePair<string, object?>("signal_count", signalCount));
    }

    public void AlertExpired(string region, string riskType)
    {
        _alertsExpired.Add(1,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType));
    }

    public void AlertConfirmed(string region, string riskType)
    {
        _alertsConfirmed.Add(1,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType));
    }

    public void AlertEscalated(string region, string riskType, AlertEscalationLevel level)
    {
        _alertsEscalated.Add(1,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType),
            new KeyValuePair<string, object?>("escalation_level", level.ToString()));
    }

    public void QuorumFailed(string region, string riskType, int currentSignals, int requiredSignals)
    {
        _quorumFailures.Add(1,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType),
            new KeyValuePair<string, object?>("current_signals", currentSignals),
            new KeyValuePair<string, object?>("required_signals", requiredSignals));
    }

    public void SignalAdded(string region, string riskType, string source)
    {
        _signalsAdded.Add(1,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType),
            new KeyValuePair<string, object?>("source", source));
    }

    public void RecordAlertLatency(TimeSpan latency, string region, string riskType)
    {
        _alertLatency.Record(latency.TotalSeconds,
            new KeyValuePair<string, object?>("region", region),
            new KeyValuePair<string, object?>("risk_type", riskType));
    }
}