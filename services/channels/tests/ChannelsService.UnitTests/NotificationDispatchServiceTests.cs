using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Services;
using ChannelsService.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChannelsService.UnitTests;

public sealed class NotificationDispatchServiceTests
{
    [Fact]
    public async Task DispatchAsync_DoesNotPublishToDeadLetter_WhenDeliverySucceeds()
    {
        var notification = new NotificationEvent { EventId = "evt-ok", TenantId = "tenant-1" };
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var service = new NotificationDispatchService(
            new StubDeliveryService(new DeliveryResultDTO { Success = true, ProviderName = "sms-provider" }),
            deadLetterPublisher,
            NullLogger<NotificationDispatchService>.Instance);

        var result = await service.DispatchAsync(notification, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(deadLetterPublisher.Published);
    }

    [Fact]
    public async Task DispatchAsync_PublishesNotificationAndResultToDeadLetter_WhenDeliveryFails()
    {
        var notification = new NotificationEvent { EventId = "evt-fail", TenantId = "tenant-1" };
        var failure = new DeliveryResultDTO { Success = false, Error = "all providers failed" };
        var deadLetterPublisher = new RecordingDeadLetterPublisher();
        var service = new NotificationDispatchService(
            new StubDeliveryService(failure),
            deadLetterPublisher,
            NullLogger<NotificationDispatchService>.Instance);

        var result = await service.DispatchAsync(notification, CancellationToken.None);

        Assert.False(result.Success);
        var published = Assert.Single(deadLetterPublisher.Published);
        Assert.Same(notification, published.Notification);
        Assert.Same(failure, published.Result);
    }

    private sealed class StubDeliveryService(DeliveryResultDTO result) : IChannelDeliveryService
    {
        public Task<DeliveryResultDTO> DeliverAsync(NotificationEvent notification,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class RecordingDeadLetterPublisher : IDeadLetterPublisher
    {
        public List<(NotificationEvent Notification, DeliveryResultDTO Result)> Published { get; } = [];

        public Task PublishAsync(NotificationEvent notification, DeliveryResultDTO resultDto,
            CancellationToken cancellationToken)
        {
            Published.Add((notification, resultDto));
            return Task.CompletedTask;
        }
    }
}
