using AuditCompliance.Application.Common;
using AuditCompliance.Application.DTOs;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Application.Policies;

namespace AuditCompliance.Application.Records;

public sealed record SearchAuditRecordsQuery
{
    public Guid TenantId { get; init; }
    public required AuditFilterInput Filter { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }

    /// <summary>occurredAt (padrão) ou recordedAt.</summary>
    public string? SortBy { get; init; }

    /// <summary>desc (padrão: registros mais recentes primeiro) ou asc.</summary>
    public string? SortDirection { get; init; }
}

public sealed class SearchAuditRecordsHandler
{
    private readonly AuditQueryPolicy _policy;
    private readonly IAuditRecordQueryRepository _queryRepository;

    public SearchAuditRecordsHandler(IAuditRecordQueryRepository queryRepository, AuditQueryPolicy policy)
    {
        _queryRepository = queryRepository;
        _policy = policy;
    }

    public async Task<Result<PagedResultDTO<AuditRecordSummaryDTO>>> HandleAsync(SearchAuditRecordsQuery query,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var page = query.Page ?? AuditQueryPolicy.DefaultPage;
        var pageSize = query.PageSize ?? AuditQueryPolicy.DefaultPageSize;

        if (page < 1) errors["page"] = ["'page' must be greater than or equal to 1."];

        if (pageSize is < 1 or > AuditQueryPolicy.MaxPageSize)
            errors["pageSize"] = [$"'pageSize' must be between 1 and {AuditQueryPolicy.MaxPageSize}."];

        var sort = ParseSort(query.SortBy, query.SortDirection, errors);
        var filter = AuditFilterValidator.Validate(query.TenantId, query.Filter, _policy.MaxQueryRangeDays, errors);

        if (!filter.IsSuccess) return filter.Error!;

        var (items, total) = await _queryRepository.SearchAsync(filter.Value!, sort!, (page - 1) * pageSize,
            pageSize, cancellationToken);

        return new PagedResultDTO<AuditRecordSummaryDTO>
        {
            Items = items.Select(AuditRecordSummaryDTO.FromDomain).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }

    private static AuditRecordSort? ParseSort(string? sortBy, string? sortDirection,
        IDictionary<string, string[]> errors)
    {
        AuditRecordSortFieldEnum? field = sortBy?.ToLowerInvariant() switch
        {
            null or "occurredat" => AuditRecordSortFieldEnum.OccurredAt,
            "recordedat" => AuditRecordSortFieldEnum.RecordedAt,
            _ => null
        };

        bool? descending = sortDirection?.ToLowerInvariant() switch
        {
            null or "desc" => true,
            "asc" => false,
            _ => null
        };

        if (field is null) errors["sortBy"] = ["'sortBy' must be 'occurredAt' or 'recordedAt'."];
        if (descending is null) errors["sortDirection"] = ["'sortDirection' must be 'asc' or 'desc'."];

        return field is null || descending is null ? null : new AuditRecordSort(field.Value, descending.Value);
    }
}
