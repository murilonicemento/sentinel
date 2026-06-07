using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Services;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Interfaces;
using ChannelsService.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChannelsService.UnitTests;

public sealed class ChannelDeliveryServiceTests
{
    [Fact]
    public async Task DeliverAsync_ReturnsSuccess_WhenFirstProviderSucceeds()
    {
        var notification = new NotificationEvent
        {
            EventId = "evt-1",
            TenantId = "tenant-1",
            Channels = new List<ChannelType> { ChannelType.Sms }
        };

        var settings = new TenantChannelSettings
        {
            TenantId = "tenant-1",
            EnabledChannels = new List<ChannelType> { ChannelType.Sms },
            PriorityOrder = new Dictionary<ChannelType, int> { [ChannelType.Sms] = 1 },
            MaxRetries = new Dictionary<ChannelType, int> { [ChannelType.Sms] = 1 }
        };

        var provider = new TestChannelProvider(ChannelType.Sms, true, "sms-provider");
        var repository = new InMemoryDeliveryRepository();
        var service = new ChannelDeliveryService(
            new[] { provider },
            repository,
            new StubTenantChannelSettingsProvider(settings),
            new NoOpRetryPolicyEngine(),
            new StubFallbackExecutor(new[] { ChannelType.Sms }),
            NullLogger<ChannelDeliveryService>.Instance);

        var result = await service.DeliverAsync(notification, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("sms-provider", result.ProviderName);
        Assert.Single(repository.Attempts);
        Assert.Equal(DeliveryStatus.Sent, repository.Attempts[0].Status);
        Assert.Equal(ChannelType.Sms, repository.Attempts[0].Channel);
    }

    [Fact]
    public async Task DeliverAsync_FallsBackToNextProvider_WhenFirstProviderFails()
    {
        var notification = new NotificationEvent
        {
            EventId = "evt-2",
            TenantId = "tenant-2",
            Channels = new List<ChannelType> { ChannelType.Sms, ChannelType.Email }
        };

        var settings = new TenantChannelSettings
        {
            TenantId = "tenant-2",
            EnabledChannels = new List<ChannelType> { ChannelType.Sms, ChannelType.Email },
            PriorityOrder = new Dictionary<ChannelType, int>
            {
                [ChannelType.Sms] = 1,
                [ChannelType.Email] = 2
            },
            MaxRetries = new Dictionary<ChannelType, int>
            {
                [ChannelType.Sms] = 1,
                [ChannelType.Email] = 1
            }
        };

        var smsProvider = new TestChannelProvider(ChannelType.Sms, false, "sms-provider");
        var emailProvider = new TestChannelProvider(ChannelType.Email, true, "email-provider");
        var repository = new InMemoryDeliveryRepository();
        var service = new ChannelDeliveryService(
            new IChannelProvider[] { smsProvider, emailProvider },
            repository,
            new StubTenantChannelSettingsProvider(settings),
            new NoOpRetryPolicyEngine(),
            new StubFallbackExecutor(new[] { ChannelType.Sms, ChannelType.Email }),
            NullLogger<ChannelDeliveryService>.Instance);

        var result = await service.DeliverAsync(notification, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("email-provider", result.ProviderName);
        Assert.Equal(2, repository.Attempts.Count);
        Assert.Equal(ChannelType.Sms, repository.Attempts[0].Channel);
        Assert.Equal(DeliveryStatus.Failed, repository.Attempts[0].Status);
        Assert.Equal(ChannelType.Email, repository.Attempts[1].Channel);
        Assert.Equal(DeliveryStatus.Sent, repository.Attempts[1].Status);
    }

    private sealed class TestChannelProvider : IChannelProvider
    {
        private readonly ChannelType _channelType;
        private readonly bool _shouldSucceed;
        private readonly string _providerName;

        public TestChannelProvider(ChannelType channelType, bool shouldSucceed, string providerName)
        {
            _channelType = channelType;
            _shouldSucceed = shouldSucceed;
            _providerName = providerName;
        }

        public ChannelType ChannelType => _channelType;
        public string ProviderName => _providerName;

        public Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DeliveryResult
            {
                Success = _shouldSucceed,
                ProviderName = _providerName,
                Error = _shouldSucceed ? null : "simulated failure"
            });
        }
    }

    private sealed class InMemoryDeliveryRepository : IDeliveryRepository
    {
        private readonly List<DeliveryAttempt> _attempts = new();

        public IReadOnlyList<DeliveryAttempt> Attempts => _attempts;

        public Task AddAsync(DeliveryAttempt attempt)
        {
            _attempts.Add(attempt);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeliveryAttempt>> GetByEventAsync(string eventId)
        {
            return Task.FromResult((IReadOnlyList<DeliveryAttempt>)_attempts.Where(a => a.EventId == eventId).ToList());
        }
    }

    private sealed class StubTenantChannelSettingsProvider : ITenantChannelSettingsProvider
    {
        private readonly TenantChannelSettings _settings;

        public StubTenantChannelSettingsProvider(TenantChannelSettings settings)
        {
            _settings = settings;
        }

        public TenantChannelSettings GetSettings(string tenantId)
        {
            return _settings;
        }
    }

    private sealed class StubFallbackExecutor : IFallbackExecutor
    {
        private readonly IReadOnlyList<ChannelType> _channelOrder;

        public StubFallbackExecutor(IReadOnlyList<ChannelType> channelOrder)
        {
            _channelOrder = channelOrder;
        }

        public IReadOnlyList<ChannelType> GetFallbackOrder(NotificationEvent notification,
            TenantChannelSettings settings)
        {
            return _channelOrder;
        }
    }

    private sealed class NoOpRetryPolicyEngine : IRetryPolicyEngine
    {
        public Task<DeliveryResult> ExecuteAsync(Func<CancellationToken, Task<DeliveryResult>> sendFunc,
            int maxAttempts, ProviderResilienceOptions resilienceOptions, CancellationToken cancellationToken)
        {
            return sendFunc(cancellationToken);
        }
    }
}