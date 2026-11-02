using AlertOrchestrator.Application.DTOs;

namespace AlertOrchestrator.Application.Ports;

public interface IAlertConfigurationPort
{
    public Task<TriggerRuleConfiguration> GetConfigurationAsync(
        string riskType,
        string? tenantId,
        CancellationToken cancellationToken = default);
}