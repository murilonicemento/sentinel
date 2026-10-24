using ChannelsService.Application.DTOs;
using ChannelsService.Application.Interfaces;
using ChannelsService.Application.Interfaces.Services;
using ChannelsService.Application.Services;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Events;
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
            Channels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms }
        };

        var settings = new TenantChannelDTO
        {
            TenantId = "tenant-1",
            EnabledChannels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms },
            PriorityOrder = new Dictionary<ChannelTypeEnum, int> { [ChannelTypeEnum.Sms] = 1 },
            MaxRetries = new Dictionary<ChannelTypeEnum, int> { [ChannelTypeEnum.Sms] = 1 }
        };

        var provider = new TestChannelProvider(ChannelTypeEnum.Sms, true, "sms-provider");
        var repository = new InMemoryDeliveryRepository();
        var service = new ChannelDeliveryService(
            new[] { provider },
            repository,
            new StubTenantChannelSettingsProvider(settings),
            new NoOpRetryPolicyEngine(),
            new StubFallbackExecutor(new[] { ChannelTypeEnum.Sms }),
            new NoOpChannelMetrics(),
            NullLogger<ChannelDeliveryService>.Instance,
            new StubTenantBillingGateway(isActive: true));

        var result = await service.DeliverAsync(notification, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("sms-provider", result.ProviderName);
        Assert.Single(repository.Attempts);
        Assert.Equal(DeliveryStatusEnum.Sent, repository.Attempts[0].StatusEnum);
        Assert.Equal(ChannelTypeEnum.Sms, repository.Attempts[0].Channel);
    }

    [Fact]
    public async Task DeliverAsync_AttemptsAllChannels_WhenMultipleChannelsConfigured()
    {
        var notification = new NotificationEvent
        {
            EventId = "evt-2",
            TenantId = "tenant-2",
            Channels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email }
        };

        var settings = new TenantChannelDTO
        {
            TenantId = "tenant-2",
            EnabledChannels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email },
            PriorityOrder = new Dictionary<ChannelTypeEnum, int>
            {
                [ChannelTypeEnum.Sms] = 1,
                [ChannelTypeEnum.Email] = 2
            },
            MaxRetries = new Dictionary<ChannelTypeEnum, int>
            {
                [ChannelTypeEnum.Sms] = 1,
                [ChannelTypeEnum.Email] = 1
            }
        };

        var smsProvider = new TestChannelProvider(ChannelTypeEnum.Sms, true, "sms-provider");
        var emailProvider = new TestChannelProvider(ChannelTypeEnum.Email, true, "email-provider");
        var repository = new InMemoryDeliveryRepository();
        var service = new ChannelDeliveryService(
            new IChannelProvider[] { smsProvider, emailProvider },
            repository,
            new StubTenantChannelSettingsProvider(settings),
            new NoOpRetryPolicyEngine(),
            new StubFallbackExecutor(new[] { ChannelTypeEnum.Sms, ChannelTypeEnum.Email }),
            new NoOpChannelMetrics(),
            NullLogger<ChannelDeliveryService>.Instance,
            new StubTenantBillingGateway(isActive: true));

        var result = await service.DeliverAsync(notification, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("sms-provider,email-provider", result.ProviderName);
        Assert.Equal(2, repository.Attempts.Count);
        Assert.All(repository.Attempts, attempt => Assert.Equal(DeliveryStatusEnum.Sent, attempt.StatusEnum));
    }

    [Fact]
    public async Task DeliverAsync_ReturnsSuccess_WhenSomeChannelsFailAndSomeSucceed()
    {
        var notification = new NotificationEvent
        {
            EventId = "evt-3",
            TenantId = "tenant-3",
            Channels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email }
        };

        var settings = new TenantChannelDTO
        {
            TenantId = "tenant-3",
            EnabledChannels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email },
            PriorityOrder = new Dictionary<ChannelTypeEnum, int>
            {
                [ChannelTypeEnum.Sms] = 1,
                [ChannelTypeEnum.Email] = 2
            },
            MaxRetries = new Dictionary<ChannelTypeEnum, int>
            {
                [ChannelTypeEnum.Sms] = 1,
                [ChannelTypeEnum.Email] = 1
            }
        };

        var smsProvider = new TestChannelProvider(ChannelTypeEnum.Sms, false, "sms-provider");
        var emailProvider = new TestChannelProvider(ChannelTypeEnum.Email, true, "email-provider");
        var repository = new InMemoryDeliveryRepository();
        var service = new ChannelDeliveryService(
            new IChannelProvider[] { smsProvider, emailProvider },
            repository,
            new StubTenantChannelSettingsProvider(settings),
            new NoOpRetryPolicyEngine(),
            new StubFallbackExecutor(new[] { ChannelTypeEnum.Sms, ChannelTypeEnum.Email }),
            new NoOpChannelMetrics(),
            NullLogger<ChannelDeliveryService>.Instance,
            new StubTenantBillingGateway(isActive: true));

        var result = await service.DeliverAsync(notification, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains("email-provider", result.ProviderName);
        Assert.Equal(2, repository.Attempts.Count);
        Assert.Equal(ChannelTypeEnum.Sms, repository.Attempts[0].Channel);
        Assert.Equal(DeliveryStatusEnum.Failed, repository.Attempts[0].StatusEnum);
        Assert.Equal(ChannelTypeEnum.Email, repository.Attempts[1].Channel);
        Assert.Equal(DeliveryStatusEnum.Sent, repository.Attempts[1].StatusEnum);
    }

    [Fact]
    public async Task DeliverAsync_ReturnsFailure_WhenAllChannelsFail()
    {
        var notification = new NotificationEvent
        {
            EventId = "evt-4",
            TenantId = "tenant-4",
            Channels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email }
        };

        var settings = new TenantChannelDTO
        {
            TenantId = "tenant-4",
            EnabledChannels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms, ChannelTypeEnum.Email },
            PriorityOrder = new Dictionary<ChannelTypeEnum, int>
            {
                [ChannelTypeEnum.Sms] = 1,
                [ChannelTypeEnum.Email] = 2
            },
            MaxRetries = new Dictionary<ChannelTypeEnum, int>
            {
                [ChannelTypeEnum.Sms] = 1,
                [ChannelTypeEnum.Email] = 1
            }
        };

        var smsProvider = new TestChannelProvider(ChannelTypeEnum.Sms, false, "sms-provider");
        var emailProvider = new TestChannelProvider(ChannelTypeEnum.Email, false, "email-provider");
        var repository = new InMemoryDeliveryRepository();
        var service = new ChannelDeliveryService(
            new IChannelProvider[] { smsProvider, emailProvider },
            repository,
            new StubTenantChannelSettingsProvider(settings),
            new NoOpRetryPolicyEngine(),
            new StubFallbackExecutor(new[] { ChannelTypeEnum.Sms, ChannelTypeEnum.Email }),
            new NoOpChannelMetrics(),
            NullLogger<ChannelDeliveryService>.Instance,
            new StubTenantBillingGateway(isActive: true));

        var result = await service.DeliverAsync(notification, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("All configured channels failed", result.Error);
        Assert.Equal(2, repository.Attempts.Count);
        Assert.All(repository.Attempts, attempt => Assert.Equal(DeliveryStatusEnum.Failed, attempt.StatusEnum));
    }

    [Fact]
    public async Task DeliverAsync_ReturnsFailure_WhenTenantIsNotActive()
    {
        var notification = new NotificationEvent
        {
            EventId = "evt-5",
            TenantId = "inactive-tenant",
            Channels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms }
        };

        var settings = new TenantChannelDTO
        {
            TenantId = "inactive-tenant",
            EnabledChannels = new List<ChannelTypeEnum> { ChannelTypeEnum.Sms },
            PriorityOrder = new Dictionary<ChannelTypeEnum, int> { [ChannelTypeEnum.Sms] = 1 },
            MaxRetries = new Dictionary<ChannelTypeEnum, int> { [ChannelTypeEnum.Sms] = 1 }
        };

        var provider = new TestChannelProvider(ChannelTypeEnum.Sms, true, "sms-provider");
        var repository = new InMemoryDeliveryRepository();
        var service = new ChannelDeliveryService(
            new[] { provider },
            repository,
            new StubTenantChannelSettingsProvider(settings),
            new NoOpRetryPolicyEngine(),
            new StubFallbackExecutor(new[] { ChannelTypeEnum.Sms }),
            new NoOpChannelMetrics(),
            NullLogger<ChannelDeliveryService>.Instance,
            new StubTenantBillingGateway(isActive: false));

        var result = await service.DeliverAsync(notification, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not active or not authorized", result.Error);
        Assert.Empty(repository.Attempts);
    }

    private sealed class TestChannelProvider : IChannelProvider
    {
        private readonly ChannelTypeEnum _channelTypeEnum;
        private readonly bool _shouldSucceed;
        private readonly string _providerName;

        public TestChannelProvider(ChannelTypeEnum channelTypeEnum, bool shouldSucceed, string providerName)
        {
            _channelTypeEnum = channelTypeEnum;
            _shouldSucceed = shouldSucceed;
            _providerName = providerName;
        }

        public ChannelTypeEnum ChannelTypeEnum => _channelTypeEnum;
        public string ProviderName => _providerName;

        public Task<DeliveryResultDTO> SendAsync(NotificationEvent notification, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DeliveryResultDTO
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
        private readonly TenantChannelDTO _dto;

        public StubTenantChannelSettingsProvider(TenantChannelDTO dto)
        {
            _dto = dto;
        }

        public TenantChannelDTO GetSettings(string tenantId)
        {
            return _dto;
        }
    }

    private sealed class StubFallbackExecutor : IFallbackExecutor
    {
        private readonly IReadOnlyList<ChannelTypeEnum> _channelOrder;

        public StubFallbackExecutor(IReadOnlyList<ChannelTypeEnum> channelOrder)
        {
            _channelOrder = channelOrder;
        }

        public IReadOnlyList<ChannelTypeEnum> GetFallbackOrder(NotificationEvent notification,
            TenantChannelDTO dto)
        {
            return _channelOrder;
        }
    }

    private sealed class NoOpRetryPolicyEngine : IRetryPolicyEngine
    {
        public Task<DeliveryResultDTO> ExecuteAsync(Func<CancellationToken, Task<DeliveryResultDTO>> sendFunc,
            int maxAttempts, ProviderResilienceDTO resilienceDto, CancellationToken cancellationToken)
        {
            return sendFunc(cancellationToken);
        }
    }

    private sealed class NoOpChannelMetrics : IChannelMetrics
    {
        public void RecordNotificationDelivery(bool success, int attemptedChannelCount, int failedChannelCount)
        {
        }

        public void RecordChannelAttempt(ChannelTypeEnum channel, string providerName, bool success, bool retried, int attemptCount)
        {
        }

        public void RecordChannelLatency(ChannelTypeEnum channel, string providerName, double durationSeconds, string status)
        {
        }

        public void RecordDeadLetterPublished(bool success)
        {
        }
    }

    private sealed class StubTenantBillingGateway : ITenantBillingGateway
    {
        private readonly bool _isActive;

        public StubTenantBillingGateway(bool isActive = true)
        {
            _isActive = isActive;
        }

        public Task<bool> ValidateTenantAsync(string tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_isActive);
        }
    }
}