using Ingestion.Application.Commands;
using Ingestion.Application.Handlers;
using Ingestion.Application.Interfaces.Services;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Moq;

namespace Ingestion.UnitTests;

public class RegisterDataSourceHandlerTests
{
    private readonly Mock<IDataSourceRepository> _mockDataSourceRepository;
    private readonly Mock<ITenantRepository> _mockTenantRepository;
    private readonly Mock<ITenantBillingGateway> _mockTenantBillingGateway;
    private readonly RegisterDataSourceHandler _handler;
    private readonly Mock<IEventTypePermissionRepository> _mockEventTypePermissionRepository;

    public RegisterDataSourceHandlerTests()
    {
        _mockTenantRepository = new Mock<ITenantRepository>();
        _mockDataSourceRepository = new Mock<IDataSourceRepository>();
        _mockTenantBillingGateway = new Mock<ITenantBillingGateway>();
        _mockEventTypePermissionRepository = new Mock<IEventTypePermissionRepository>();
        _mockTenantBillingGateway
            .Setup(x => x.ValidateTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockEventTypePermissionRepository
            .Setup(x => x.RegisterManyAsync(It.IsAny<List<Ingestion.Domain.Aggregates.EventTypePermission>>()))
            .ReturnsAsync(true);
        _handler = new RegisterDataSourceHandler(
            _mockTenantRepository.Object,
            _mockDataSourceRepository.Object,
            _mockEventTypePermissionRepository.Object,
            _mockTenantBillingGateway.Object
        );
    }

    private void ResetMocks()
    {
        _mockTenantRepository.Reset();
        _mockDataSourceRepository.Reset();
        _mockTenantBillingGateway.Reset();
        _mockEventTypePermissionRepository.Reset();
        _mockTenantBillingGateway
            .Setup(x => x.ValidateTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockEventTypePermissionRepository
            .Setup(x => x.RegisterManyAsync(It.IsAny<List<Ingestion.Domain.Aggregates.EventTypePermission>>()))
            .ReturnsAsync(true);
    }

    #region Success

    [Fact]
    public async Task Handle_WithValidCommand_SuccessfullyRegistersDataSource()
    {
        ResetMocks();
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = expectedTenantId,
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(expectedTenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Temperature Sensor", expectedTenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedDataSourceId, result.dataSourceId);
        Assert.Equal(expectedTenantId, result.tenantId);
        _mockTenantRepository.Verify(x => x.ExistsAsync(expectedTenantId), Times.Once);
        _mockTenantBillingGateway.Verify(x => x.ValidateTenantAsync(expectedTenantId, It.IsAny<CancellationToken>()), Times.Once);
        _mockDataSourceRepository.Verify(
            x => x.GetByNameAndTenantAsync("Temperature Sensor", expectedTenantId), Times.Once);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.Is<Domain.AggregateRoots.DataSource>(ds => ds.Name == command.Name &&
                                                                               ds.Endpoint == command.Endpoint &&
                                                                               ds.DataSourceType == "Sensor" &&
                                                                               ds.MeasurementType == "Temperature" &&
                                                                               ds.CollectionFrequency == "Hourly")),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenBillingRejectsTenant_ThrowsInvalidOperationException()
    {
        ResetMocks();
        var tenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(true);

        _mockTenantBillingGateway
            .Setup(x => x.ValidateTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("tenant", exception.Message, StringComparison.OrdinalIgnoreCase);
        _mockDataSourceRepository.Verify(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAllDataSourceTypes_RegistersSuccessfully()
    {
        ResetMocks();
        var dataSourceTypes = new[] { "Sensor", "Api", "File", "ExternalSystem" };
        var expectedDataSourceId = Guid.NewGuid();
        var expectedTenantId = Guid.NewGuid();

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(expectedTenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync(It.IsAny<string>(), expectedTenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        foreach (var dataSourceType in dataSourceTypes)
        {
            var command = new RegisterDataSourceCommand
            {
                TenantId = expectedTenantId,
                Name = $"Test {dataSourceType}",
                Endpoint = "http://test.api",
                DataSourceType = dataSourceType,
                MeasurementType = "Temperature",
                CollectionFrequency = "Hourly"
            };

            // Garantir que o mock esteja configurado corretamente para cada nome específico
            _mockDataSourceRepository
                .Setup(x => x.GetByNameAndTenantAsync(command.Name, expectedTenantId))
                .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(expectedDataSourceId, result.dataSourceId);
            Assert.Equal(expectedTenantId, result.tenantId);
        }

        _mockTenantRepository.Verify(x => x.ExistsAsync(expectedTenantId), Times.Exactly(dataSourceTypes.Length));
        _mockDataSourceRepository.Verify(
            x => x.GetByNameAndTenantAsync(It.IsAny<string>(), expectedTenantId),
            Times.Exactly(dataSourceTypes.Length));
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

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(expectedTenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync(It.IsAny<string>(), expectedTenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        foreach (var measurementType in measurementTypes)
        {
            var command = new RegisterDataSourceCommand
            {
                TenantId = expectedTenantId,
                Name = $"Test {measurementType}",
                Endpoint = "http://test.api",
                DataSourceType = "Sensor",
                MeasurementType = measurementType,
                CollectionFrequency = "Hourly"
            };

            // Garantir que o mock esteja configurado corretamente para cada nome específico
            _mockDataSourceRepository
                .Setup(x => x.GetByNameAndTenantAsync(command.Name, expectedTenantId))
                .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(expectedDataSourceId, result.dataSourceId);
            Assert.Equal(expectedTenantId, result.tenantId);
        }

        _mockTenantRepository.Verify(x => x.ExistsAsync(expectedTenantId), Times.Exactly(measurementTypes.Length));
        _mockDataSourceRepository.Verify(
            x => x.GetByNameAndTenantAsync(It.IsAny<string>(), expectedTenantId),
            Times.Exactly(measurementTypes.Length));
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

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(expectedTenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync(It.IsAny<string>(), expectedTenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        foreach (var collectionFrequency in collectionFrequencies)
        {
            var command = new RegisterDataSourceCommand
            {
                TenantId = expectedTenantId,
                Name = "Test Sensor",
                Endpoint = "http://test.api",
                DataSourceType = "Sensor",
                MeasurementType = "Temperature",
                CollectionFrequency = collectionFrequency
            };

            // Garantir que o mock esteja configurado corretamente para cada nome específico
            _mockDataSourceRepository
                .Setup(x => x.GetByNameAndTenantAsync(command.Name, expectedTenantId))
                .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(expectedDataSourceId, result.dataSourceId);
            Assert.Equal(expectedTenantId, result.tenantId);
        }

        _mockTenantRepository.Verify(x => x.ExistsAsync(expectedTenantId), Times.Exactly(collectionFrequencies.Length));
        _mockDataSourceRepository.Verify(
            x => x.GetByNameAndTenantAsync(It.IsAny<string>(), expectedTenantId),
            Times.Exactly(collectionFrequencies.Length));
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
            TenantId = expectedTenantId,
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(expectedTenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Temperature Sensor", expectedTenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

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
            TenantId = Guid.NewGuid(),
            Name = "Temperature Sensor",
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(command.TenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Temperature Sensor", command.TenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

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

    #region MeasurementType Validation

    [Fact]
    public async Task Handle_WithInvalidMeasurementType_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "InvalidMeasurement",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Test Sensor", tenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid measurement type", exception.Message);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyMeasurementType_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = string.Empty,
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Test Sensor", tenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullMeasurementType_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = null!,
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Test Sensor", tenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    #endregion

    #region CollectionFrequency Validation

    [Fact]
    public async Task Handle_WithInvalidCollectionFrequency_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "InvalidFrequency"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Test Sensor", tenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Invalid collection frequency type", exception.Message);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithEmptyCollectionFrequency_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = string.Empty
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Test Sensor", tenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithNullCollectionFrequency_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = null!
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Test Sensor", tenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()),
            Times.Never);
    }

    #endregion

    #region Tenant and Name Validation

    [Fact]
    public async Task Handle_WithNonExistentTenant_ThrowsKeyNotFoundException()
    {
        var tenantId = Guid.NewGuid();
        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Test Sensor",
            Endpoint = "http://test.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(false);

        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains($"Tenant {tenantId} not found", exception.Message);
        _mockTenantRepository.Verify(x => x.ExistsAsync(tenantId), Times.Once);
        _mockDataSourceRepository.Verify(
            x => x.GetByNameAndTenantAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithExistingDataSourceName_ThrowsInvalidOperationException()
    {
        var tenantId = Guid.NewGuid();
        var existingDataSource = new Domain.AggregateRoots.DataSource(
            Guid.NewGuid(),
            "Test Sensor",
            "http://test.api",
            "Sensor",
            "Temperature",
            "Hourly",
            tenantId
        );

        var command = new RegisterDataSourceCommand
        {
            TenantId = tenantId,
            Name = "Test Sensor",
            Endpoint = "http://newtest.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(tenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Test Sensor", tenantId))
            .ReturnsAsync(existingDataSource);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("DataSource 'Test Sensor' already exists for this tenant", exception.Message);
        _mockTenantRepository.Verify(x => x.ExistsAsync(tenantId), Times.Once);
        _mockDataSourceRepository.Verify(
            x => x.GetByNameAndTenantAsync("Test Sensor", tenantId), Times.Once);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()), Times.Never);
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
            TenantId = expectedTenantId,
            Name = longName,
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(expectedTenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync(longName, expectedTenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedDataSourceId, result.dataSourceId);
        Assert.Equal(expectedTenantId, result.tenantId);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.Is<Domain.AggregateRoots.DataSource>(ds => ds.Name == longName)),
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
            TenantId = expectedTenantId,
            Name = "Test Sensor",
            Endpoint = longEndpoint,
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(expectedTenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync("Test Sensor", expectedTenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedDataSourceId, result.dataSourceId);
        Assert.Equal(expectedTenantId, result.tenantId);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.Is<Domain.AggregateRoots.DataSource>(ds => ds.Endpoint == longEndpoint)),
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
            TenantId = expectedTenantId,
            Name = nameWithSpecialChars,
            Endpoint = "http://sensor.api",
            DataSourceType = "Sensor",
            MeasurementType = "Temperature",
            CollectionFrequency = "Hourly"
        };

        _mockTenantRepository
            .Setup(x => x.ExistsAsync(expectedTenantId))
            .ReturnsAsync(true);

        _mockDataSourceRepository
            .Setup(x => x.GetByNameAndTenantAsync(nameWithSpecialChars, expectedTenantId))
            .ReturnsAsync((Domain.AggregateRoots.DataSource?)null);

        _mockDataSourceRepository
            .Setup(x => x.RegisterAsync(It.IsAny<Domain.AggregateRoots.DataSource>()))
            .ReturnsAsync((expectedDataSourceId, expectedTenantId));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(expectedDataSourceId, result.dataSourceId);
        Assert.Equal(expectedTenantId, result.tenantId);
        _mockDataSourceRepository.Verify(
            x => x.RegisterAsync(It.Is<Domain.AggregateRoots.DataSource>(ds => ds.Name == nameWithSpecialChars)),
            Times.Once);
    }

    #endregion
}