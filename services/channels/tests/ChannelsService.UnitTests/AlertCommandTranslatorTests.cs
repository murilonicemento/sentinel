using ChannelsService.Application.DTOs;
using ChannelsService.Application.Services;
using ChannelsService.Domain.Enums;
using Xunit;

namespace ChannelsService.UnitTests;

public sealed class AlertCommandTranslatorTests
{
    private static TriggerAlertCommandDTO Command(int level = 1, List<string>? channels = null, string? title = null) =>
        new(Guid.Parse("0b6f4c2e-5d1a-4e8b-9f3c-7a2d6e1b4c5f"), "rj-capital", "flood", 0.876, 3,
            new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), level, "tenant-a", channels, title);

    [Fact]
    public void Notification_IsIdentifiedByWindowAndEscalationLevel()
    {
        Assert.Equal("alert:0b6f4c2e5d1a4e8b9f3c7a2d6e1b4c5f:1", AlertCommandTranslator.ToNotification(Command()).EventId);
        Assert.Equal("alert:0b6f4c2e5d1a4e8b9f3c7a2d6e1b4c5f:2",
            AlertCommandTranslator.ToNotification(Command(level: 2)).EventId);
    }

    [Theory]
    [InlineData(1, NotificationPriorityEnum.High)]
    [InlineData(2, NotificationPriorityEnum.Critical)]
    [InlineData(3, NotificationPriorityEnum.Critical)]
    public void Priority_RisesWithTheEscalation(int level, NotificationPriorityEnum expected)
    {
        Assert.Equal(expected, AlertCommandTranslator.ToNotification(Command(level)).PriorityEnum);
    }

    [Fact]
    public void WithoutTargetChannels_LeavesTheChoiceToTheTenantSettings()
    {
        var notification = AlertCommandTranslator.ToNotification(Command());

        Assert.Empty(notification.Channels);
        Assert.Equal("Alerta de flood em rj-capital", notification.Message.Title);
        Assert.Contains("score 0.88", notification.Message.Body);
        Assert.Equal("tenant-a", notification.TenantId);
    }

    [Fact]
    public void TargetChannels_AreParsedAndUnknownNamesIgnored()
    {
        var notification = AlertCommandTranslator.ToNotification(Command(channels: ["sms", "Siren", "fax", "SMS"],
            title: "Evacuação"));

        Assert.Equal([ChannelTypeEnum.Sms, ChannelTypeEnum.Siren], notification.Channels);
        Assert.Equal("Evacuação", notification.Message.Title);
    }
}
