using System.Text.Json;
using RiskCatalog.Application.DTO;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.Application.Services;

/// <summary>
/// Paged, deterministic listings of the catalog data other services copy (risk-evaluation builds its local snapshot
/// from them after each publication).
/// </summary>
public sealed class CatalogListingService
{
    private readonly IEventTypeRepository _eventTypes;
    private readonly IRegionalParameterRepository _regionalParameters;
    private readonly IRiskMatrixRepository _riskMatrices;

    public CatalogListingService(IRegionalParameterRepository regionalParameters, IRiskMatrixRepository riskMatrices,
        IEventTypeRepository eventTypes)
    {
        _regionalParameters = regionalParameters;
        _riskMatrices = riskMatrices;
        _eventTypes = eventTypes;
    }

    public async Task<PagedResultDTO<RegionalRiskParametersDTO>> GetRegionalParametersAsync(int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _regionalParameters.GetPageAsync(page, pageSize, cancellationToken);
        return Page(items.Select(r => new RegionalRiskParametersDTO
        {
            RegionId = r.Id,
            AdjustmentFactor = r.AdjustmentFactor,
            Description = r.Description,
            CenterLatitude = r.CenterLatitude,
            CenterLongitude = r.CenterLongitude,
            RegionBounds = ParseBounds(r.RegionBoundsJson),
            CoverageRadiusKm = r.CoverageRadiusKm
        }).ToList(), page, pageSize, total);
    }

    public async Task<PagedResultDTO<RiskMatrixDTO>> GetRiskMatrixEntriesAsync(int? version, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (items, total) = await _riskMatrices.GetPageAsync(version, page, pageSize, cancellationToken);
        return Page(items.Select(m => new RiskMatrixDTO
        {
            EventTypeCode = m.EventType.Code,
            SeverityLevel = m.SeverityLevel.ToString(),
            RiskLevel = m.RiskLevel.ToString(),
            Version = m.Version
        }).ToList(), page, pageSize, total);
    }

    public async Task<PagedResultDTO<SeverityCriterionDTO>> GetSeverityCriteriaAsync(int? version, int page,
        int pageSize, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _eventTypes.GetSeverityCriteriaPageAsync(version, page, pageSize, cancellationToken);
        return Page(items.Select(c => new SeverityCriterionDTO
        {
            EventTypeCode = c.EventType.Code,
            SeverityLevel = c.Severity.Level.ToString(),
            MinValue = c.MinValue,
            MaxValue = c.MaxValue,
            Unit = c.Unit,
            Version = c.Version
        }).ToList(), page, pageSize, total);
    }

    private static PagedResultDTO<T> Page<T>(IReadOnlyList<T> items, int page, int pageSize, long total) => new()
    {
        Items = items,
        Page = page,
        PageSize = pageSize,
        TotalItems = total,
        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
    };

    // Bounds are stored as JSON; a malformed value is treated as "no polygon" rather than failing the listing.
    private static PolygonDTO? ParseBounds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<PolygonDTO>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
