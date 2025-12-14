using System.Net;
using Ingestion.Application.Commands;
using Ingestion.Application.DTO;
using Ingestion.Application.Events;
using Ingestion.Application.Handlers;
using Ingestion.Application.Interfaces.Deduplicators;
using Ingestion.Application.Interfaces.Providers;
using Ingestion.Domain.AggregateRoots;
using Ingestion.Domain.Aggregates;
using Ingestion.Domain.Interfaces.Repositories;
using Ingestion.Domain.Outbox;
using Ingestion.Domain.Repositories;
using Ingestion.Infrastructure.Read.Persistence.DbContext;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Moq;

namespace Ingestion.UnitTests;

public class RegisterSensorCollectionHandlerTests
{
    private readonly Mock<IDataSourceRepository> _mockDataSourceRepository;
    private readonly Mock<IDataCollectionRepository> _mockDataCollectionRepository;
    private readonly Mock<ISampleSensorRepository> _mockSampleSensorRepository;
    private readonly Mock<IEventDeduplicator> _mockEventDeduplicator;
    private readonly Mock<IObjectStorageProvider> _mockMinioProvider;
    private readonly Mock<IOutboxRepository> _mockOutboxRepository;
    private readonly Mock<ReadDbContext> _mockReadDbContext;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<RegisterSensorCollectionHandler>> _mockLogger;
    private readonly RegisterSensorCollectionHandler _handler;

    public RegisterSensorCollectionHandlerTests()
    {
        _mockDataSourceRepository = new Mock<IDataSourceRepository>();
        _mockDataCollectionRepository = new Mock<IDataCollectionRepository>();
        _mockSampleSensorRepository = new Mock<ISampleSensorRepository>();
        _mockEventDeduplicator = new Mock<IEventDeduplicator>();
        _mockMinioProvider = new Mock<IObjectStorageProvider>();
        _mockOutboxRepository = new Mock<IOutboxRepository>();
        _mockLogger = new Mock<ILogger<RegisterSensorCollectionHandler>>();

        var configData = new Dictionary<string, string?>
        {
            { "ConnectionStrings:IngestionReadDatabase", "mongodb://localhost:27017" }
        };

        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x[It.IsAny<string>()])
            .Returns((string key) => configData.TryGetValue(key, out var value) ? value : null);

        var configurationBuilder = new ConfigurationBuilder();
        configurationBuilder.AddInMemoryCollection(configData);
        var realConfiguration = configurationBuilder.Build();

        _mockReadDbContext = new Mock<ReadDbContext>(realConfiguration) { CallBase = false };
        _handler = new RegisterSensorCollectionHandler(
            _mockDataSourceRepository.Object,
            _mockDataCollectionRepository.Object,
            _mockSampleSensorRepository.Object,
            _mockEventDeduplicator.Object,
            _mockMinioProvider.Object,
            _mockOutboxRepository.Object,
            _mockReadDbContext.Object,
            _mockConfiguration.Object,
            _mockLogger.Object
        );
    }

    #region Payload Validation

    [Fact]
    public async Task Handle_WithNullPayload_ThrowsArgumentException()
    {
        var command = new RegisterSensorCollectionCommand
        {
            Payload = null!,
            DatasourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CollectedAt = DateTime.UtcNow,
            SampleSensors = new List<SampleSensorDTO>()
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithEmptyPayload_ThrowsArgumentException()
    {
        var command = new RegisterSensorCollectionCommand
        {
            Payload = string.Empty,
            DatasourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CollectedAt = DateTime.UtcNow,
            SampleSensors = new List<SampleSensorDTO>()
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    #endregion

    #region Unit Validation

    [Fact]
    public async Task Handle_WithNullUnitInSampleSensor_ThrowsArgumentException()
    {
        var sampleSensors = new List<SampleSensorDTO>
        {
            new() { Unit = null!, SensorValue = 25.5 }
        };
        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WithEmptyUnitInSampleSensor_ThrowsArgumentException()
    {
        var sampleSensors = new List<SampleSensorDTO>
        {
            new() { Unit = string.Empty, SensorValue = 25.5 }
        };
        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    #endregion

    #region DataSource and Tenant Validation

    [Fact]
    public async Task Handle_WithNonExistentDataSource_ThrowsKeyNotFoundException()
    {
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var sampleSensors = new List<SampleSensorDTO>
        {
            new() { Unit = "C", SensorValue = 25.5 }
        };
        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId))
            .Returns((DataSource?)null);

        var exception =
            await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Data source or tenant not exist", exception.Message);
    }

    #endregion

    #region Unit Type Validation

    [Fact]
    public async Task Handle_WithInvalidUnitForMeasurementType_ThrowsArgumentException()
    {
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var dataSource = new DataSource(
            datasourceId,
            "Temperature Sensor",
            "http://sensor.api",
            "Sensor",
            "Temperature",
            "Hourly",
            tenantId
        );
        var sampleSensors = new List<SampleSensorDTO>
        {
            new()
            {
                Unit = "invalid_unit",
                SensorValue = 25.5,
                Latitude = 0,
                Longitude = 0,
                RecordedAt = DateTime.UtcNow
            }
        };
        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId))
            .Returns(dataSource);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    #endregion

    #region Collection Frequency Validation

    [Fact]
    public async Task Handle_WithInvalidCollectionFrequency_ThrowsArgumentException()
    {
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var collectionTime = DateTime.UtcNow.AddHours(-1);

        var lastCollection = new DataCollection(
            Guid.NewGuid(),
            datasourceId,
            collectionTime,
            "{}",
            tenantId
        );
        var dataSource = new DataSource(
            datasourceId,
            "Temperature Sensor",
            "http://sensor.api",
            "Sensor",
            "Temperature",
            "Daily",
            tenantId
        )
        {
            DataCollections = new List<DataCollection> { lastCollection }
        };
        var sampleSensors = new List<SampleSensorDTO>
        {
            new()
            {
                Unit = "C",
                SensorValue = 25.5,
                Latitude = 0,
                Longitude = 0,
                RecordedAt = DateTime.UtcNow
            }
        };

        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId))
            .Returns(dataSource);

        await Assert.ThrowsAsync<ArgumentException>(() => _handler.Handle(command, CancellationToken.None));
    }

    #endregion

    #region Deduplication

    [Fact]
    public async Task Handle_WithDuplicateEvent_ReturnsDataSourceIdWithoutProcessing()
    {
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var dataSource = new DataSource(
            datasourceId,
            "Temperature Sensor",
            "http://sensor.api",
            "Sensor",
            "Temperature",
            "Hourly",
            tenantId
        );
        var sampleSensors = new List<SampleSensorDTO>
        {
            new()
            {
                Unit = "C",
                SensorValue = 25.5,
                Latitude = 0,
                Longitude = 0,
                RecordedAt = DateTime.UtcNow
            }
        };
        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId))
            .Returns(dataSource);
        _mockEventDeduplicator.Setup(x => x.IsDuplicateAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(datasourceId, result);
        _mockDataCollectionRepository.Verify(x => x.RegisterAsync(It.IsAny<DataCollection>()), Times.Never);
        _mockSampleSensorRepository.Verify(x => x.RegisterAsync(It.IsAny<SampleSensor>()), Times.Never);
    }

    #endregion

    #region Success

    [Fact]
    public async Task Handle_WithValidCommand_SuccessfullyRegistersCollection()
    {
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var dataSource = new DataSource(
            datasourceId,
            "Temperature Sensor",
            "http://sensor.api",
            "Sensor",
            "Temperature",
            "Hourly",
            tenantId
        );
        var sampleSensors = new List<SampleSensorDTO>
        {
            new()
            {
                Unit = "C",
                SensorValue = 25.5,
                Latitude = -23.5505,
                Longitude = -46.6333,
                RecordedAt = DateTime.UtcNow
            }
        };
        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId))
            .Returns(dataSource);
        _mockEventDeduplicator.Setup(x => x.IsDuplicateAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockConfiguration.Setup(x => x["MinIO:BucketName"])
            .Returns("test-bucket");
        _mockMinioProvider.Setup(x => x.UploadJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UploadResultDTO
            {
                ETag = "fake-etag",
                Size = 123,
                ObjectName = "whatever.json",
                ResponseContent = "ok",
                ResponseStatusCode = HttpStatusCode.OK
            });

        var mockCollection = new Mock<IMongoCollection<ClimaticEventDetectedEvent>>();
        _mockReadDbContext.Setup(x => x.GetCollection<ClimaticEventDetectedEvent>("events"))
            .Returns(mockCollection.Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(datasourceId, result);
        _mockDataCollectionRepository.Verify(x => x.RegisterAsync(It.IsAny<DataCollection>()), Times.Once);
        _mockSampleSensorRepository.Verify(x => x.RegisterAsync(It.IsAny<SampleSensor>()), Times.Once);
        _mockOutboxRepository.Verify(x => x.RegisterAsync(It.IsAny<OutboxMessage>()), Times.Once);
        _mockEventDeduplicator.Verify(x => x.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<TimeSpan>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleSampleSensors_RegistersAllSensorsAndEvents()
    {
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var dataSource = new DataSource(
            datasourceId,
            "Temperature Sensor",
            "http://sensor.api",
            "Sensor",
            "Temperature",
            "Hourly",
            tenantId
        );
        var sampleSensors = new List<SampleSensorDTO>
        {
            new()
            {
                Unit = "C",
                SensorValue = 25.5,
                Latitude = -23.5505,
                Longitude = -46.6333,
                RecordedAt = DateTime.UtcNow
            },
            new()
            {
                Unit = "C",
                SensorValue = 28.0,
                Latitude = -23.5506,
                Longitude = -46.6334,
                RecordedAt = DateTime.UtcNow
            }
        };
        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId))
            .Returns(dataSource);
        _mockEventDeduplicator.Setup(x => x.IsDuplicateAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockConfiguration.Setup(x => x["MinIO:BucketName"])
            .Returns("test-bucket");
        _mockMinioProvider.Setup(x => x.UploadJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UploadResultDTO
            {
                ETag = "fake-etag",
                Size = 123,
                ObjectName = "whatever.json",
                ResponseContent = "ok",
                ResponseStatusCode = HttpStatusCode.OK
            });

        var mockCollection = new Mock<IMongoCollection<ClimaticEventDetectedEvent>>();
        _mockReadDbContext.Setup(x => x.GetCollection<ClimaticEventDetectedEvent>("events"))
            .Returns(mockCollection.Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(datasourceId, result);
        _mockSampleSensorRepository.Verify(x => x.RegisterAsync(It.IsAny<SampleSensor>()), Times.Exactly(2));
        _mockOutboxRepository.Verify(x => x.RegisterAsync(It.IsAny<OutboxMessage>()), Times.Exactly(2));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Handle_WithValidFrequencyAndNoLastCollection_Succeeds()
    {
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var dataSource = new DataSource(
            datasourceId,
            "Temperature Sensor",
            "http://sensor.api",
            "Sensor",
            "Temperature",
            "Daily",
            tenantId
        );

        var sampleSensors = new List<SampleSensorDTO>
        {
            new()
            {
                Unit = "C",
                SensorValue = 25.5,
                Latitude = 0,
                Longitude = 0,
                RecordedAt = DateTime.UtcNow
            }
        };

        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId))
            .Returns(dataSource);
        _mockEventDeduplicator.Setup(x => x.IsDuplicateAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockConfiguration.Setup(x => x["MinIO:BucketName"])
            .Returns("test-bucket");
        _mockMinioProvider.Setup(x => x.UploadJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new UploadResultDTO
            {
                ETag = "fake-etag",
                Size = 123,
                ObjectName = "whatever.json",
                ResponseContent = "ok",
                ResponseStatusCode = HttpStatusCode.OK
            });

        var mockCollection = new Mock<IMongoCollection<ClimaticEventDetectedEvent>>();
        _mockReadDbContext.Setup(x => x.GetCollection<ClimaticEventDetectedEvent>("events"))
            .Returns(mockCollection.Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(datasourceId, result);
        _mockDataCollectionRepository.Verify(x => x.RegisterAsync(It.IsAny<DataCollection>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMinIOUploadFailure_PropagatesException()
    {
        var datasourceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var dataSource = new DataSource(
            datasourceId,
            "Temperature Sensor",
            "http://sensor.api",
            "Sensor",
            "Temperature",
            "Hourly",
            tenantId
        );
        var sampleSensors = new List<SampleSensorDTO>
        {
            new()
            {
                Unit = "C",
                SensorValue = 25.5,
                Latitude = 0,
                Longitude = 0,
                RecordedAt = DateTime.UtcNow
            }
        };
        var command = new RegisterSensorCollectionCommand
        {
            Payload = "{\"data\": \"test\"}",
            DatasourceId = datasourceId,
            TenantId = tenantId,
            CollectedAt = DateTime.UtcNow,
            SampleSensors = sampleSensors
        };

        _mockDataSourceRepository.Setup(x => x.GetByIdAndTenantId(datasourceId, tenantId))
            .Returns(dataSource);
        _mockEventDeduplicator.Setup(x => x.IsDuplicateAsync(It.IsAny<string>()))
            .ReturnsAsync(false);
        _mockConfiguration.Setup(x => x["MinIO:BucketName"])
            .Returns("test-bucket");
        _mockMinioProvider.Setup(x => x.UploadJsonAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("MinIO connection failed"));

        await Assert.ThrowsAsync<Exception>(() => _handler.Handle(command, CancellationToken.None));
    }

    #endregion
}