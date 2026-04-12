using ChannelsService.Domain.Models;

namespace ChannelsService.Application.Interfaces;

public interface IRetryPolicyEngine
{
    Task<DeliveryResult> ExecuteAsync(Func<Task<DeliveryResult>> sendFunc, int maxAttempts, CancellationToken cancellationToken);
}