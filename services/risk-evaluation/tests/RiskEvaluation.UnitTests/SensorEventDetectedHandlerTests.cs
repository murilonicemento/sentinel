using Moq;
using RiskEvaluation.Application.Contracts;
using RiskEvaluation.Application.Handlers;
using RiskEvaluation.Application.Interfaces.Services;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.ValueObjects;
using Xunit;

namespace RiskEvaluation.UnitTests;

public class SensorEventDetectedHandlerTests
{
    [Theory]
    [InlineData("TemperatureAnomaly", nameof(RiskMetrics.TemperatureAnomaly))]
    [InlineData("HumidityAnomaly", nameof(RiskMetrics.HumidityAnomaly))]
    [InlineData("WindGust", nameof(RiskMetrics.WindGust))]
    [InlineData("PressureChange", nameof(RiskMetrics.PressureChange))]
    [InlineData("Rainfall", nameof(RiskMetrics.Rainfall))]
    public async Task ClimaticEventType_IsMappedToItsMetric(string eventType, string metric)
    {
        RiskMetrics? captured = null;
        var service = new Mock<IRiskEvaluationService>();
        service.Setup(s => s.EvaluateRiskAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<RiskMetrics>(),
                It.IsAny<RiskEvents>()))
            .Callback<int, int, RiskMetrics, RiskEvents>((_, _, metrics, _) => captured = metrics)
            .ReturnsAsync((RiskEvaluationEntity)null!);

        await new SensorEventDetectedHandler(service.Object).Handle(
            new SensorEventDetected { EventType = eventType, Intensity = 12.5, Latitude = -23, Longitude = -46 },
            CancellationToken.None);

        Assert.Equal(12.5, (double)typeof(RiskMetrics).GetProperty(metric)!.GetValue(captured)!);
    }
}
