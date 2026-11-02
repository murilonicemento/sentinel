using System.Net;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Handlers;
using Ingestion.Application.Interfaces.Services;
using Moq;

namespace Ingestion.UnitTests;

public class RegisterClimaticEventHandlerTests
{
    private readonly Mock<ISensorCollectionService> _mockSensorCollectionService;
    private readonly RegisterClimaticEventHandler _handler;

    public RegisterClimaticEventHandlerTests()
    {
        _mockSensorCollectionService = new Mock<ISensorCollectionService>();
        _handler = new RegisterClimaticEventHandler(_mockSensorCollectionService.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsDataSourceId()
    {
        // Arrange
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var collectedAt = DateTime.UtcNow;
        var payload = "{\"data\": \"test\"}";
        var sampleSensors = new List<SampleSensorDTO>
        {
            new() { Unit = "C", SensorValue = 25.5 }
        };

        var command = new RegisterClimaticEventCommand
        {
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = collectedAt,
            Payload = payload,
            SampleSensors = sampleSensors
        };

        _mockSensorCollectionService.Setup(x => x.ProcessSensorCollection<object>(
            datasourceId, tenantId, collectedAt, payload, sampleSensors, "Climatic", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(datasourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(datasourceId, result);
        _mockSensorCollectionService.Verify(x => x.ProcessSensorCollection<object>(
            datasourceId, tenantId, collectedAt, payload, sampleSensors, "Climatic", 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleSampleSensors_CallsServiceWithCorrectParameters()
    {
        // Arrange
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var collectedAt = DateTime.UtcNow;
        var payload = "{\"data\": \"test\"}";
        var sampleSensors = new List<SampleSensorDTO>
        {
            new() { Unit = "C", SensorValue = 25.5 },
            new() { Unit = "C", SensorValue = 28.0 }
        };

        var command = new RegisterClimaticEventCommand
        {
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = collectedAt,
            Payload = payload,
            SampleSensors = sampleSensors
        };

        _mockSensorCollectionService.Setup(x => x.ProcessSensorCollection<object>(
            datasourceId, tenantId, collectedAt, payload, sampleSensors, "Climatic", 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(datasourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(datasourceId, result);
        _mockSensorCollectionService.Verify(x => x.ProcessSensorCollection<object>(
            datasourceId, tenantId, collectedAt, payload, sampleSensors, "Climatic", 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenServiceThrowsException_PropagatesException()
    {
        // Arrange
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var collectedAt = DateTime.UtcNow;
        var payload = "{\"data\": \"test\"}";
        var sampleSensors = new List<SampleSensorDTO>
        {
            new() { Unit = "C", SensorValue = 25.5 }
        };

        var command = new RegisterClimaticEventCommand
        {
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = collectedAt,
            Payload = payload,
            SampleSensors = sampleSensors
        };

        var expectedException = new InvalidOperationException("Service error");
        _mockSensorCollectionService.Setup(x => x.ProcessSensorCollection<object>(
            datasourceId, tenantId, collectedAt, payload, sampleSensors, "Climatic", 
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Equal(expectedException, exception);
    }
}
