using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.Application.Services;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.EventTypes;
using RiskCatalog.Domain.IRepositories;

namespace RiskCatalog.UnitTests;

public class EventTypeServiceTests
{
    private readonly Mock<IEventTypeRepository> _eventTypeRepositoryMock;
    private readonly Mock<ISeverityRepository> _severityRepositoryMock;
    private readonly Mock<ICacheService> _cacheServiceMock;
    private readonly Mock<ILogger<EventTypeService>> _loggerMock;
    private readonly EventTypeService _eventTypeService;

    public EventTypeServiceTests()
    {
        _eventTypeRepositoryMock = new Mock<IEventTypeRepository>();
        _severityRepositoryMock = new Mock<ISeverityRepository>();
        _cacheServiceMock = new Mock<ICacheService>();
        _loggerMock = new Mock<ILogger<EventTypeService>>();

        _eventTypeService = new EventTypeService(
            _eventTypeRepositoryMock.Object,
            _severityRepositoryMock.Object,
            _cacheServiceMock.Object,
            _loggerMock.Object);
    }

    #region GetEventTypesAsync Tests

    [Fact]
    public async Task GetEventTypesAsync_WhenCacheHasValue_ShouldReturnCachedResult()
    {
        bool? isActive = true;
        var cachedEventTypes = new List<EventTypeDTO>
        {
            new() { Id = Guid.NewGuid(), Code = "RAIN", Name = "Rain Event" },
            new() { Id = Guid.NewGuid(), Code = "FLOOD", Name = "Flood Event" }
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<List<EventTypeDTO>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEventTypes);

        var result = await _eventTypeService.GetEventTypesAsync(isActive);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(cachedEventTypes);
        result.Should().HaveCount(2);
        _eventTypeRepositoryMock.Verify(
            x => x.GetEventTypes(
                It.IsAny<bool?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEventTypesAsync_WhenCacheIsEmptyAndRepositoryReturnsData_ShouldReturnEventTypesAndCacheThem()
    {
        bool? isActive = true;
        var eventTypes = new List<EventType>
        {
            new(Guid.NewGuid(), "RAIN", "Rain Event", "Rain description", true),
            new(Guid.NewGuid(), "FLOOD", "Flood Event", "Flood description", true)
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<List<EventTypeDTO>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<EventTypeDTO>?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypes(
                isActive,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventTypes);

        var result = await _eventTypeService.GetEventTypesAsync(isActive);

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Code.Should().Be("RAIN");
        result[1].Code.Should().Be("FLOOD");

        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<List<EventTypeDTO>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventTypesAsync_WhenIsActiveIsNull_ShouldReturnAllEventTypes()
    {
        bool? isActive = null;
        var eventTypes = new List<EventType>
        {
            new(Guid.NewGuid(), "RAIN", "Rain Event", "Rain description", true),
            new(Guid.NewGuid(), "FLOOD", "Flood Event", "Flood description", false)
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<List<EventTypeDTO>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<EventTypeDTO>?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypes(
                isActive,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventTypes);

        var result = await _eventTypeService.GetEventTypesAsync(isActive);

        result.Should().NotBeNull();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetEventTypesAsync_WhenRepositoryReturnsEmptyList_ShouldReturnEmptyList()
    {
        bool? isActive = true;

        _cacheServiceMock
            .Setup(x => x.GetAsync<List<EventTypeDTO>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<EventTypeDTO>?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypes(
                isActive,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EventType>());

        var result = await _eventTypeService.GetEventTypesAsync(isActive);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region GetEventTypeByCodeAsync Tests

    [Fact]
    public async Task GetEventTypeByCodeAsync_WhenCacheHasValue_ShouldReturnCachedResult()
    {
        var eventTypeCode = "RAIN";
        var cachedEventType = new EventTypeDTO
        {
            Id = Guid.NewGuid(),
            Code = eventTypeCode,
            Name = "Rain Event"
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<EventTypeDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEventType);

        var result = await _eventTypeService.GetEventTypeByCodeAsync(eventTypeCode);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(cachedEventType);
        _eventTypeRepositoryMock.Verify(
            x => x.GetEventTypeByCode(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetEventTypeByCodeAsync_WhenCacheIsEmptyAndRepositoryReturnsData_ShouldReturnEventTypeAndCacheIt()
    {
        var eventTypeCode = "RAIN";
        var eventType = new EventType(Guid.NewGuid(), eventTypeCode, "Rain Event", "Rain description", true);

        _cacheServiceMock
            .Setup(x => x.GetAsync<EventTypeDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventTypeDTO?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                eventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        var result = await _eventTypeService.GetEventTypeByCodeAsync(eventTypeCode);

        result.Should().NotBeNull();
        result!.Code.Should().Be(eventTypeCode);
        result.Name.Should().Be("Rain Event");
        result.Id.Should().Be(eventType.Id);

        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<EventTypeDTO>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventTypeByCodeAsync_WhenEventTypeNotFound_ShouldReturnNull()
    {
        var eventTypeCode = "INVALID";

        _cacheServiceMock
            .Setup(x => x.GetAsync<EventTypeDTO>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventTypeDTO?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                eventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventType?)null);

        var result = await _eventTypeService.GetEventTypeByCodeAsync(eventTypeCode);

        result.Should().BeNull();
        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<EventTypeDTO>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetSeverityCriterionForEventTypeAsync Tests

    [Fact]
    public async Task GetSeverityCriterionForEventTypeAsync_WhenCacheHasValue_ShouldReturnCachedResult()
    {
        var eventTypeCode = "RAIN";
        int? version = 1;
        var cachedCriteria = new List<SeverityCriterionDTO>
        {
            new()
            {
                EventTypeCode = eventTypeCode,
                SeverityLevel = "High",
                MinValue = 50.0,
                MaxValue = 100.0,
                Unit = "mm",
                Version = version.Value
            }
        };

        _cacheServiceMock
            .Setup(x => x.GetAsync<List<SeverityCriterionDTO>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedCriteria);

        var result = await _eventTypeService.GetSeverityCriterionForEventTypeAsync(eventTypeCode, version);

        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(cachedCriteria);
        _eventTypeRepositoryMock.Verify(
            x => x.GetSeveritiesCriterionForEventType(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetSeverityCriterionForEventTypeAsync_WhenCacheIsEmptyAndRepositoryReturnsData_ShouldReturnCriteriaAndCacheThem()
    {
        var eventTypeCode = "RAIN";
        int? version = 1;
        var eventType = new EventType(Guid.NewGuid(), eventTypeCode, "Rain Event", "Rain description", true);
        var severity = new Severity(Guid.NewGuid(), SeverityLevelEnum.High, "High severity");
        var severityCriterion = new SeverityCriterion(
            Guid.NewGuid(),
            eventType.Id,
            severity.Id,
            50.0,
            100,
            "mm",
            version.Value)
        {
            Severity = severity
        };

        var criteria = new List<SeverityCriterion> { severityCriterion };

        _cacheServiceMock
            .Setup(x => x.GetAsync<List<SeverityCriterionDTO>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<SeverityCriterionDTO>?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.GetSeveritiesCriterionForEventType(
                eventTypeCode,
                version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(criteria);

        var result = await _eventTypeService.GetSeverityCriterionForEventTypeAsync(eventTypeCode, version);

        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].EventTypeCode.Should().Be(eventTypeCode);
        result[0].SeverityLevel.Should().Be("High");
        result[0].MinValue.Should().Be(50.0);
        result[0].MaxValue.Should().Be(100.0);
        result[0].Version.Should().Be(version.Value);

        _cacheServiceMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<List<SeverityCriterionDTO>>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSeverityCriterionForEventTypeAsync_WhenVersionIsNull_ShouldHandleNullVersion()
    {
        var eventTypeCode = "RAIN";
        int? version = null;
        var criteria = new List<SeverityCriterion>();

        _cacheServiceMock
            .Setup(x => x.GetAsync<List<SeverityCriterionDTO>>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<SeverityCriterionDTO>?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.GetSeveritiesCriterionForEventType(
                eventTypeCode,
                version,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(criteria);

        var result = await _eventTypeService.GetSeverityCriterionForEventTypeAsync(eventTypeCode, version);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region CreateEventTypeAsync Tests

    [Fact]
    public async Task CreateEventTypeAsync_WhenValidData_ShouldCreateEventType()
    {
        var createEventTypeDto = new CreateEventTypeDTO
        {
            Code = "RAIN",
            Name = "Rain Event",
            Description = "Rain description",
            IsActive = true
        };

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                createEventTypeDto.Code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventType?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.CreateEventType(
                It.IsAny<EventType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _eventTypeService.CreateEventTypeAsync(createEventTypeDto);

        result.Should().BeTrue();
        _eventTypeRepositoryMock.Verify(
            x => x.CreateEventType(
                It.Is<EventType>(et =>
                    et.Code == createEventTypeDto.Code &&
                    et.Name == createEventTypeDto.Name &&
                    et.Description == createEventTypeDto.Description &&
                    et.IsActive == createEventTypeDto.IsActive),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CreateEventTypeAsync_WhenEventTypeAlreadyExists_ShouldThrowArgumentException()
    {
        var createEventTypeDto = new CreateEventTypeDTO
        {
            Code = "RAIN",
            Name = "Rain Event",
            Description = "Rain description",
            IsActive = true
        };

        var existingEventType = new EventType(
            Guid.NewGuid(),
            createEventTypeDto.Code,
            "Existing Event",
            "Description",
            true);

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                createEventTypeDto.Code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEventType);

        var act = async () => await _eventTypeService.CreateEventTypeAsync(createEventTypeDto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Event type with the same code already exists.");

        _eventTypeRepositoryMock.Verify(
            x => x.CreateEventType(
                It.IsAny<EventType>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateEventTypeAsync_WhenRepositoryReturnsFalse_ShouldReturnFalse()
    {
        var createEventTypeDto = new CreateEventTypeDTO
        {
            Code = "RAIN",
            Name = "Rain Event",
            Description = "Rain description",
            IsActive = true
        };

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                createEventTypeDto.Code,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventType?)null);

        _eventTypeRepositoryMock
            .Setup(x => x.CreateEventType(
                It.IsAny<EventType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _eventTypeService.CreateEventTypeAsync(createEventTypeDto);

        result.Should().BeFalse();
    }

    #endregion

    #region CreateSeverity Tests

    [Fact]
    public async Task CreateSeverity_WhenValidData_ShouldCreateSeverity()
    {
        var severityDto = new SeverityDTO
        {
            Level = "High",
            Description = "High severity"
        };

        _severityRepositoryMock
            .Setup(x => x.GetByLevel(
                SeverityLevelEnum.High,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Severity?)null);

        _severityRepositoryMock
            .Setup(x => x.CreateSeverity(
                It.IsAny<Severity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _eventTypeService.CreateSeverity(severityDto);

        result.Should().BeTrue();
        _severityRepositoryMock.Verify(
            x => x.CreateSeverity(
                It.Is<Severity>(s =>
                    s.Level == SeverityLevelEnum.High &&
                    s.Description == severityDto.Description),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSeverity_WhenSeverityAlreadyExists_ShouldThrowArgumentException()
    {
        var severityDto = new SeverityDTO
        {
            Level = "High",
            Description = "High severity"
        };

        var existingSeverity = new Severity(
            Guid.NewGuid(),
            SeverityLevelEnum.High,
            "Existing High severity");

        _severityRepositoryMock
            .Setup(x => x.GetByLevel(
                SeverityLevelEnum.High,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSeverity);

        var act = async () => await _eventTypeService.CreateSeverity(severityDto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Severity with the same level already exists.");

        _severityRepositoryMock.Verify(
            x => x.CreateSeverity(
                It.IsAny<Severity>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateSeverity_WhenRepositoryReturnsFalse_ShouldReturnFalse()
    {
        var severityDto = new SeverityDTO
        {
            Level = "High",
            Description = "High severity"
        };

        _severityRepositoryMock
            .Setup(x => x.GetByLevel(
                SeverityLevelEnum.High,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Severity?)null);

        _severityRepositoryMock
            .Setup(x => x.CreateSeverity(
                It.IsAny<Severity>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _eventTypeService.CreateSeverity(severityDto);

        result.Should().BeFalse();
    }

    #endregion

    #region UpdateEventTypeStatusAsync Tests

    [Fact]
    public async Task UpdateEventTypeStatusAsync_WhenValidData_ShouldUpdateStatus()
    {
        var eventTypeId = Guid.NewGuid();
        var isActive = true;

        _eventTypeRepositoryMock
            .Setup(x => x.UpdateEventTypeStatus(
                eventTypeId,
                isActive,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _eventTypeService.UpdateEventTypeStatusAsync(eventTypeId, isActive);

        result.Should().BeTrue();
        _eventTypeRepositoryMock.Verify(
            x => x.UpdateEventTypeStatus(
                eventTypeId,
                isActive,
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateEventTypeStatusAsync_WhenEventTypeNotFound_ShouldReturnFalse()
    {
        var eventTypeId = Guid.NewGuid();
        var isActive = true;

        _eventTypeRepositoryMock
            .Setup(x => x.UpdateEventTypeStatus(
                eventTypeId,
                isActive,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _eventTypeService.UpdateEventTypeStatusAsync(eventTypeId, isActive);

        result.Should().BeFalse();
        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateEventTypeStatusAsync_WhenDeactivatingEventType_ShouldUpdateToInactive()
    {
        var eventTypeId = Guid.NewGuid();
        var isActive = false;

        _eventTypeRepositoryMock
            .Setup(x => x.UpdateEventTypeStatus(
                eventTypeId,
                isActive,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _eventTypeService.UpdateEventTypeStatusAsync(eventTypeId, isActive);

        result.Should().BeTrue();
        _eventTypeRepositoryMock.Verify(
            x => x.UpdateEventTypeStatus(
                eventTypeId,
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region CreateSeverityCriterionToEventType Tests

    [Fact]
    public async Task CreateSeverityCriterionToEventType_WhenValidData_ShouldAddCriterion()
    {
        var severityCriterionDto = new SeverityCriterionDTO
        {
            EventTypeCode = "RAIN",
            SeverityLevel = "High",
            MinValue = 50.0,
            MaxValue = 100.0,
            Unit = "mm",
            Version = 1
        };

        var eventType = new EventType(
            Guid.NewGuid(),
            severityCriterionDto.EventTypeCode,
            "Rain Event",
            "Description",
            true)
        {
            SeverityCriteria = new List<SeverityCriterion>()
        };

        var severity = new Severity(
            Guid.NewGuid(),
            SeverityLevelEnum.High,
            "High severity");

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                severityCriterionDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _severityRepositoryMock
            .Setup(x => x.GetByLevel(
                SeverityLevelEnum.High,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(severity);

        _eventTypeRepositoryMock
            .Setup(x => x.AddSeverityCriterionToEventType(
                It.IsAny<SeverityCriterion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _eventTypeService.CreateSeverityCriterionToEventType(severityCriterionDto);

        result.Should().BeTrue();
        _eventTypeRepositoryMock.Verify(
            x => x.AddSeverityCriterionToEventType(
                It.Is<SeverityCriterion>(sc =>
                    sc.EventTypeId == eventType.Id &&
                    sc.SeverityId == severity.Id &&
                    sc.MinValue == severityCriterionDto.MinValue &&
                    sc.MaxValue == severityCriterionDto.MaxValue &&
                    sc.Unit == severityCriterionDto.Unit &&
                    sc.Version == severityCriterionDto.Version),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheServiceMock.Verify(
            x => x.RemoveByPatternAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSeverityCriterionToEventType_WhenEventTypeNotFound_ShouldThrowKeyNotFoundException()
    {
        var severityCriterionDto = new SeverityCriterionDTO
        {
            EventTypeCode = "INVALID",
            SeverityLevel = "High",
            MinValue = 50.0,
            MaxValue = 100.0,
            Unit = "mm",
            Version = 1
        };

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                severityCriterionDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventType?)null);

        var act = async () => await _eventTypeService.CreateSeverityCriterionToEventType(severityCriterionDto);

        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("Event type not found.");

        _eventTypeRepositoryMock.Verify(
            x => x.AddSeverityCriterionToEventType(
                It.IsAny<SeverityCriterion>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateSeverityCriterionToEventType_WhenSeverityLevelNotFound_ShouldThrowArgumentException()
    {
        var severityCriterionDto = new SeverityCriterionDTO
        {
            EventTypeCode = "RAIN",
            SeverityLevel = "Invalid",
            MinValue = 50.0,
            MaxValue = 100.0,
            Unit = "mm",
            Version = 1
        };

        var eventType = new EventType(
            Guid.NewGuid(),
            severityCriterionDto.EventTypeCode,
            "Rain Event",
            "Description",
            true)
        {
            SeverityCriteria = new List<SeverityCriterion>()
        };

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                severityCriterionDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        var act = async () => await _eventTypeService.CreateSeverityCriterionToEventType(severityCriterionDto);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateSeverityCriterionToEventType_WhenSeverityNotFound_ShouldThrowArgumentException()
    {
        var severityCriterionDto = new SeverityCriterionDTO
        {
            EventTypeCode = "RAIN",
            SeverityLevel = "High",
            MinValue = 50.0,
            MaxValue = 100.0,
            Unit = "mm",
            Version = 1
        };

        var eventType = new EventType(
            Guid.NewGuid(),
            severityCriterionDto.EventTypeCode,
            "Rain Event",
            "Description",
            true)
        {
            SeverityCriteria = new List<SeverityCriterion>()
        };

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                severityCriterionDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _severityRepositoryMock
            .Setup(x => x.GetByLevel(
                SeverityLevelEnum.High,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Severity?)null);

        var act = async () => await _eventTypeService.CreateSeverityCriterionToEventType(severityCriterionDto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Severity level not found.");
    }

    [Fact]
    public async Task CreateSeverityCriterionToEventType_WhenRangeOverlaps_ShouldThrowBadHttpRequestException()
    {
        var severityCriterionDto = new SeverityCriterionDTO
        {
            EventTypeCode = "RAIN",
            SeverityLevel = "High",
            MinValue = 50.0,
            MaxValue = 100.0,
            Unit = "mm",
            Version = 1
        };

        var eventTypeId = Guid.NewGuid();
        var existingSeverityId = Guid.NewGuid();
        var existingCriterion = new SeverityCriterion(
            Guid.NewGuid(),
            eventTypeId,
            existingSeverityId,
            60.0,
            90,
            "mm",
            1);

        var eventType = new EventType(
            eventTypeId,
            severityCriterionDto.EventTypeCode,
            "Rain Event",
            "Description",
            true)
        {
            SeverityCriteria = new List<SeverityCriterion> { existingCriterion }
        };

        var severity = new Severity(
            Guid.NewGuid(),
            SeverityLevelEnum.High,
            "High severity");

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                severityCriterionDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _severityRepositoryMock
            .Setup(x => x.GetByLevel(
                SeverityLevelEnum.High,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(severity);

        var act = async () => await _eventTypeService.CreateSeverityCriterionToEventType(severityCriterionDto);

        await act.Should().ThrowAsync<BadHttpRequestException>()
            .WithMessage("Severity criterion range overlaps with an existing criterion.");

        _eventTypeRepositoryMock.Verify(
            x => x.AddSeverityCriterionToEventType(
                It.IsAny<SeverityCriterion>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateSeverityCriterionToEventType_WhenRepositoryReturnsFalse_ShouldReturnFalse()
    {
        var severityCriterionDto = new SeverityCriterionDTO
        {
            EventTypeCode = "RAIN",
            SeverityLevel = "High",
            MinValue = 150.0,
            MaxValue = 200.0,
            Unit = "mm",
            Version = 1
        };

        var eventType = new EventType(
            Guid.NewGuid(),
            severityCriterionDto.EventTypeCode,
            "Rain Event",
            "Description",
            true)
        {
            SeverityCriteria = new List<SeverityCriterion>()
        };

        var severity = new Severity(
            Guid.NewGuid(),
            SeverityLevelEnum.High,
            "High severity");

        _eventTypeRepositoryMock
            .Setup(x => x.GetEventTypeByCode(
                severityCriterionDto.EventTypeCode,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventType);

        _severityRepositoryMock
            .Setup(x => x.GetByLevel(
                SeverityLevelEnum.High,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(severity);

        _eventTypeRepositoryMock
            .Setup(x => x.AddSeverityCriterionToEventType(
                It.IsAny<SeverityCriterion>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _eventTypeService.CreateSeverityCriterionToEventType(severityCriterionDto);

        result.Should().BeFalse();
    }

    #endregion
}

