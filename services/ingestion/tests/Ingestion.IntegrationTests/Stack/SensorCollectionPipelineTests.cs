using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Xunit;

namespace Ingestion.IntegrationTests.Stack;

/// <summary>A sensor collection from the HTTP request to Kafka: stores, transactional outbox and relay.</summary>
[Collection(IngestionStackCollection.Name)]
public class SensorCollectionPipelineTests(IngestionStackFactory stack)
{
    private static readonly TimeSpan RelayTimeout = TimeSpan.FromSeconds(60);

    private async Task<Guid> CreateRainDataSourceAsync()
    {
        var response = await stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA).PostAsJsonAsync(
            "/api/ingestion/data-source", new
            {
                tenantId = IngestionStackFactory.TenantA,
                name = $"rain-{Guid.NewGuid():N}",
                endpoint = "http://sensors.example.com/rain",
                dataSourceType = "Sensor",
                measurementType = "Rainfall",
                collectionFrequency = "Hourly",
                eventPermissions = new[] { new { eventDomain = "Climatic", eventType = "Rainfall" } }
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data")
            .GetProperty("dataSourceId").GetGuid();
    }

    private static object Collection(Guid dataSourceId, DateTime collectedAt) => new
    {
        datasourceId = dataSourceId,
        tenantId = IngestionStackFactory.TenantA,
        collectedAt,
        payload = "{\"station\":\"A-1\",\"rain\":60}",
        sampleSensors = new[]
        {
            new { sensorValue = 60.0, unit = "mm", latitude = -23.55, longitude = -46.63, recordedAt = collectedAt },
            new { sensorValue = 75.0, unit = "mm", latitude = -22.90, longitude = -43.20, recordedAt = collectedAt }
        }
    };

    private static DateTime CollectedAt() =>
        DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-5).AddTicks(-(DateTime.UtcNow.Ticks % TimeSpan.TicksPerSecond)),
            DateTimeKind.Utc);

    [Fact]
    public async Task Collection_CommitsCollectionSamplesAndOutboxTogether()
    {
        var dataSourceId = await CreateRainDataSourceAsync();

        var response = await stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA)
            .PostAsJsonAsync("/api/ingestion/sensor-collection", Collection(dataSourceId, CollectedAt()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var collectionId = await stack.ScalarAsync<Guid>(
            "SELECT id FROM data_collection WHERE data_source_id = @p", dataSourceId);
        Assert.Equal(2L, await stack.ScalarAsync<long>(
            "SELECT count(*) FROM sensor_sample WHERE data_collection_id = @p", collectionId));
        Assert.Equal(2L, await stack.ScalarAsync<long>(
            "SELECT count(*) FROM outbox WHERE aggregate_id = @p", collectionId));
        Assert.NotNull(await stack.ScalarAsync<string>(
            "SELECT trace_parent FROM outbox WHERE aggregate_id = @p LIMIT 1", collectionId));
    }

    [Fact]
    public async Task RepeatedCollection_DoesNotDuplicateRows()
    {
        var dataSourceId = await CreateRainDataSourceAsync();
        var collectedAt = CollectedAt();
        var client = stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA);

        (await client.PostAsJsonAsync("/api/ingestion/sensor-collection", Collection(dataSourceId, collectedAt)))
            .EnsureSuccessStatusCode();
        await client.PostAsJsonAsync("/api/ingestion/sensor-collection", Collection(dataSourceId, collectedAt));

        Assert.Equal(1L, await stack.ScalarAsync<long>(
            "SELECT count(*) FROM data_collection WHERE data_source_id = @p", dataSourceId));
        Assert.Equal(2L, await stack.ScalarAsync<long>(
            "SELECT count(*) FROM outbox o JOIN data_collection c ON c.id = o.aggregate_id WHERE c.data_source_id = @p",
            dataSourceId));
    }

    [Fact]
    public async Task Relay_PublishesEachEventOnceInTheRequestTraceAndMarksItProcessed()
    {
        var dataSourceId = await CreateRainDataSourceAsync();
        using var consumer = new ConsumerBuilder<Ignore, string>(new ConsumerConfig
        {
            BootstrapServers = stack.KafkaBootstrapServers,
            GroupId = $"ingestion-tests-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();
        consumer.Subscribe("climatic-event-detected");

        (await stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA)
                .PostAsJsonAsync("/api/ingestion/sensor-collection", Collection(dataSourceId, CollectedAt())))
            .EnsureSuccessStatusCode();
        var collectionId = await stack.ScalarAsync<Guid>(
            "SELECT id FROM data_collection WHERE data_source_id = @p", dataSourceId);
        var traceParent = await stack.ScalarAsync<string>(
            "SELECT trace_parent FROM outbox WHERE aggregate_id = @p LIMIT 1", collectionId);

        var received = new List<ConsumeResult<Ignore, string>>();
        var deadline = Stopwatch.StartNew();
        while (received.Count < 2 && deadline.Elapsed < RelayTimeout)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(1));
            if (result?.Message is not null &&
                JsonDocument.Parse(result.Message.Value).RootElement.GetProperty("CollectionId").GetGuid() ==
                collectionId)
                received.Add(result);
        }

        Assert.Equal(2, received.Count);
        var traceId = ActivityContext.Parse(traceParent!, null).TraceId;
        Assert.All(received, message =>
        {
            Assert.True(message.Message.Headers.TryGetLastBytes("traceparent", out var header));
            Assert.Equal(traceId, ActivityContext.Parse(Encoding.UTF8.GetString(header), null).TraceId);
        });
        Assert.Equal(0L, await stack.ScalarAsync<long>(
            "SELECT count(*) FROM outbox WHERE aggregate_id = @p AND NOT processed", collectionId));

        // A later relay pass must not publish the processed rows again.
        await Task.Delay(TimeSpan.FromSeconds(3));
        var again = consumer.Consume(TimeSpan.FromSeconds(2));
        Assert.True(again?.Message is null ||
                    JsonDocument.Parse(again.Message.Value).RootElement.GetProperty("CollectionId").GetGuid() !=
                    collectionId);
    }

    [Fact]
    public async Task ProjectedEvents_AreVisibleOnlyToTheirTenant()
    {
        var dataSourceId = await CreateRainDataSourceAsync();
        (await stack.ClientFor("Ingestion.Write", IngestionStackFactory.TenantA)
                .PostAsJsonAsync("/api/ingestion/sensor-collection", Collection(dataSourceId, CollectedAt())))
            .EnsureSuccessStatusCode();
        var collectionId = await stack.ScalarAsync<Guid>(
            "SELECT id FROM data_collection WHERE data_source_id = @p", dataSourceId);

        async Task<int> VisibleTo(Guid tenant)
        {
            var body = await stack.ClientFor("Ingestion.Read", tenant)
                .GetFromJsonAsync<JsonElement>("/api/v1/ingestion/detected-events?pageSize=50");
            return body.GetProperty("data").GetProperty("items").EnumerateArray()
                .Count(item => item.GetProperty("collectionId").GetGuid() == collectionId);
        }

        Assert.Equal(2, await VisibleTo(IngestionStackFactory.TenantA));
        Assert.Equal(0, await VisibleTo(IngestionStackFactory.TenantB));
    }
}
