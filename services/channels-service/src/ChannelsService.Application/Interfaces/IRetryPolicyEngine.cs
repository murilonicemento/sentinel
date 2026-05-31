using ChannelsService.Domain.Models;

namespace ChannelsService.Application.Interfaces;

public interface IRetryPolicyEngine
{
    Task<DeliveryResult> ExecuteAsync(Func<CancellationToken, Task<DeliveryResult>> sendFunc, int maxAttempts, ProviderResilienceOptions resilienceOptions, CancellationToken cancellationToken);
}