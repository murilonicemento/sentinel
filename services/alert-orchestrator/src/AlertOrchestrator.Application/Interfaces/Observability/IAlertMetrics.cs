namespace AlertOrchestrator.Application.Interfaces.Observability;

public interface IAlertMetrics
{
    void AlertWindowOpened(string region, string riskType);
    void AlertWindowClosed(string region, string riskType);
    void AlertTriggered(string region, string riskType, int signalCount);
    void AlertExpired(string region, string riskType);
    void QuorumFailed(string region, string riskType, int currentSignals, int requiredSignals);
    void SignalAdded(string region, string riskType, string source);
}
