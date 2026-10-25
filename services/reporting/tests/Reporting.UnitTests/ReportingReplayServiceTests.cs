using Reporting.Application.Interfaces;
using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.UnitTests;

public class ReportingReplayServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequestReplay_WithoutRequester_PublishesAsAdmin(string? requestedBy)
    {
        var publisher = new RecordingReplayPublisher();
        var sut = new ReportingReplayService(publisher);

        var result = await sut.RequestReplayAsync("reporting-events-dead-letter", "{}", requestedBy);

        Assert.Equal("admin", publisher.LastRequestedBy);
        Assert.Equal("admin", result.RequestedBy);
    }

    [Fact]
    public async Task RequestReplay_WithRequester_ForwardsTopicPayloadAndRequester()
    {
        var publisher = new RecordingReplayPublisher();
        var sut = new ReportingReplayService(publisher);

        var result = await sut.RequestReplayAsync("reporting-events-dead-letter", "{\"eventId\":\"1\"}", "operator");

        Assert.Equal("reporting-events-dead-letter", publisher.LastSourceTopic);
        Assert.Equal("{\"eventId\":\"1\"}", publisher.LastPayload);
        Assert.Equal("operator", result.RequestedBy);
    }

    private sealed class RecordingReplayPublisher : IReportingReplayPublisher
    {
        public string? LastSourceTopic { get; private set; }
        public string? LastPayload { get; private set; }
        public string? LastRequestedBy { get; private set; }

        public Task<ReplayPublishResult> PublishAsync(
            string sourceTopic,
            string payload,
            string requestedBy,
            CancellationToken cancellationToken = default)
        {
            LastSourceTopic = sourceTopic;
            LastPayload = payload;
            LastRequestedBy = requestedBy;

            return Task.FromResult(new ReplayPublishResult("reporting-events-replay", 0, 42, sourceTopic, requestedBy));
        }
    }
}
