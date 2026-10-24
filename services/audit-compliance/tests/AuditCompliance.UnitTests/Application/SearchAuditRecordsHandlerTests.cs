using AuditCompliance.Application.Common;
using AuditCompliance.Application.Interfaces;
using AuditCompliance.Application.Policies;
using AuditCompliance.Application.Records;
using AuditCompliance.Domain.Aggregates;
using AuditCompliance.Domain.ValueObjects;
using AuditCompliance.UnitTests.Support;
using Moq;

namespace AuditCompliance.UnitTests.Application;

public class SearchAuditRecordsHandlerTests
{
    private readonly Mock<IAuditRecordQueryRepository> _repository = new();
    private readonly SearchAuditRecordsHandler _handler;

    public SearchAuditRecordsHandlerTests()
    {
        _handler = new SearchAuditRecordsHandler(_repository.Object, new AuditQueryPolicy { MaxQueryRangeDays = 30 });
    }

    [Fact]
    public async Task HandleAsync_WithDefaults_UsesFirstPageTenItemsAndNewestFirst()
    {
        AuditRecordFilter? capturedFilter = null;
        _repository.Setup(r => r.SearchAsync(It.IsAny<AuditRecordFilter>(), It.IsAny<AuditRecordSort>(), 0, 10,
                It.IsAny<CancellationToken>()))
            .Callback<AuditRecordFilter, AuditRecordSort, int, int, CancellationToken>((f, _, _, _, _) => capturedFilter = f)
            .ReturnsAsync(((IReadOnlyList<AuditRecord>)[TestData.Record()], 21L));

        var result = await _handler.HandleAsync(Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Page);
        Assert.Equal(10, result.Value.PageSize);
        Assert.Equal(21, result.Value.TotalItems);
        Assert.Equal(3, result.Value.TotalPages);
        Assert.Single(result.Value.Items);
        Assert.Equal(TestData.TenantId, capturedFilter!.TenantId);
        _repository.Verify(r => r.SearchAsync(It.IsAny<AuditRecordFilter>(),
            new AuditRecordSort(AuditRecordSortFieldEnum.OccurredAt, true), 0, 10, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task HandleAsync_WithExplicitPagingAndSort_ComputesSkip()
    {
        _repository.Setup(r => r.SearchAsync(It.IsAny<AuditRecordFilter>(), It.IsAny<AuditRecordSort>(),
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<AuditRecord>)[], 0L));

        var result = await _handler.HandleAsync(Query() with
        {
            Page = 3,
            PageSize = 50,
            SortBy = "recordedAt",
            SortDirection = "asc"
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.TotalPages);
        _repository.Verify(r => r.SearchAsync(It.IsAny<AuditRecordFilter>(),
            new AuditRecordSort(AuditRecordSortFieldEnum.RecordedAt, false), 100, 50, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData(0, 10, null, null, "page")]
    [InlineData(1, 0, null, null, "pageSize")]
    [InlineData(1, 51, null, null, "pageSize")]
    [InlineData(1, 10, "eventType", null, "sortBy")]
    [InlineData(1, 10, null, "random", "sortDirection")]
    public async Task HandleAsync_WithInvalidPagingOrSort_ReturnsFieldError(int page, int pageSize, string? sortBy,
        string? sortDirection, string expectedField)
    {
        var result = await _handler.HandleAsync(Query() with
        {
            Page = page,
            PageSize = pageSize,
            SortBy = sortBy,
            SortDirection = sortDirection
        }, CancellationToken.None);

        AssertValidationError(result.Error, expectedField);
    }

    [Theory]
    [InlineData(null, "2026-03-10T00:00:00Z", "from")]
    [InlineData("2026-03-01T00:00:00Z", null, "to")]
    [InlineData("2026-03-01T00:00:00", "2026-03-10T00:00:00Z", "from")]
    [InlineData("03/01/2026", "2026-03-10T00:00:00Z", "from")]
    [InlineData("2026-03-10T00:00:00Z", "2026-03-01T00:00:00Z", "to")]
    [InlineData("2026-01-01T00:00:00Z", "2026-03-01T00:00:00Z", "to")]
    public async Task HandleAsync_WithInvalidInterval_ReturnsFieldError(string? from, string? to, string expectedField)
    {
        var result = await _handler.HandleAsync(Query(from, to), CancellationToken.None);

        AssertValidationError(result.Error, expectedField);
    }

    [Fact]
    public async Task HandleAsync_WithUnknownEventTypeFilter_ReturnsFieldError()
    {
        var query = Query();
        var result = await _handler.HandleAsync(query with { Filter = query.Filter with { EventType = "Nope" } },
            CancellationToken.None);

        AssertValidationError(result.Error, "eventType");
    }

    private void AssertValidationError(Error? error, string expectedField)
    {
        Assert.NotNull(error);
        Assert.Equal(ErrorTypeEnum.Validation, error!.Type);
        Assert.Equal(ApplicationErrorCodes.ValidationError, error.Code);
        Assert.Contains(expectedField, error.FieldErrors!.Keys);
        _repository.VerifyNoOtherCalls();
    }

    private static SearchAuditRecordsQuery Query(string? from = "2026-03-01T00:00:00Z",
        string? to = "2026-03-10T00:00:00Z")
    {
        return new SearchAuditRecordsQuery
        {
            TenantId = TestData.TenantId,
            Filter = new AuditFilterInput { From = from, To = to }
        };
    }
}
