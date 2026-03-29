using AlertOrchestrator.Application.DTOs;
using AlertOrchestrator.Application.Interfaces.Services;
using AlertOrchestrator.Domain.Enums;
using AlertOrchestrator.Domain.Interfaces.Repositories;

namespace AlertOrchestrator.Application.Services;

public sealed class AlertWindowQueryService : IAlertWindowQueryService
{
    private readonly IAlertWindowRepository _repository;

    public AlertWindowQueryService(IAlertWindowRepository repository)
    {
        _repository = repository;
    }

    public async Task<AlertWindowDTO?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var window = await _repository.GetByIdAsync(id, cancellationToken);
        return window is null ? null : MapToDto(window);
    }

    public async Task<IReadOnlyList<AlertWindowDTO>> GetByRegionAsync(string region, CancellationToken cancellationToken = default)
    {
        var windows = await _repository.GetByRegionAsync(region, cancellationToken);
        return windows.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AlertWindowDTO>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AlertStatus>(status, true, out var alertStatus))
        {
            return Array.Empty<AlertWindowDTO>();
        }

        var windows = await _repository.GetByStatusAsync(alertStatus, cancellationToken);
        return windows.Select(MapToDto).ToList();
    }

    private static AlertWindowDTO MapToDto(Domain.Aggregates.AlertWindow window)
    {
        return new AlertWindowDTO(
            window.Id,
            window.Region,
            window.RiskType.ToString(),
            window.TenantId,
            window.OpenedAt,
            window.ExpiresAt,
            window.Status.ToString(),
            window.Threshold,
            window.Signals.Count,
            window.TriggeredAt,
            window.FinalRiskScore,
            window.Signals.Select(s => new SignalDTO(
                s.Source.ToString(),
                s.Timestamp,
                s.EventId,
                s.RiskScore,
                s.RiskType.ToString())).ToList());
    }
}
