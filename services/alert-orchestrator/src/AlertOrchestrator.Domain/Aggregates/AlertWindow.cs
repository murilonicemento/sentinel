using AlertOrchestrator.Domain.Configuration;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.Events;
using AlertOrchestrator.Domain.ValueObjects;

namespace AlertOrchestrator.Domain.Aggregates;

public class AlertWindow
{
    private readonly List<object> _domainEvents = new();

    private AlertWindow()
    {
    }

    public Guid Id { get; private set; }
    public string Region { get; private set; } = string.Empty;
    public RiskType RiskType { get; private set; } = null!;
    public string? TenantId { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public AlertStatus Status { get; private set; }
    public double Threshold { get; private set; }
    public List<Signal> Signals { get; } = new();
    public DateTime? TriggeredAt { get; private set; }
    public double? FinalRiskScore { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? ClosedBy { get; private set; }
    public string? CloseReason { get; private set; }
    public AlertEscalationLevel CurrentEscalationLevel { get; private set; } = AlertEscalationLevel.Level1;
    public DateTime? ConfirmedAt { get; private set; }
    public IReadOnlyList<object> DomainEvents => _domainEvents.AsReadOnly();

    public static AlertWindow Open(
        string region,
        RiskType riskType,
        string? tenantId,
        double threshold,
        TimeSpan windowDuration)
    {
        var window = new AlertWindow
        {
            Id = Guid.NewGuid(),
            Region = region,
            RiskType = riskType,
            TenantId = tenantId,
            OpenedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(windowDuration),
            Status = AlertStatus.Open,
            Threshold = threshold
        };

        window._domainEvents.Add(new AlertWindowOpenedEvent(
            window.Id,
            window.Region,
            window.RiskType.ToString(),
            window.OpenedAt,
            window.ExpiresAt,
            window.Threshold));

        return window;
    }

    public void AddSignal(Signal signal)
    {
        if (Status != AlertStatus.Open)
            throw new InvalidOperationException($"Cannot add signal to window with status {Status}");

        if (DateTime.UtcNow > ExpiresAt) throw new InvalidOperationException("Cannot add signal to expired window");

        if (Signals.Any(s => s.EventId == signal.EventId)) return; // Idempotency: ignore duplicate signals

        Signals.Add(signal);
    }

    public bool MeetsQuorum(QuorumConfiguration quorumConfig)
    {
        if (Signals.Count < quorumConfig.MinimumSignals) return false;

        var distinctSources = Signals.Select(s => s.Source).Distinct().Count();
        if (distinctSources < quorumConfig.RequiredDistinctSources) return false;

        if (quorumConfig.RequireSensor && !Signals.Any(s => s.Source == SignalSource.Sensor)) return false;

        return !quorumConfig.RequireSatellite || Signals.Any(s => s.Source == SignalSource.Satellite);
    }

    public bool ShouldTrigger(double riskScore)
    {
        return riskScore >= Threshold;
    }

    public bool TryTrigger(double riskScore, QuorumConfiguration quorumConfig)
    {
        if (Status != AlertStatus.Open) return false;

        if (!ShouldTrigger(riskScore)) return false;

        if (!MeetsQuorum(quorumConfig)) return false;

        Status = AlertStatus.Triggered;
        TriggeredAt = DateTime.UtcNow;
        FinalRiskScore = riskScore;
        CurrentEscalationLevel = AlertEscalationLevel.Level1;

        _domainEvents.Add(new AlertTriggeredEvent(
            Id,
            Region,
            RiskType.ToString(),
            TriggeredAt.Value,
            FinalRiskScore.Value,
            Signals.Count,
            Signals.Select(s => s.Source.ToString()).ToList()));

        return true;
    }

    public bool Escalate(AlertEscalationLevel newLevel)
    {
        if (Status != AlertStatus.Triggered || ConfirmedAt.HasValue) return false;

        if (newLevel <= CurrentEscalationLevel) return false;

        CurrentEscalationLevel = newLevel;

        _domainEvents.Add(new AlertEscalatedEvent(
            Id,
            Region,
            RiskType.ToString(),
            newLevel,
            DateTime.UtcNow));

        return true;
    }

    public void ConfirmReceipt()
    {
        if (Status != AlertStatus.Triggered) return;

        ConfirmedAt = DateTime.UtcNow;

        _domainEvents.Add(new AlertConfirmedEvent(
            Id,
            Region,
            RiskType.ToString(),
            ConfirmedAt.Value));
    }

    public void MarkExpired()
    {
        if (Status != AlertStatus.Open || DateTime.UtcNow <= ExpiresAt) return;

        Status = AlertStatus.Expired;
        _domainEvents.Add(new AlertWindowExpiredEvent(
            Id,
            Region,
            RiskType.ToString(),
            DateTime.UtcNow,
            Signals.Count));
    }

    public void Close(string closedBy, string? reason = null)
    {
        if (Status == AlertStatus.Closed) throw new InvalidOperationException("Alert window is already closed");

        Status = AlertStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        ClosedBy = closedBy;
        CloseReason = reason;

        _domainEvents.Add(new AlertClosedEvent(
            Id,
            Region,
            RiskType.ToString(),
            ClosedAt.Value,
            Signals.Count,
            ClosedBy,
            reason));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}