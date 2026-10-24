using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Handlers;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Enums;
using Ingestion.Domain.Interfaces.Repositories;
using Moq;

namespace Ingestion.UnitTests;

public class RegisterSensorCollectionHandlerTests
{
    private readonly Mock<ISensorCollectionService> _mockSensorCollectionService;
    private readonly Mock<IDataSourceRepository> _mockDataSourceRepository;
    private readonly RegisterSensorCollectionHandler _handler;

    public RegisterSensorCollectionHandlerTests()
    {
        _mockSensorCollectionService = new Mock<ISensorCollectionService>();
        _mockDataSourceRepository = new Mock<IDataSourceRepository>();
        _handler = new RegisterSensorCollectionHandler(
            _mockSensorCollectionService.Object,
            _mockDataSourceRepository.Object);
    }

    private static RegisterSensorCollectionCommand CreateCommand(Guid datasourceId, Guid tenantId) => new()
    {
        DatasourceId = datasourceId,
        TenantId = tenantId,
        CollectedAt = DateTime.UtcNow,
        Payload = "{\"data\": \"test\"}",
        SampleSensors = [new SampleSensorDTO { Unit = "C", SensorValue = 25.5 }]
    };

    private static List<SampleSensorDTO> SameSamples(RegisterSensorCollectionCommand command) =>
        It.Is<List<SampleSensorDTO>>(samples => samples.SequenceEqual(command.SampleSensors));

    [Theory]
    [InlineData("Temperature", "Climatic")]
    [InlineData("Rainfall", "Climatic")]
    [InlineData("Fire", "Disaster")]
    [InlineData("Earthquake", "Disaster")]
    public async Task Handle_RoutesToDomainInferredFromDataSourceMeasurementType(
        string measurementType, string expectedDomain)
    {
        // Arrange
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var command = CreateCommand(datasourceId, tenantId);
        var dataSource = new DataSource(datasourceId, "Sensor", "http://sensor.api", "Sensor", measurementType,
            "Hourly", tenantId);

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId)).Returns(dataSource);
        _mockSensorCollectionService.Setup(x => x.ProcessSensorCollection<SensorEventDetected>(
                datasourceId, tenantId, command.CollectedAt, command.Payload, SameSamples(command), expectedDomain,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(datasourceId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(datasourceId, result);
        _mockSensorCollectionService.Verify(x => x.ProcessSensorCollection<SensorEventDetected>(
            datasourceId, tenantId, command.CollectedAt, command.Payload, SameSamples(command), expectedDomain,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDataSourceDoesNotBelongToTenant_ThrowsKeyNotFoundAndDoesNotProcess()
    {
        // Arrange
        var command = CreateCommand(Guid.NewGuid(), Guid.NewGuid());
        _mockDataSourceRepository
            .Setup(x => x.GetByIdAndTenantId(command.DatasourceId, command.TenantId))
            .Returns((DataSource?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        _mockSensorCollectionService.Verify(x => x.ProcessSensorCollection<SensorEventDetected>(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(),
            It.IsAny<List<SampleSensorDTO>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMeasurementTypeHasNoDomain_ThrowsArgumentException()
    {
        // Arrange
        var command = CreateCommand(Guid.NewGuid(), Guid.NewGuid());
        var dataSource = new DataSource(command.DatasourceId, "Sensor", "http://sensor.api", "Sensor", "Unknown",
            "Hourly", command.TenantId);
        _mockDataSourceRepository
            .Setup(x => x.GetByIdAndTenantId(command.DatasourceId, command.TenantId))
            .Returns(dataSource);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Theory]
    [InlineData("Temperature", SensorDomainEnum.Climatic)]
    [InlineData("Humidity", SensorDomainEnum.Climatic)]
    [InlineData("WindSpeed", SensorDomainEnum.Climatic)]
    [InlineData("Rainfall", SensorDomainEnum.Climatic)]
    [InlineData("Pressure", SensorDomainEnum.Climatic)]
    [InlineData("Fire", SensorDomainEnum.Disaster)]
    [InlineData("Earthquake", SensorDomainEnum.Disaster)]
    public void DataSource_ResolveSensorDomain_MapsEveryMeasurementType(
        string measurementType, SensorDomainEnum expectedDomain)
    {
        var dataSource = new DataSource(Guid.NewGuid(), "Sensor", "http://sensor.api", "Sensor", measurementType,
            "Hourly", Guid.NewGuid());

        Assert.Equal(expectedDomain, dataSource.ResolveSensorDomain());
    }
}
