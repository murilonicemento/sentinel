using ChannelsService.Application.Services;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;
using Xunit;

namespace ChannelsService.UnitTests;

public sealed class FallbackExecutorTests
{
    [Fact]
    public void GetFallbackOrder_ReturnsRequestedChannelsOrderedByPriorityWhenFallbackDisabled()
    {
        var executor = new FallbackExecutor();

        var notification = new NotificationEvent
        {
            TenantId = "tenant-1",
            Channels = new List<ChannelType> { ChannelType.Email, ChannelType.Sms },
            FallbackEnabled = false
        };

        var settings = new TenantChannelSettings
        {
            TenantId = "tenant-1",
            EnabledChannels = new List<ChannelType> { ChannelType.Sms, ChannelType.Email, ChannelType.Push },
            PriorityOrder = new Dictionary<ChannelType, int>
            {
                [ChannelType.Sms] = 1,
                [ChannelType.Email] = 2,
                [ChannelType.Push] = 3
            },
            FallbackOrder = new List<ChannelType> { ChannelType.Sms, ChannelType.Push }
        };

        var result = executor.GetFallbackOrder(notification, settings);

        Assert.Equal(new[] { ChannelType.Sms, ChannelType.Email }, result);
    }

    [Fact]
    public void GetFallbackOrder_UsesFallbackOrderWhenEnabledAndNoRequestedChannels()
    {
        var executor = new FallbackExecutor();

        var notification = new NotificationEvent
        {
            TenantId = "tenant-2",
            FallbackEnabled = true
        };

        var settings = new TenantChannelSettings
        {
            TenantId = "tenant-2",
            EnabledChannels = new List<ChannelType> { ChannelType.Sms, ChannelType.Email, ChannelType.Push },
            PriorityOrder = new Dictionary<ChannelType, int>
            {
                [ChannelType.Sms] = 1,
                [ChannelType.Email] = 2,
                [ChannelType.Push] = 3
            },
            FallbackOrder = new List<ChannelType> { ChannelType.Push, ChannelType.Sms }
        };

        var result = executor.GetFallbackOrder(notification, settings);

        Assert.Equal(new[] { ChannelType.Push, ChannelType.Sms }, result);
    }
}