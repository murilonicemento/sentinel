using Moq;
using RiskEvaluation.Application.Services;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Enums;
using RiskEvaluation.Domain.Events;
using RiskEvaluation.Domain.Repositories;
using RiskEvaluation.Domain.Services;
using RiskEvaluation.Application.Interfaces;

namespace RiskEvaluation.UnitTests.Services;

public class RiskEvaluationServiceTests
{
    private readonly Mock<IRiskEvaluationRepository> _repositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<RiskCalculationService> _calculationServiceMock;
    private readonly RiskEvaluationService _service;

    public RiskEvaluationServiceTests()
    {
        _repositoryMock = new Mock<IRiskEvaluationRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _calculationServiceMock = new Mock<RiskCalculationService>();

        _service = new RiskEvaluationService(
            _repositoryMock.Object,
            _eventPublisherMock.Object,
            _calculationServiceMock.Object);
    }

    [Theory]
    [InlineData(0.1, 0.2, 0.5, 0.23, RiskLevel.Low)]
    [InlineData(0.2, 0.3, 0.4, 0.28, RiskLevel.Low)]
    public async Task EvaluateRiskAsync_LowRisk_ShouldSaveAndPublishEvent(
        double gust, double precipitation, double pressure, double score, RiskLevel level)
    {
        // Arrange
        const string location = "TestLocation";
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(gust, precipitation, pressure)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(level);

        // Act
        var result = await _service.EvaluateRiskAsync(location, gust, precipitation, pressure);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(location, result.Location);
        Assert.Equal(score, result.Score);
        Assert.Equal(level, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.Is<RiskEvaluationEntity>(
            e => e.Location == location && e.Score == score && e.Level == level)), Times.Once);

        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<RiskEvaluatedEvent>(
            e => e.Location == location && e.Score == score && e.Level == level.ToString())), Times.Once);

        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<HighRiskDetectedEvent>()), Times.Never);
    }

    [Theory]
    [InlineData(0.5, 0.6, 0.4, 0.52, RiskLevel.Medium)]
    [InlineData(0.6, 0.5, 0.5, 0.55, RiskLevel.Medium)]
    public async Task EvaluateRiskAsync_MediumRisk_ShouldSaveAndPublishEvent(
        double gust, double precipitation, double pressure, double score, RiskLevel level)
    {
        // Arrange
        const string location = "TestLocation";
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(gust, precipitation, pressure)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(level);

        // Act
        var result = await _service.EvaluateRiskAsync(location, gust, precipitation, pressure);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(location, result.Location);
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
        const string location = "HighRiskLocation";
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(gust, precipitation, pressure)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(level);

        // Act
        var result = await _service.EvaluateRiskAsync(location, gust, precipitation, pressure);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(location, result.Location);
        Assert.Equal(score, result.Score);
        Assert.Equal(level, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<RiskEvaluatedEvent>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<HighRiskDetectedEvent>(
            e => e.Location == location && e.Score == score && e.Level == level.ToString())), Times.Once);
    }

    [Theory]
    [InlineData(1.0, 1.0, 1.0, 1.0, RiskLevel.Critical)]
    [InlineData(0.9, 0.9, 0.8, 0.88, RiskLevel.Critical)]
    public async Task EvaluateRiskAsync_CriticalRisk_ShouldSaveAndPublishBothEvents(
        double gust, double precipitation, double pressure, double score, RiskLevel level)
    {
        // Arrange
        const string location = "CriticalRiskLocation";
        _calculationServiceMock.Setup(x => x.CalculateRiskScore(gust, precipitation, pressure)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(level);

        // Act
        var result = await _service.EvaluateRiskAsync(location, gust, precipitation, pressure);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(location, result.Location);
        Assert.Equal(score, result.Score);
        Assert.Equal(level, result.Level);

        _repositoryMock.Verify(x => x.AddAsync(It.IsAny<RiskEvaluationEntity>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.IsAny<RiskEvaluatedEvent>()), Times.Once);
        _eventPublisherMock.Verify(x => x.PublishAsync(It.Is<HighRiskDetectedEvent>(
            e => e.Location == location && e.Score == score && e.Level == level.ToString())), Times.Once);
    }

    [Fact]
    public async Task EvaluateRiskAsync_ShouldGenerateUniqueId()
    {
        // Arrange
        const string location = "TestLocation";
        const double gust = 0.5;
        const double precipitation = 0.5;
        const double pressure = 0.5;
        const double score = 0.5;

        _calculationServiceMock.Setup(x => x.CalculateRiskScore(gust, precipitation, pressure)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(RiskLevel.Medium);

        // Act
        var result1 = await _service.EvaluateRiskAsync(location, gust, precipitation, pressure);
        var result2 = await _service.EvaluateRiskAsync(location, gust, precipitation, pressure);

        // Assert
        Assert.NotEqual(result1.Id, result2.Id);
    }

    [Fact]
    public async Task EvaluateRiskAsync_ShouldSetTimestamp()
    {
        // Arrange
        const string location = "TestLocation";
        const double gust = 0.2;
        const double precipitation = 0.2;
        const double pressure = 0.3;
        const double score = 0.25;

        _calculationServiceMock.Setup(x => x.CalculateRiskScore(gust, precipitation, pressure)).Returns(score);
        _calculationServiceMock.Setup(x => x.ClassifyRiskLevel(score)).Returns(RiskLevel.Low);

        var beforeTest = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await _service.EvaluateRiskAsync(location, gust, precipitation, pressure);

        var afterTest = DateTime.UtcNow.AddSeconds(1);

        // Assert
        Assert.True(result.Timestamp >= beforeTest && result.Timestamp <= afterTest);
    }
}
