using System.Text.Json;
using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
using Xunit;

namespace ChannelsService.UnitTests.Contracts;

/// <summary>
/// <c>reporting-events</c> as reporting reads it (X-24). The same sample is read by reporting's
/// <c>ProducerContractTests</c>.
/// </summary>
public class ReportingEventContractTests
{
    private const string Sample = """
        {"EventId":"NotificationSent:6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f:alert:3c9a1f0e5b7d4e2a8f6c1d0b9a8e7f6d:1:sms","EventType":"NotificationSent","TenantId":"6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f","Region":"rj-capital","Severity":"HIGH","Channel":"sms","RiskScore":0.84,"Status":"SUCCESS","Timestamp":"2026-11-01T12:00:05Z","Source":"channels"}
        """;

    [Fact]
    public void DeliveredAlertNotification_SerializesToTheContract()
    {
        var notification = new NotificationEvent
        {
            EventId = "alert:3c9a1f0e5b7d4e2a8f6c1d0b9a8e7f6d:1",
            TenantId = "6d3f1c8e-2b4a-4f1e-9c7d-1a2b3c4d5e6f",
            PriorityEnum = NotificationPriorityEnum.High,
            Metadata = new Dictionary<string, string> { ["region"] = "rj-capital", ["riskScore"] = "0.84" }
        };

        var reportingEvent = ReportingEventEnvelopeDTO.ForDelivery(notification, ChannelTypeEnum.Sms, delivered: true,
            new DateTime(2026, 11, 1, 12, 0, 5, DateTimeKind.Utc));

        Assert.Equal(Sample.Trim(), JsonSerializer.Serialize(reportingEvent));
    }
}
