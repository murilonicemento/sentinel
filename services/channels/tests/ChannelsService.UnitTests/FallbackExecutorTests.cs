using ChannelsService.Application.DTOs;
using ChannelsService.Application.Services;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
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
            Channels = new List<ChannelTypeEnum> { ChannelTypeEnum.Email, ChannelTypeEnum.Sms },
            FallbackEnabled = false
        };

        var settings = new TenantChannelDTO
        {
            TenantId = "tenant-1",
            EnabledChannels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email, ChannelTypeEnum.Push },
            PriorityOrder = new Dictionary<ChannelTypeEnum, int>
            {
                [ChannelTypeEnum.Sms] = 1,
                [ChannelTypeEnum.Email] = 2,
                [ChannelTypeEnum.Push] = 3
            },
            FallbackOrder = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Push }
        };

        var result = executor.GetFallbackOrder(notification, settings);

        Assert.Equal(new[] { ChannelTypeEnum.Sms, ChannelTypeEnum.Email }, result);
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

        var settings = new TenantChannelDTO
        {
            TenantId = "tenant-2",
            EnabledChannels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email, ChannelTypeEnum.Push },
            PriorityOrder = new Dictionary<ChannelTypeEnum, int>
            {
                [ChannelTypeEnum.Sms] = 1,
                [ChannelTypeEnum.Email] = 2,
                [ChannelTypeEnum.Push] = 3
            },
            FallbackOrder = new List<ChannelTypeEnum> { ChannelTypeEnum.Push, ChannelTypeEnum.Sms }
        };

        var result = executor.GetFallbackOrder(notification, settings);

        Assert.Equal(new[] { ChannelTypeEnum.Push, ChannelTypeEnum.Sms }, result);
    }
}