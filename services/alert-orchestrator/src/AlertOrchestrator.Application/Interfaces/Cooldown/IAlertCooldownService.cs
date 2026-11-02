namespace AlertOrchestrator.Application.Interfaces.Cooldown;

public interface IAlertCooldownService
{
    Task<bool> IsInCooldownAsync(string region, string riskType, string? tenantId,
        CancellationToken cancellationToken = default);

    Task RecordAlertAsync(string region, string riskType, string? tenantId,
        CancellationToken cancellationToken = default);

    Task<TimeSpan?> GetRemainingCooldownAsync(string region, string riskType, string? tenantId,
        CancellationToken cancellationToken = default);
}