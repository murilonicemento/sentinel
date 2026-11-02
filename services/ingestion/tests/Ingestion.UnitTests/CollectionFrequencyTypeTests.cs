using Ingestion.Domain.ValueObjects;

namespace Ingestion.UnitTests;

public class CollectionFrequencyTypeTests
{
    [Theory]
    [InlineData(30, false)]
    [InlineData(90, true)]
    public void Hourly_MeasuresTheWindowInUtc(int minutesAgo, bool expected)
    {
        var lastCollected = DateTime.UtcNow.AddMinutes(-minutesAgo);

        Assert.Equal(expected, CollectionFrequencyType.From("Hourly").IsValidFrequency(lastCollected));
    }

    [Fact]
    public void UnspecifiedKind_IsTreatedAsUtc()
    {
        var lastCollected = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-30), DateTimeKind.Unspecified);

        Assert.False(CollectionFrequencyType.From("Hourly").IsValidFrequency(lastCollected));
    }

    [Fact]
    public void LocalKind_IsConvertedBeforeComparing()
    {
        var lastCollected = DateTime.UtcNow.AddMinutes(-30).ToLocalTime();

        Assert.False(CollectionFrequencyType.From("Hourly").IsValidFrequency(lastCollected));
    }
}
