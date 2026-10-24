namespace ChannelsService.Infrastructure.Options;

/// <summary>
/// Configuration options for ChannelsService infrastructure.
/// </summary>
public sealed record ChannelsServiceInfrastructureOptions
{
    /// <summary>
    /// Base URL for the Tenants Billing service.
    /// </summary>
    public string? TenantsBillingBaseUrl { get; init; } = "http://localhost:5055";
}
