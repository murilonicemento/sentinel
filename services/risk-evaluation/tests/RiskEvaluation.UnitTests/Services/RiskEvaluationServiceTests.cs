using Microsoft.Extensions.Logging;
using Moq;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Application.Services;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.Events;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Application.Interfaces.Messaging;
using RiskEvaluation.Application.Interfaces.Services;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.UnitTests.Services;

public class RiskEvaluationServiceTests
{
    private const int ScorePrecision = 6;

    private readonly Mock<IRiskEvaluationRepository> _repositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IRecentScoresCache> _cacheMock;
    private readonly Mock<ICatalogSnapshotRepository> _catalogSnapshotsMock = new();
    private readonly Mock<ILogger<RiskEvaluationService>> _loggerMock;
    private readonly RiskEvaluationService _service;

    public RiskEvaluationServiceTests()
    {
        _repositoryMock = new Mock<IRiskEvaluationRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _cacheMock = new Mock<IRecentScoresCache>();
        _loggerMock = new Mock<ILogger<RiskEvaluationService>>();

        // RiskCalculationService is a pure domain service; without a repository it uses the default weights:
        // metrics TemperatureAnomaly 0.15, HumidityAnomaly 0.10, WindGust 0.15, Rainfall 0.12, PressureChange 0.08
        // and events Wildfire 0.10, Earthquake 0.12, Flood 0.10, Landslide 0.08.
        _service = new RiskEvaluationService(
            _repositoryMock.Object,
            _eventPublisherMock.Object,
            new RiskCalculationService(),
            _cacheMock.Object,
            _catalogSnapshotsMock.Object,
            _loggerMock.Object);
    }

    private void SetupNoCacheAndNoGeospatialContext(int latitude, int longitude)
    {
        _cacheMock.Setup(x => x.GetAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskScoreCacheEntry?)null);
    }

    private static RiskMetrics MaxMetrics() => new()
    {
        TemperatureAnomaly = 10,
        HumidityAnomaly = 50,
        WindGust = 100,
        Rainfall = 50,
        PressureChange = 20
    };

    private static EventDetails DetectedAtMaxIntensity() => new() { Detected = true, Intensity = 10 };

    [Theory]
    [InlineData(20, 0, 0, 0.03)]
    [InlineData(40, 25, 0, 0.12)]
    public async Task EvaluateRiskAsync_LowRisk_ShouldSaveAndPublishEvent(
        double windGust, double rainfall, double temperatureAnomaly, double expectedScore)
    {
        // Arrange
        const int latitude = -23;
        const int longitude = -46;
        var metrics = new RiskMetrics
        { WindGust = windGust, Rainfall = rainfall, TemperatureAnomaly = temperatureAnomaly };
        var events = new RiskEvents();
        SetupNoCacheAndNoGeospatialContext(latitude, longitude);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(latitude, result.Latitude);
        Assert.Equal(longitude, result.Longitude);
        Assert.Equal(expectedScore, result.Score, ScorePrecision);
        Assert.Equal(RiskLevel.Low, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.Is<RiskEvaluationEntity>(
            e => e.Latitude == latitude && e.Longitude == longitude && e.Score == result.Score &&
                 e.Level == RiskLevel.Low)), Times.Once);

        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<RiskEvaluatedEvent>(
            e => e.Latitude == latitude && e.Longitude == longitude && e.Score == result.Score &&
                 e.Level == nameof(RiskLevel.Low))), Times.Once);

        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<HighRiskDetectedEvent>()), Times.Never);
        _cacheMock.Verify(x => x.SetAsync(latitude, longitude, It.IsAny<RiskScoreCacheEntry>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(100, 50, 10, 0.42)]
    [InlineData(100, 50, 5, 0.345)]
    public async Task EvaluateRiskAsync_MediumRisk_ShouldSaveAndPublishEvent(
        double windGust, double rainfall, double temperatureAnomaly, double expectedScore)
    {
        // Arrange
        const int latitude = -23;
        const int longitude = -46;
        var metrics = new RiskMetrics
        { WindGust = windGust, Rainfall = rainfall, TemperatureAnomaly = temperatureAnomaly };
        var events = new RiskEvents();
        SetupNoCacheAndNoGeospatialContext(latitude, longitude);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(latitude, result.Latitude);
        Assert.Equal(longitude, result.Longitude);
        Assert.Equal(expectedScore, result.Score, ScorePrecision);
        Assert.Equal(RiskLevel.Medium, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<RiskEvaluatedEvent>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<HighRiskDetectedEvent>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateRiskAsync_HighRisk_ShouldSaveAndPublishBothEvents()
    {
        // Arrange
        const int latitude = -22;
        const int longitude = -43;
        var metrics = MaxMetrics();
        var events = new RiskEvents { Flood = DetectedAtMaxIntensity() };
        SetupNoCacheAndNoGeospatialContext(latitude, longitude);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(latitude, result.Latitude);
        Assert.Equal(longitude, result.Longitude);
        Assert.Equal(0.70, result.Score, ScorePrecision);
        Assert.Equal(RiskLevel.High, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<RiskEvaluatedEvent>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<HighRiskDetectedEvent>(
            e => e.Latitude == latitude && e.Longitude == longitude && e.Score == result.Score &&
                 e.Level == nameof(RiskLevel.High))), Times.Once);
    }

    public static TheoryData<RiskEvents, double> CriticalRiskEvents => new()
    {
        {
            new RiskEvents
            {
                Wildfire = DetectedAtMaxIntensity(),
                Earthquake = DetectedAtMaxIntensity(),
                Flood = DetectedAtMaxIntensity(),
                Landslide = DetectedAtMaxIntensity()
            },
            1.0
        },
        {
            new RiskEvents
            {
                Wildfire = DetectedAtMaxIntensity(),
                Earthquake = DetectedAtMaxIntensity(),
                Flood = DetectedAtMaxIntensity()
            },
            0.92
        }
    };

    [Theory]
    [MemberData(nameof(CriticalRiskEvents))]
    public async Task EvaluateRiskAsync_CriticalRisk_ShouldSaveAndPublishBothEvents(
        RiskEvents events, double expectedScore)
    {
        // Arrange
        const int latitude = -22;
        const int longitude = -43;
        var metrics = MaxMetrics();
        SetupNoCacheAndNoGeospatialContext(latitude, longitude);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(latitude, result.Latitude);
        Assert.Equal(longitude, result.Longitude);
        Assert.Equal(expectedScore, result.Score, ScorePrecision);
        Assert.Equal(RiskLevel.Critical, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<RiskEvaluatedEvent>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<HighRiskDetectedEvent>(
            e => e.Latitude == latitude && e.Longitude == longitude && e.Score == result.Score &&
                 e.Level == nameof(RiskLevel.Critical))), Times.Once);
    }

    [Fact]
    public async Task EvaluateRiskAsync_ShouldGenerateUniqueId()
    {
        // Arrange
        const int latitude = -23;
        const int longitude = -46;
        var metrics = new RiskMetrics { WindGust = 50, Rainfall = 25, PressureChange = 10 };
        var events = new RiskEvents();
        SetupNoCacheAndNoGeospatialContext(latitude, longitude);

        // Act
        var result1 = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);
        var result2 = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotEqual(result1.Id, result2.Id);
    }

    [Fact]
    public async Task EvaluateRiskAsync_ShouldSetTimestamp()
    {
        // Arrange
        const int latitude = -23;
        const int longitude = -46;
        var metrics = new RiskMetrics { WindGust = 20, Rainfall = 10, PressureChange = 6 };
        var events = new RiskEvents();
        SetupNoCacheAndNoGeospatialContext(latitude, longitude);

        var beforeTest = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        var afterTest = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(result.Timestamp >= beforeTest && result.Timestamp <= afterTest);
    }

    [Fact]
    public async Task EvaluateRiskAsync_WithCachedResult_ShouldReturnCachedEvaluation()
    {
        // Arrange
        const int latitude = -23;
        const int longitude = -46;
        const double cachedScore = 0.45;
        const RiskLevel cachedLevel = RiskLevel.Medium;
        var cachedEntry = new RiskScoreCacheEntry
        {
            Score = cachedScore,
            Level = cachedLevel.ToString(),
            CalculatedAt = DateTime.UtcNow,
            Metrics = new RiskMetrics(),
            Events = new RiskEvents()
        };

        _cacheMock.Setup(x => x.GetAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEntry);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, new RiskMetrics(), new RiskEvents());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(cachedScore, result.Score);
        Assert.Equal(cachedLevel, result.Level);
        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
    }

    // A circle of 50 km around (-22.9, -43.2) with factor 1.2, a polygon around (-23.5, -46.6) with factor 0.5,
    // and Flood intensities 5..10 classified as High severity, which the matrix maps to Critical risk.
    private static RiskCatalogSnapshot Catalog() => new(
        7,
        DateTime.UtcNow,
        [
            new RegionalAdjustment(Guid.NewGuid(), 1.2, -22.9, -43.2, 50, []),
            new RegionalAdjustment(Guid.NewGuid(), 0.5, null, null, null,
                [new GeoCoordinate(-23, -47), new GeoCoordinate(-23, -46), new GeoCoordinate(-24, -46), new GeoCoordinate(-24, -47)])
        ],
        [new SeverityRule("Flood", "High", 5, 10)],
        [new RiskMatrixRule("Flood", "High", RiskLevel.Critical)]);

    [Fact]
    public async Task EvaluateRiskAsync_InsideACatalogRegion_AppliesItsAdjustmentFactor()
    {
        _catalogSnapshotsMock.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog());
        SetupNoCacheAndNoGeospatialContext(-22, -43);

        var result = await _service.EvaluateRiskAsync(-22, -43, MaxMetrics(),
            new RiskEvents { Flood = DetectedAtMaxIntensity() }, new EvaluationSource(-22.95, -43.25));

        Assert.Equal(0.84, result.Score, ScorePrecision);
        Assert.Equal(RiskLevel.Critical, result.Level);
    }

    [Fact]
    public async Task EvaluateRiskAsync_OutsideEveryRegion_KeepsTheBaseScore()
    {
        _catalogSnapshotsMock.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog());
        SetupNoCacheAndNoGeospatialContext(-22, -43);

        var result = await _service.EvaluateRiskAsync(-22, -43, MaxMetrics(),
            new RiskEvents { Flood = DetectedAtMaxIntensity() }, new EvaluationSource(-10, -40));

        Assert.Equal(0.70, result.Score, ScorePrecision);
    }

    [Fact]
    public async Task EvaluateRiskAsync_WhenTheMatrixClassifiesTheEventHigher_RaisesTheLevel()
    {
        _catalogSnapshotsMock.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog());
        SetupNoCacheAndNoGeospatialContext(-10, -40);

        var result = await _service.EvaluateRiskAsync(-10, -40, new RiskMetrics(),
            new RiskEvents { Flood = new EventDetails { Detected = true, Intensity = 6 } },
            new EvaluationSource(-10, -40, "flood", 6));

        Assert.True(result.Score < 0.3);
        Assert.Equal(RiskLevel.Critical, result.Level);
    }

    [Fact]
    public async Task EvaluateRiskAsync_MatrixNeverLowersTheLevel()
    {
        var catalog = new RiskCatalogSnapshot(7, DateTime.UtcNow, [], [new SeverityRule("Flood", "Low", 0, 10)],
            [new RiskMatrixRule("Flood", "Low", RiskLevel.Low)]);
        _catalogSnapshotsMock.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(catalog);
        SetupNoCacheAndNoGeospatialContext(-22, -43);

        var result = await _service.EvaluateRiskAsync(-22, -43, MaxMetrics(),
            new RiskEvents { Flood = DetectedAtMaxIntensity() }, new EvaluationSource(-22, -43, "Flood", 10));

        Assert.Equal(RiskLevel.High, result.Level);
    }

    [Fact]
    public async Task EvaluateRiskAsync_WithCachedScore_StillAppliesTheMatrixFloorOfTheEvent()
    {
        _catalogSnapshotsMock.Setup(x => x.GetLatestAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Catalog());
        _cacheMock.Setup(x => x.GetAsync(-10, -40, It.IsAny<CancellationToken>())).ReturnsAsync(
            new RiskScoreCacheEntry { Score = 0.1, Level = "Low", CalculatedAt = DateTime.UtcNow });

        var result = await _service.EvaluateRiskAsync(-10, -40, new RiskMetrics(), new RiskEvents(),
            new EvaluationSource(-10, -40, "Flood", 8));

        Assert.Equal(0.1, result.Score);
        Assert.Equal(RiskLevel.Critical, result.Level);
    }
}
