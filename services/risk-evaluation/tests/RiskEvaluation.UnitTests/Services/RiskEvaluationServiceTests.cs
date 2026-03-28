using Microsoft.Extensions.Logging;
using Moq;
using RiskEvaluation.Application.IntegrationClients;
using RiskEvaluation.Application.Services;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.Events;
using RiskEvaluation.Domain.Interfaces;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Application.Interfaces;
using RiskEvaluation.Domain.ValueObjects;

namespace RiskEvaluation.UnitTests.Services;

public class RiskEvaluationServiceTests
{
    private readonly Mock<IRiskEvaluationRepository> _repositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<RiskCalculationService> _calculationServiceMock;
    private readonly Mock<IRecentScoresCache> _cacheMock;
    private readonly Mock<IRiskCatalogClient> _riskCatalogClientMock;
    private readonly Mock<IGeospatialClient> _geospatialClientMock;
    private readonly Mock<ILogger<RiskEvaluationService>> _loggerMock;
    private readonly RiskEvaluationService _service;

    public RiskEvaluationServiceTests()
    {
        _repositoryMock = new Mock<IRiskEvaluationRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _calculationServiceMock = new Mock<RiskCalculationService>();
        _cacheMock = new Mock<IRecentScoresCache>();
        _riskCatalogClientMock = new Mock<IRiskCatalogClient>();
        _geospatialClientMock = new Mock<IGeospatialClient>();
        _loggerMock = new Mock<ILogger<RiskEvaluationService>>();

        _service = new RiskEvaluationService(
            _repositoryMock.Object,
            _eventPublisherMock.Object,
            _calculationServiceMock.Object,
            _cacheMock.Object,
            _riskCatalogClientMock.Object,
            _geospatialClientMock.Object,
            _loggerMock.Object);
    }

    [Theory]
    [InlineData(0.1, 0.2, 0.5, 0.23, RiskLevel.Low)]
    [InlineData(0.2, 0.3, 0.4, 0.28, RiskLevel.Low)]
    public async Task EvaluateRiskAsync_LowRisk_ShouldSaveAndPublishEvent(
        double gust, double precipitation, double pressure, double score, RiskLevel level)
    {
        // Arrange
        const int latitude = -23;
        const int longitude = -46;
        var metrics = new RiskMetrics { WindGust = gust, Rainfall = precipitation, PressureChange = pressure };
        var events = new RiskEvents();

        _cacheMock.Setup(x => x.GetAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskScoreCacheEntry?)null);
        _riskCatalogClientMock.Setup(x => x.GetLatestRiskWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskWeightsDto?)null);
        _geospatialClientMock.Setup(x => x.GetSpatialContextAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeospatialContextDto?)null);
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(
            metrics, events, null, null, null)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(level);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(latitude, result.Latitude);
        Assert.Equal(longitude, result.Longitude);
        Assert.Equal(score, result.Score);
        Assert.Equal(level, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.Is<RiskEvaluationEntity>(
            e => e.Latitude == latitude && e.Longitude == longitude && e.Score == score && e.Level == level)), Times.Once);

        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<RiskEvaluatedEvent>(
            e => e.Latitude == latitude && e.Longitude == longitude && e.Score == score && e.Level == level.ToString())), Times.Once);

        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<HighRiskDetectedEvent>()), Times.Never);
        _cacheMock.Verify(x => x.SetAsync(latitude, longitude, It.IsAny<RiskScoreCacheEntry>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0.5, 0.6, 0.4, 0.52, RiskLevel.Medium)]
    [InlineData(0.6, 0.5, 0.5, 0.55, RiskLevel.Medium)]
    public async Task EvaluateRiskAsync_MediumRisk_ShouldSaveAndPublishEvent(
        double gust, double precipitation, double pressure, double score, RiskLevel level)
    {
        // Arrange
        const int latitude = -23;
        const int longitude = -46;
        var metrics = new RiskMetrics { WindGust = gust, Rainfall = precipitation, PressureChange = pressure };
        var events = new RiskEvents();

        _cacheMock.Setup(x => x.GetAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskScoreCacheEntry?)null);
        _riskCatalogClientMock.Setup(x => x.GetLatestRiskWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskWeightsDto?)null);
        _geospatialClientMock.Setup(x => x.GetSpatialContextAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeospatialContextDto?)null);
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(
            metrics, events, null, null, null)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(level);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(latitude, result.Latitude);
        Assert.Equal(longitude, result.Longitude);
        Assert.Equal(score, result.Score);
        Assert.Equal(level, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<RiskEvaluatedEvent>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<HighRiskDetectedEvent>()), Times.Never);
    }

    [Theory]
    [InlineData(0.8, 0.8, 0.6, 0.76, RiskLevel.High)]
    public async Task EvaluateRiskAsync_HighRisk_ShouldSaveAndPublishBothEvents(
        double gust, double precipitation, double pressure, double score, RiskLevel level)
    {
        // Arrange
        const int latitude = -22;
        const int longitude = -43;
        var metrics = new RiskMetrics { WindGust = gust, Rainfall = precipitation, PressureChange = pressure };
        var events = new RiskEvents();

        _cacheMock.Setup(x => x.GetAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskScoreCacheEntry?)null);
        _riskCatalogClientMock.Setup(x => x.GetLatestRiskWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskWeightsDto?)null);
        _geospatialClientMock.Setup(x => x.GetSpatialContextAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeospatialContextDto?)null);
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(
            metrics, events, null, null, null)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(level);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(latitude, result.Latitude);
        Assert.Equal(longitude, result.Longitude);
        Assert.Equal(score, result.Score);
        Assert.Equal(level, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<RiskEvaluatedEvent>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<HighRiskDetectedEvent>(
            e => e.Latitude == latitude && e.Longitude == longitude && e.Score == score && e.Level == level.ToString())), Times.Once);
    }

    [Theory]
    [InlineData(1.0, 1.0, 1.0, 1.0, RiskLevel.Critical)]
    [InlineData(0.9, 0.9, 0.8, 0.88, RiskLevel.Critical)]
    public async Task EvaluateRiskAsync_CriticalRisk_ShouldSaveAndPublishBothEvents(
        double gust, double precipitation, double pressure, double score, RiskLevel level)
    {
        // Arrange
        const int latitude = -22;
        const int longitude = -43;
        var metrics = new RiskMetrics { WindGust = gust, Rainfall = precipitation, PressureChange = pressure };
        var events = new RiskEvents();

        _cacheMock.Setup(x => x.GetAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskScoreCacheEntry?)null);
        _riskCatalogClientMock.Setup(x => x.GetLatestRiskWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskWeightsDto?)null);
        _geospatialClientMock.Setup(x => x.GetSpatialContextAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeospatialContextDto?)null);
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(
            metrics, events, null, null, null)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(level);

        // Act
        var result = await _service.EvaluateRiskAsync(latitude, longitude, metrics, events);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(latitude, result.Latitude);
        Assert.Equal(longitude, result.Longitude);
        Assert.Equal(score, result.Score);
        Assert.Equal(level, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<RiskEvaluatedEvent>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<HighRiskDetectedEvent>(
            e => e.Latitude == latitude && e.Longitude == longitude && e.Score == score && e.Level == level.ToString())), Times.Once);
    }

    [Fact]
    public async Task EvaluateRiskAsync_ShouldGenerateUniqueId()
    {
        // Arrange
        const int latitude = -23;
        const int longitude = -46;
        const double gust = 0.5;
        const double precipitation = 0.5;
        const double pressure = 0.5;
        const double score = 0.5;
        var metrics = new RiskMetrics { WindGust = gust, Rainfall = precipitation, PressureChange = pressure };
        var events = new RiskEvents();

        _cacheMock.Setup(x => x.GetAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskScoreCacheEntry?)null);
        _riskCatalogClientMock.Setup(x => x.GetLatestRiskWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskWeightsDto?)null);
        _geospatialClientMock.Setup(x => x.GetSpatialContextAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeospatialContextDto?)null);
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(
            metrics, events, null, null, null)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(RiskLevel.Medium);

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
        const double gust = 0.2;
        const double precipitation = 0.2;
        const double pressure = 0.3;
        const double score = 0.25;
        var metrics = new RiskMetrics { WindGust = gust, Rainfall = precipitation, PressureChange = pressure };
        var events = new RiskEvents();

        _cacheMock.Setup(x => x.GetAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskScoreCacheEntry?)null);
        _riskCatalogClientMock.Setup(x => x.GetLatestRiskWeightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((RiskWeightsDto?)null);
        _geospatialClientMock.Setup(x => x.GetSpatialContextAsync(latitude, longitude, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeospatialContextDto?)null);
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(
            metrics, events, null, null, null)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(RiskLevel.Low);

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
        _riskCatalogClientMock.Verify(x => x.GetLatestRiskWeightsAsync(It.IsAny<CancellationToken>()), Times.Never);
        _geospatialClientMock.Verify(x => x.GetSpatialContextAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
