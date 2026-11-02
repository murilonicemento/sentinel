using AlertOrchestrator.Application.DTOs;
using AlertOrchestrator.Application.Interfaces.Services;
using AlertOrchestrator.Domain.Aggregates;
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

    public async Task<AlertWindowDTO?> GetByIdAsync(Guid id, AlertWindowAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        var window = await _repository.GetByIdAsync(id, cancellationToken);

        // A window outside the caller's tenants is reported as missing so its existence is not disclosed.
        return window is null || !scope.Allows(window.TenantId) ? null : MapToDto(window);
    }

    public async Task<IReadOnlyList<AlertWindowDTO>> GetByRegionAsync(string region, AlertWindowAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        var windows = await _repository.GetByRegionAsync(region, cancellationToken);
        return windows.Where(w => scope.Allows(w.TenantId)).Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<AlertWindowDTO>> GetByStatusAsync(string status, AlertWindowAccessScope scope,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<AlertStatus>(status, true, out var alertStatus)) return Array.Empty<AlertWindowDTO>();

        var windows = await _repository.GetByStatusAsync(alertStatus, cancellationToken);
        return windows.Where(w => scope.Allows(w.TenantId)).Select(MapToDto).ToList();
    }

    public async Task<PagedResultDTO<AlertWindowDTO>> SearchAsync(string? region, string? status,
        AlertWindowAccessScope scope, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(region) && string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Provide a region or a status filter.", nameof(region));

        AlertStatus? alertStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AlertStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new ArgumentException($"Unknown status '{status}'.", nameof(status));
            alertStatus = parsed;
        }

        var result = await _repository.SearchAsync(region, alertStatus,
            scope.AllTenants ? null : scope.TenantIds.ToList(), page, pageSize, cancellationToken);

        return new PagedResultDTO<AlertWindowDTO>
        {
            Items = result.Items.Select(MapToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = result.TotalItems,
            TotalPages = (int)Math.Ceiling(result.TotalItems / (double)pageSize)
        };
    }

    private static AlertWindowDTO MapToDto(AlertWindow window)
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