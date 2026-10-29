using Geospatial.Api.Options;
using Geospatial.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Geospatial.IntegrationTests;

public class GeospatialOptionsValidationTests
{
    private static readonly Dictionary<string, string?> Valid = new()
    {
        ["Elasticsearch:Url"] = "http://elasticsearch:9200",
        ["KafkaConsumerOptions:TopicName"] = "sensor-event-detected",
        ["KafkaConsumerOptions:DeadLetterTopic"] = "geospatial-dead-letter",
        ["KafkaProducerOptions:RegionIntersectedTopic"] = "region-intersected"
    };

    private static ServiceProvider Provider(string key, string? value)
    {
        var settings = new Dictionary<string, string?>(Valid) { [key] = value };
        return new ServiceCollection()
            .AddGeospatialOptions(new ConfigurationBuilder().AddInMemoryCollection(settings).Build())
            .BuildServiceProvider();
    }

    [Fact]
    public void ValidSettings_Bind()
    {
        using var provider = Provider("Elasticsearch:Url", "http://elasticsearch:9200");

        Assert.Equal("sensor-event-detected",
            provider.GetRequiredService<IOptions<KafkaConsumerOptions>>().Value.TopicName);
    }

    [Theory]
    [InlineData("Elasticsearch:Url", "not-a-url")]
    [InlineData("KafkaConsumerOptions:TopicName", "")]
    [InlineData("KafkaConsumerOptions:DeadLetterTopic", "")]
    [InlineData("KafkaProducerOptions:RegionIntersectedTopic", "")]
    public void InvalidSettings_FailWithTheKeyName(string key, string value)
    {
        using var provider = Provider(key, value);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IStartupValidator>().Validate());
        Assert.Contains(key, exception.Message);
    }
}
