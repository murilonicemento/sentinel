using Ingestion.Application.Commands;
using Ingestion.Application.Handlers;
using Ingestion.Domain.Interfaces.Repositories;
using Moq;

namespace Ingestion.UnitTests;

public class RegisterDataSourceHandlerTests
{
    private readonly Mock<IDataSourceRepository> _mockDataSourceRepository;
    private readonly RegisterDataSourceHandler _handler;

    public RegisterDataSourceHandlerTests()
    {
        _mockDataSourceRepository = new Mock<IDataSourceRepository>();
        _handler = new RegisterDataSourceHandler(_mockDataSourceRepository.Object);
    }

    #region Success

    [Fact]
    public async Task Handle_WithValidCommand_SuccessfullyRegistersDataSource()
    {
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedDataSourceId, result.dataSourceId);
        Assert.Equal(expectedTenantId, result.tenantId);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.Is<Domain.AggregateRoots.DataSource>(
                ds => ds.Name == command.Name &&
                      ds.Endpoint == command.Endpoint &&
                      ds.DataSourceType == "Sensor" &&
                      ds.MeasurementType == "Temperature" &&
                      ds.CollectionFrequency == "Hourly")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithAllDataSourceTypes_RegistersSuccessfully()
    {
        var dataSourceTypes = new[] { "Sensor", "Api", "File", "ExternalSystem" };
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        foreach (var dataSourceType in dataSourceTypes)
        {
            var command = new RegisterDataSourceCommand
            {
                Name = $"Test {dataSourceType}",
                Endpoint = "http://test.api",
                DataSourceType = dataSourceType,
                MeasurementType = "Temperature",
                CollectionFrequency = "Hourly"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(expectedDataSourceId, result.dataSourceId);
            Assert.Equal(expectedTenantId, result.tenantId);
        }

        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Exactly(dataSourceTypes.Length));
    }

    [Fact]
    public async Task Handle_WithAllMeasurementTypes_RegistersSuccessfully()
    {
        var measurementTypes = new[] { "Temperature", "Humidity", "WindSpeed", "Rainfall", "Pressure" };
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        foreach (var measurementType in measurementTypes)
        {
            var command = new RegisterDataSourceCommand
            {
                Name = $"Test {measurementType}",
                Endpoint = "http://test.api",
                DataSourceType = "Sensor",
                MeasurementType = measurementType,
                CollectionFrequency = "Hourly"
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(expectedDataSourceId, result.dataSourceId);
            Assert.Equal(expectedTenantId, result.tenantId);
        }

        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Exactly(measurementTypes.Length));
    }

    [Fact]
    public async Task Handle_WithAllCollectionFrequencies_RegistersSuccessfully()
    {
        var collectionFrequencies = new[] { "Hourly", "Daily", "Weekly" };
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        foreach (var collectionFrequency in collectionFrequencies)
        {
            var command = new RegisterDataSourceCommand
            {
                Name = "Test Sensor",
                Endpoint = "http://test.api",
                DataSourceType = "Sensor",
                MeasurementType = "Temperature",
                CollectionFrequency = collectionFrequency
            };

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(expectedDataSourceId, result.dataSourceId);
            Assert.Equal(expectedTenantId, result.tenantId);
        }

        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Exactly(collectionFrequencies.Length));
    }

    [Fact]
    public async Task Handle_WithValidCommand_GeneratesNewGuidForDataSourceId()
    {
        var expectedTenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        Guid capturedDataSourceId = Guid.Empty;
        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .Callback<Domain.AggregateRoots.DataSource>(ds => capturedDataSourceId = ds.Id)
            .ReturnsAsync((Domain.AggregateRoots.DataSource ds) => (ds.Id, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, capturedDataSourceId);
        Assert.Equal(capturedDataSourceId, result.dataSourceId);
    }

    [Fact]
    public async Task Handle_WithValidCommand_GeneratesNewGuidForTenantId()
    {
        var expectedDataSourceId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        Guid capturedTenantId = Guid.Empty;
        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .Callback<Domain.AggregateRoots.DataSource>(ds => capturedTenantId = ds.TenantId)
            .ReturnsAsync((Domain.AggregateRoots.DataSource ds) => (expectedDataSourceId, ds.TenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, capturedTenantId);
        Assert.Equal(capturedTenantId, result.tenantId);
    }

    #endregion

    #region DataSourceType Validation

    [Fact]
    public async Task Handle_WithInvalidDataSourceType_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "InvalidType",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid DataSourceType", exception.Message);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyDataSourceType_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = string.Empty,
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullDataSourceType_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = null!,
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    #endregion

    #region MeasurementType Validation

    [Fact]
    public async Task Handle_WithInvalidMeasurementType_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "InvalidMeasurement",
            CollectionFrequency = "Hourly"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid measurement type", exception.Message);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyMeasurementType_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = string.Empty,
            CollectionFrequency = "Hourly"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullMeasurementType_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = null!,
            CollectionFrequency = "Hourly"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    #endregion

    #region CollectionFrequency Validation

    [Fact]
    public async Task Handle_WithInvalidCollectionFrequency_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "InvalidFrequency"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid collection frequency type", exception.Message);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyCollectionFrequency_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = string.Empty
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCollectionFrequency_ThrowsArgumentException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = null!
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    #endregion

    #region Repository Validation

    [Fact]
    public async Task Handle_WhenRepositoryThrowsException_PropagatesException()
    {
        var command = new RegisterDataSourceCommand
        {
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        await Assert.ThrowsAsync<Exception>(
            () => _handler.Handle(command, CancellationToken.None));
    }


    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithLongName_RegistersSuccessfully()
    {
        var longName = new string('A', 1000);
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            Name = longName,
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedDataSourceId, result.dataSourceId);
        Assert.Equal(expectedTenantId, result.tenantId);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.Is<Domain.AggregateRoots.DataSource>(
                ds => ds.Name == longName)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithLongEndpoint_RegistersSuccessfully()
    {
        var longEndpoint = $"http://{new string('a', 1000)}.com";
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            Name = "Test Sensor",
            Endpoint = longEndpoint,
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedDataSourceId, result.dataSourceId);
        Assert.Equal(expectedTenantId, result.tenantId);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.Is<Domain.AggregateRoots.DataSource>(
                ds => ds.Endpoint == longEndpoint)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithSpecialCharactersInName_RegistersSuccessfully()
    {
        var nameWithSpecialChars = "Sensor-Test_123 (Temperature)";
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            Name = nameWithSpecialChars,
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedDataSourceId, result.dataSourceId);
        Assert.Equal(expectedTenantId, result.tenantId);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.Is<Domain.AggregateRoots.DataSource>(
                ds => ds.Name == nameWithSpecialChars)),
            Times.Once);
    }

    #endregion
}

