using System.Security.Claims;
using ChannelsService.Api.Controllers;
using ChannelsService.Api.Security;
using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Services;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

namespace ChannelsService.UnitTests;

public sealed class TenantAuthorizationTests
{
    private const string UserIssuer = "sentinel";

    private static readonly TenantAccessEvaluator TenantAccess =
        new(Options.Create(new JwtOptions { Issuer = UserIssuer }));

    private static ControllerContext ContextFor(string issuer, params string[] tenants)
    {
        var claims = new List<Claim> { new("sub", "caller"), new("iss", issuer) };
        claims.AddRange(tenants.Select(tenant => new Claim("tenant_id", tenant)));

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
            }
        };
    }

    [Fact]
    public async Task PostNotification_ForTenantNotInClaims_ReturnsForbidAndDoesNotDispatch()
    {
        var dispatch = new RecordingDispatchService();
        var controller = new NotificationsController(dispatch, new StubAttemptQueryService(), TenantAccess)
        {
            ControllerContext = ContextFor(UserIssuer, "tenant-a")
        };

        var result = await controller.Post(new NotificationEvent { EventId = "evt-1", TenantId = "tenant-b" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(dispatch.Dispatched);
    }

    [Fact]
    public async Task PostNotification_ForOwnTenant_Dispatches()
    {
        var dispatch = new RecordingDispatchService();
        var controller = new NotificationsController(dispatch, new StubAttemptQueryService(), TenantAccess)
        {
            ControllerContext = ContextFor(UserIssuer, "TENANT-A")
        };

        var result = await controller.Post(new NotificationEvent { EventId = "evt-1", TenantId = "tenant-a" },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(dispatch.Dispatched);
    }

    [Fact]
    public async Task ReprocessDeadLetter_ForTenantNotInClaims_ReturnsForbidAndDoesNotPublish()
    {
        var publisher = new RecordingNotificationPublisher();
        var controller = new DeadLetterController(publisher, TenantAccess)
        {
            ControllerContext = ContextFor(UserIssuer, "tenant-a")
        };

        var result = await controller.Reprocess(new NotificationEvent { EventId = "evt-1", TenantId = "tenant-b" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task DeliveryAttemptQuery_ReturnsOnlyAttemptsOfScopedTenants()
    {
        var repository = new StubDeliveryRepository(
            new DeliveryAttempt { EventId = "evt-1", TenantId = "tenant-a" },
            new DeliveryAttempt { EventId = "evt-1", TenantId = "tenant-b" });
        var service = new DeliveryAttemptQueryService(repository);

        var tenantScoped = await service.GetByEventAsync("evt-1", TenantAccessScope.ForTenants(["tenant-a"]),
            CancellationToken.None);
        var platform = await service.GetByEventAsync("evt-1", TenantAccessScope.Platform, CancellationToken.None);

        Assert.Equal("tenant-a", Assert.Single(tenantScoped).TenantId);
        Assert.Equal(2, platform.Count);
    }

    private sealed class RecordingDispatchService : INotificationDispatchService
    {
        public List<NotificationEvent> Dispatched { get; } = [];

        public Task<DeliveryResultDTO> DispatchAsync(NotificationEvent notification,
            CancellationToken cancellationToken)
        {
            Dispatched.Add(notification);
            return Task.FromResult(new DeliveryResultDTO { Success = true });
        }
    }

    private sealed class StubAttemptQueryService : IDeliveryAttemptQueryService
    {
        public Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId, TenantAccessScope scope,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DeliveryAttempt>>([]);

        public Task<PagedResultDTO<DeliveryAttemptDTO>> GetPageByEventAsync(string eventId, TenantAccessScope scope,
            int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResultDTO<DeliveryAttemptDTO> { Items = [], Page = page, PageSize = pageSize });
    }

    private sealed class RecordingNotificationPublisher : INotificationPublisher
    {
        public List<NotificationEvent> Published { get; } = [];

        public Task PublishAsync(NotificationEvent notification, CancellationToken cancellationToken)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class StubDeliveryRepository(params DeliveryAttempt[] attempts) : IDeliveryRepository
    {
        public Task AddAsync(DeliveryAttempt attempt) => Task.CompletedTask;

        public Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId) =>
            Task.FromResult<IReadOnlyList<DeliveryAttempt>>(attempts.Where(a => a.EventId == eventId).ToList());
    }
}
