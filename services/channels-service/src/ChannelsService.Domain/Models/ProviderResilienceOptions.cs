namespace ChannelsService.Domain.Models;

public sealed record ProviderResilienceOptions
{
    public int TimeoutSeconds { get; init; }
    public int CircuitBreakerFailureThreshold { get; init; } = 3;
    public int CircuitBreakerDurationSeconds { get; init; } = 30;
    public int CircuitBreakerSamplingDurationSeconds { get; init; } = 60;
}
