using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.Application.Services;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.UnitTests;

public class RiskModelServiceTests
{
    private readonly Mock<IEventTypeRepository> _eventTypeRepositoryMock;
    private readonly Mock<IRiskMatrixRepository> _riskMatrixRepositoryMock;
    private readonly Mock<IIDFCurveRepository> _idfCurveRepositoryMock;
    private readonly Mock<IRegionalParameterRepository> _regionalParameterRepositoryMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly Mock<ILogger<RiskModelService>> _loggerMock;
    private readonly RiskModelService _riskModelService;

    public RiskModelServiceTests()
    {
        _eventTypeRepositoryMock = new Mock<IEventTypeRepository>();
        _riskMatrixRepositoryMock = new Mock<IRiskMatrixRepository>();
        _idfCurveRepositoryMock = new Mock<IIDFCurveRepository>();
        _regionalParameterRepositoryMock = new Mock<IRegionalParameterRepository>();
        _cacheServiceMock = new Mock<ICacheService>();
        _publisherMock = new Mock<IPublisher>();
        _loggerMock = new Mock<ILogger<RiskModelService>>();

        _riskModelService = new RiskModelService(
            _eventTypeRepositoryMock.Object,
            _riskMatrixRepositoryMock.Object,
            _idfCurveRepositoryMock.Object,
            _regionalParameterRepositoryMock.Object,
            _cacheServiceMock.Object,
            _publisherMock.Object,
            _loggerMock.Object);
    }

    #region GetRiskMatrixForEventTypeAsync Tests

    [Fact]
    public async Task GetRiskMatrixForEventTypeAsync_WhenCacheHasValue_ShouldReturnCachedResult()
    {
        var eventTypeCode = "RAIN";
        var severityLevel = "High";
        int? version = 1;
        var cachedRiskMatrix = new RiskMatrixDTO
        {
            EventTypeCode = eventTypeCode,
            SeverityLevel = severityLevel,
            RiskLevel = "Critical",
            Version = version.Value
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<RiskMatrixDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedRiskMatrix);

        var result = await _riskModelService.GetRiskMatrixForEventTypeAsync(
            eventTypeCode,
            severityLevel,
            version);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(cachedRiskMatrix);
        _riskMatrixRepositoryMock.Verify(
            x => x.GetRiskMatrixForEventTypeAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRiskMatrixForEventTypeAsync_WhenCacheIsEmptyAndRepositoryReturnsData_ShouldReturnRiskMatrixAndCacheIt()
    {
        var eventTypeCode = "RAIN";
        var severityLevel = "High";
        int? version = 1;
        var eventType = new EventType(Guid.NewGuid(), eventTypeCode, "Rain Event", "Rain description", true);
        var riskMatrix = new RiskMatrix(
            Guid.NewGuid(),
            eventType.Id,
            SeverityLevelEnum.High,
            RiskLevelEnum.Critical,
            version.Value)
        {
            EventType = eventType
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<RiskMatrixDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskMatrixDTO?)null);

        _riskMatrixRepositoryMock
            .Setup(x => x.GetRiskMatrixForEventTypeAsync(
                eventTypeCode,
                severityLevel,
                version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(riskMatrix);

        var result = await _riskModelService.GetRiskMatrixForEventTypeAsync(
            eventTypeCode,
            severityLevel,
            version);

        result.Should().NotBeNull();
        result!.EventTypeCode.Should().Be(eventTypeCode);
        result.SeverityLevel.Should().Be(severityLevel);
        result.RiskLevel.Should().Be(RiskLevelEnum.Critical.ToString());
        result.Version.Should().Be(version.Value);

        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<RiskMatrixDTO>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRiskMatrixForEventTypeAsync_WhenRiskMatrixNotFound_ShouldReturnNull()
    {
        var eventTypeCode = "RAIN";
        var severityLevel = "High";
        int? version = 1;

        _cacheServiceMock
            .Setup(x => x.GetAsync<RiskMatrixDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskMatrixDTO?)null);

        _riskMatrixRepositoryMock
            .Setup(x => x.GetRiskMatrixForEventTypeAsync(
                eventTypeCode,
                severityLevel,
                version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskMatrix?)null);

        var result = await _riskModelService.GetRiskMatrixForEventTypeAsync(
            eventTypeCode,
            severityLevel,
            version);

        result.Should().BeNull();
        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<RiskMatrixDTO>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRiskMatrixForEventTypeAsync_WhenVersionIsNull_ShouldHandleNullVersion()
    {
        var eventTypeCode = "RAIN";
        var severityLevel = "High";
        int? version = null;
        var eventType = new EventType(Guid.NewGuid(), eventTypeCode, "Rain Event", "Rain description", true);
        var riskMatrix = new RiskMatrix(
            Guid.NewGuid(),
            eventType.Id,
            SeverityLevelEnum.High,
            RiskLevelEnum.Critical,
            1)
        {
            EventType = eventType
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<RiskMatrixDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskMatrixDTO?)null);

        _riskMatrixRepositoryMock
            .Setup(x => x.GetRiskMatrixForEventTypeAsync(
                eventTypeCode,
                severityLevel,
                version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(riskMatrix);

        var result = await _riskModelService.GetRiskMatrixForEventTypeAsync(
            eventTypeCode,
            severityLevel,
            version);

        result.Should().NotBeNull();
        result!.EventTypeCode.Should().Be(eventTypeCode);
    }

    #endregion

    #region GetIDFCurvesForEventTypeAsync Tests

    [Fact]
    public async Task GetIDFCurvesForEventTypeAsync_WhenCacheHasValue_ShouldReturnCachedResult()
    {
        var eventTypeCode = "RAIN";
        int? returnPeriodYears = 10;
        var cachedIdfCurve = new IDFCurvesDTO
        {
            DurationMinutes = 60,
            Intensity = 25.5,
            ReturnPeriodYears = returnPeriodYears.Value,
            Version = 1
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<IDFCurvesDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedIdfCurve);

        var result = await _riskModelService.GetIDFCurvesForEventTypeAsync(
            eventTypeCode,
            returnPeriodYears);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(cachedIdfCurve);
        _idfCurveRepositoryMock.Verify(
            x => x.GetIDFCurveForEventTypeAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetIDFCurvesForEventTypeAsync_WhenCacheIsEmptyAndRepositoryReturnsData_ShouldReturnIdfCurveAndCacheIt()
    {
        var eventTypeCode = "RAIN";
        int? returnPeriodYears = 10;
        var eventType = new EventType(Guid.NewGuid(), eventTypeCode, "Rain Event", "Rain description", true);
        var idfCurve = new IDFCurve(
            Guid.NewGuid(),
            eventType.Id,
            60,
            25.5,
            returnPeriodYears.Value,
            1)
        {
            EventType = eventType
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<IDFCurvesDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDFCurvesDTO?)null);

        _idfCurveRepositoryMock
            .Setup(x => x.GetIDFCurveForEventTypeAsync(
                eventTypeCode,
                returnPeriodYears,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(idfCurve);

        var result = await _riskModelService.GetIDFCurvesForEventTypeAsync(
            eventTypeCode,
            returnPeriodYears);

        result.Should().NotBeNull();
        result!.DurationMinutes.Should().Be(60);
        result.Intensity.Should().Be(25.5);
        result.ReturnPeriodYears.Should().Be(returnPeriodYears.Value);
        result.Version.Should().Be(1);

        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<IDFCurvesDTO>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetIDFCurvesForEventTypeAsync_WhenIdfCurveNotFound_ShouldReturnNull()
    {
        var eventTypeCode = "RAIN";
        int? returnPeriodYears = 10;

        _cacheServiceMock
            .Setup(x => x.GetAsync<IDFCurvesDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDFCurvesDTO?)null);

        _idfCurveRepositoryMock
            .Setup(x => x.GetIDFCurveForEventTypeAsync(
                eventTypeCode,
                returnPeriodYears,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDFCurve?)null);

        var result = await _riskModelService.GetIDFCurvesForEventTypeAsync(
            eventTypeCode,
            returnPeriodYears);

        result.Should().BeNull();
    }

    #endregion

    #region GetRegionalRiskParametersAsync Tests

    [Fact]
    public async Task GetRegionalRiskParametersAsync_WhenCacheHasValue_ShouldReturnCachedResult()
    {
        var regionId = Guid.NewGuid();
        var cachedParameter = new RegionalRiskParametersDTO
        {
            RegionId = regionId,
            AdjustmentFactor = 1.5,
            Description = "Test Region"
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<RegionalRiskParametersDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedParameter);

        var result = await _riskModelService.GetRegionalRiskParametersAsync(regionId);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(cachedParameter);
        _regionalParameterRepositoryMock.Verify(
            x => x.GetRegionByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetRegionalRiskParametersAsync_WhenCacheIsEmptyAndRepositoryReturnsData_ShouldReturnParameterAndCacheIt()
    {
        var regionId = Guid.NewGuid();
        var regionalParameter = new RegionalParameter(regionId, 1.5, "Test Region");

        _cacheServiceMock
            .Setup(x => x.GetAsync<RegionalRiskParametersDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegionalRiskParametersDTO?)null);

        _regionalParameterRepositoryMock
            .Setup(x => x.GetRegionByIdAsync(
                regionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(regionalParameter);

        var result = await _riskModelService.GetRegionalRiskParametersAsync(regionId);

        result.Should().NotBeNull();
        result!.RegionId.Should().Be(regionId);
        result.AdjustmentFactor.Should().Be(1.5);
        result.Description.Should().Be("Test Region");

        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<RegionalRiskParametersDTO>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRegionalRiskParametersAsync_WhenRegionalParameterNotFound_ShouldReturnNull()
    {
        var regionId = Guid.NewGuid();

        _cacheServiceMock
            .Setup(x => x.GetAsync<RegionalRiskParametersDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegionalRiskParametersDTO?)null);

        _regionalParameterRepositoryMock
            .Setup(x => x.GetRegionByIdAsync(
                regionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegionalParameter?)null);

        var result = await _riskModelService.GetRegionalRiskParametersAsync(regionId);

        result.Should().BeNull();
    }

    #endregion

    #region CreateRiskMatrixAsync Tests

    [Fact]
    public async Task CreateRiskMatrixAsync_WhenValidData_ShouldCreateRiskMatrix()
    {
        var riskMatrixDto = new RiskMatrixDTO
        {
            EventTypeCode = "RAIN",
            SeverityLevel = "High",
            RiskLevel = "Critical",
            Version = 1
        };

        var eventType = new EventType(Guid.NewGuid(), riskMatrixDto.EventTypeCode, "Rain Event", "Description", true);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                riskMatrixDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _riskMatrixRepositoryMock
            .Setup(x => x.GetRiskMatrixForEventTypeAsync(
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskMatrix?)null);

        _riskMatrixRepositoryMock
            .Setup(x => x.AddRiskMatrixAsync(
                It.IsAny<RiskMatrix>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _riskModelService.CreateRiskMatrixAsync(riskMatrixDto);

        result.Should().BeTrue();
        _riskMatrixRepositoryMock.Verify(
            x => x.AddRiskMatrixAsync(
                It.Is<RiskMatrix>(rm => 
                    rm.EventTypeId == eventType.Id &&
                    rm.SeverityLevel == SeverityLevelEnum.High &&
                    rm.RiskLevel == RiskLevelEnum.Critical &&
                    rm.Version == riskMatrixDto.Version),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRiskMatrixAsync_WhenEventTypeDoesNotExist_ShouldThrowArgumentException()
    {
        var riskMatrixDto = new RiskMatrixDTO
        {
            EventTypeCode = "INVALID",
            SeverityLevel = "High",
            RiskLevel = "Critical",
            Version = 1
        };

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                riskMatrixDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventType?)null);

        var act = async () => await _riskModelService.CreateRiskMatrixAsync(riskMatrixDto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Event type does not exist.");

        _riskMatrixRepositoryMock.Verify(
            x => x.AddRiskMatrixAsync(
                It.IsAny<RiskMatrix>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateRiskMatrixAsync_WhenRiskMatrixAlreadyExists_ShouldThrowArgumentException()
    {
        var riskMatrixDto = new RiskMatrixDTO
        {
            EventTypeCode = "RAIN",
            SeverityLevel = "High",
            RiskLevel = "Critical",
            Version = 1
        };

        var eventType = new EventType(Guid.NewGuid(), riskMatrixDto.EventTypeCode, "Rain Event", "Description", true);
        var existingRiskMatrix = new RiskMatrix(
            Guid.NewGuid(),
            eventType.Id,
            SeverityLevelEnum.High,
            RiskLevelEnum.Critical,
            riskMatrixDto.Version);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                riskMatrixDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _riskMatrixRepositoryMock
            .Setup(x => x.GetRiskMatrixForEventTypeAsync(
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRiskMatrix);

        var act = async () => await _riskModelService.CreateRiskMatrixAsync(riskMatrixDto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Risk matrix already exists for the given event type, severity level, and version.");

        _riskMatrixRepositoryMock.Verify(
            x => x.AddRiskMatrixAsync(
                It.IsAny<RiskMatrix>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateRiskMatrixAsync_WhenRepositoryReturnsFalse_ShouldReturnFalse()
    {
        var riskMatrixDto = new RiskMatrixDTO
        {
            EventTypeCode = "RAIN",
            SeverityLevel = "High",
            RiskLevel = "Critical",
            Version = 1
        };

        var eventType = new EventType(Guid.NewGuid(), riskMatrixDto.EventTypeCode, "Rain Event", "Description", true);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                riskMatrixDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _riskMatrixRepositoryMock
            .Setup(x => x.GetRiskMatrixForEventTypeAsync(
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskMatrix?)null);

        _riskMatrixRepositoryMock
            .Setup(x => x.AddRiskMatrixAsync(
                It.IsAny<RiskMatrix>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _riskModelService.CreateRiskMatrixAsync(riskMatrixDto);

        result.Should().BeFalse();
    }

    #endregion

    #region CreateIDFCurvesAsync Tests

    [Fact]
    public async Task CreateIDFCurvesAsync_WhenValidData_ShouldCreateIdfCurve()
    {
        var idfCurvesDto = new CreateIDFCurvesDTO
        {
            EventTypeCode = "RAIN",
            DurationMinutes = 60,
            Intensity = 25.5,
            ReturnPeriodYears = 10,
            Version = 1
        };

        var eventType = new EventType(Guid.NewGuid(), idfCurvesDto.EventTypeCode, "Rain Event", "Description", true);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                idfCurvesDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _idfCurveRepositoryMock
            .Setup(x => x.GetIDFCurveForDurationAndPeriod(
                idfCurvesDto.EventTypeCode,
                idfCurvesDto.DurationMinutes,
                idfCurvesDto.ReturnPeriodYears,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IDFCurve?)null);

        _idfCurveRepositoryMock
            .Setup(x => x.AddIDFCurveAsync(
                It.IsAny<IDFCurve>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _riskModelService.CreateIDFCurvesAsync(idfCurvesDto);

        result.Should().BeTrue();
        _idfCurveRepositoryMock.Verify(
            x => x.AddIDFCurveAsync(
                It.Is<IDFCurve>(curve =>
                    curve.EventTypeId == eventType.Id &&
                    curve.DurationMinutes == idfCurvesDto.DurationMinutes &&
                    curve.Intensity == idfCurvesDto.Intensity &&
                    curve.ReturnPeriodYears == idfCurvesDto.ReturnPeriodYears &&
                    curve.Version == idfCurvesDto.Version),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateIDFCurvesAsync_WhenEventTypeDoesNotExist_ShouldThrowArgumentException()
    {
        var idfCurvesDto = new CreateIDFCurvesDTO
        {
            EventTypeCode = "INVALID",
            DurationMinutes = 60,
            Intensity = 25.5,
            ReturnPeriodYears = 10,
            Version = 1
        };

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                idfCurvesDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventType?)null);

        var act = async () => await _riskModelService.CreateIDFCurvesAsync(idfCurvesDto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Event type does not exist.");

        _idfCurveRepositoryMock.Verify(
            x => x.AddIDFCurveAsync(
                It.IsAny<IDFCurve>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateIDFCurvesAsync_WhenIdfCurveAlreadyExists_ShouldThrowArgumentException()
    {
        var idfCurvesDto = new CreateIDFCurvesDTO
        {
            EventTypeCode = "RAIN",
            DurationMinutes = 60,
            Intensity = 25.5,
            ReturnPeriodYears = 10,
            Version = 1
        };

        var eventType = new EventType(Guid.NewGuid(), idfCurvesDto.EventTypeCode, "Rain Event", "Description", true);
        var existingIdfCurve = new IDFCurve(
            Guid.NewGuid(),
            eventType.Id,
            idfCurvesDto.DurationMinutes,
            idfCurvesDto.Intensity,
            idfCurvesDto.ReturnPeriodYears,
            idfCurvesDto.Version);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                idfCurvesDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _idfCurveRepositoryMock
            .Setup(x => x.GetIDFCurveForDurationAndPeriod(
                idfCurvesDto.EventTypeCode,
                idfCurvesDto.DurationMinutes,
                idfCurvesDto.ReturnPeriodYears,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingIdfCurve);

        var act = async () => await _riskModelService.CreateIDFCurvesAsync(idfCurvesDto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("IDF Curve already exists for the given event type, duration minutes and return period years.");

        _idfCurveRepositoryMock.Verify(
            x => x.AddIDFCurveAsync(
                It.IsAny<IDFCurve>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region CreateRegionalRiskParametersAsync Tests

    [Fact]
    public async Task CreateRegionalRiskParametersAsync_WhenValidData_ShouldCreateRegionalParameter()
    {
        var regionalParameterDto = new CreateRegionalRiskParameterDTO
        {
            AdjustmentFactor = 1.5,
            Description = "Test Region"
        };

        var createdRegionId = Guid.NewGuid();

        _regionalParameterRepositoryMock
            .SetupSequence(x => x.GetByAdjustmentFactorAsync(
                regionalParameterDto.AdjustmentFactor,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((RegionalParameter?)null)
            .ReturnsAsync(new RegionalParameter(createdRegionId, regionalParameterDto.AdjustmentFactor, regionalParameterDto.Description));

        _regionalParameterRepositoryMock
            .Setup(x => x.AddRegionalParameterAsync(
                It.IsAny<RegionalParameter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _riskModelService.CreateRegionalRiskParametersAsync(regionalParameterDto);

        result.Should().BeTrue();
        _regionalParameterRepositoryMock.Verify(
            x => x.AddRegionalParameterAsync(
                It.Is<RegionalParameter>(rp =>
                    rp.AdjustmentFactor == regionalParameterDto.AdjustmentFactor &&
                    rp.Description == regionalParameterDto.Description),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateRegionalRiskParametersAsync_WhenRegionalParameterAlreadyExists_ShouldThrowArgumentException()
    {
        var regionalParameterDto = new CreateRegionalRiskParameterDTO
        {
            AdjustmentFactor = 1.5,
            Description = "Test Region"
        };

        var existingParameter = new RegionalParameter(
            Guid.NewGuid(),
            regionalParameterDto.AdjustmentFactor,
            regionalParameterDto.Description);

        _regionalParameterRepositoryMock
            .Setup(x => x.GetByAdjustmentFactorAsync(
                regionalParameterDto.AdjustmentFactor,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingParameter);

        var act = async () => await _riskModelService.CreateRegionalRiskParametersAsync(regionalParameterDto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Regional risk parameters already exist for the given region.");

        _regionalParameterRepositoryMock.Verify(
            x => x.AddRegionalParameterAsync(
                It.IsAny<RegionalParameter>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region PublishCatalogVersionAsync Tests

    [Fact]
    public async Task PublishCatalogVersionAsync_WhenValidData_ShouldPublishCatalogAndInvalidateCache()
    {
        var catalogPublishDto = new CatalogPublishDTO
        {
            Version = 1,
            Notes = "Test publish"
        };
        var tenantId = Guid.NewGuid();

        _riskMatrixRepositoryMock
            .Setup(x => x.MarkVersionAsActiveAsync(
                catalogPublishDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _idfCurveRepositoryMock
            .Setup(x => x.MarkVersionAsActiveAsync(
                catalogPublishDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _publisherMock
            .Setup(x => x.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _riskModelService.PublishCatalogVersionAsync(catalogPublishDto, tenantId);

        result.Should().BeTrue();

        _publisherMock.Verify(
            x => x.PublishAsync(
                It.IsAny<string>(),
                It.Is<string>(payload => payload.Contains(catalogPublishDto.Version.ToString())),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task PublishCatalogVersionAsync_WhenNoDataFoundForVersion_ShouldThrowArgumentException()
    {
        var catalogPublishDto = new CatalogPublishDTO
        {
            Version = 999,
            Notes = "Test publish"
        };
        var tenantId = Guid.NewGuid();

        _riskMatrixRepositoryMock
            .Setup(x => x.MarkVersionAsActiveAsync(
                catalogPublishDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _idfCurveRepositoryMock
            .Setup(x => x.MarkVersionAsActiveAsync(
                catalogPublishDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = async () => await _riskModelService.PublishCatalogVersionAsync(catalogPublishDto, tenantId);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"No data found for version {catalogPublishDto.Version}");

        _publisherMock.Verify(
            x => x.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishCatalogVersionAsync_WhenAtLeastOneRepositoryHasData_ShouldPublishCatalog()
    {
        var catalogPublishDto = new CatalogPublishDTO
        {
            Version = 1,
            Notes = "Test publish"
        };
        var tenantId = Guid.NewGuid();

        _riskMatrixRepositoryMock
            .Setup(x => x.MarkVersionAsActiveAsync(
                catalogPublishDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _idfCurveRepositoryMock
            .Setup(x => x.MarkVersionAsActiveAsync(
                catalogPublishDto.Version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _publisherMock
            .Setup(x => x.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _riskModelService.PublishCatalogVersionAsync(catalogPublishDto, tenantId);

        result.Should().BeTrue();
        _publisherMock.Verify(
            x => x.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}

