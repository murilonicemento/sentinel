using System.Text.Json;
using Ingestion.Application.Events;
using Xunit;

namespace Ingestion.UnitTests;

/// <summary>
/// <c>SensorEventDetected</c> as the outbox publishes it on <c>disaster-event-detected</c> and
/// <c>climatic-event-detected</c>. geospatial's <c>SensorEventDetectedConsumerTests</c> indexes the same sample.
/// </summary>
public class SensorEventDetectedContractTests
{
    private const string Sample = """
        {"CollectionId":"1b2c3d4e-5f60-4718-8a9b-0c1d2e3f4a5b","EventType":"Rainfall","Intensity":7.5,"Latitude":-22.9068,"Longitude":-43.1729,"CollectedAt":"2026-11-01T12:00:00Z","EventId":"8f3a5c1e-2d4b-8e6f-9a1b-3c5d7e9f1a2b","TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f"}
        """;

    [Fact]
    public void SensorEvent_SerializesToTheContract()
    {
        var evt = new SensorEventDetected(Guid.Parse("1b2c3d4e-5f60-4718-8a9b-0c1d2e3f4a5b"), "Rainfall", 7.5, -22.9068,
            -43.1729, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
            Guid.Parse("8f3a5c1e-2d4b-8e6f-9a1b-3c5d7e9f1a2b"), Guid.Parse("6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f"));

        Assert.Equal(Sample.Trim(), JsonSerializer.Serialize(evt));
    }
}
