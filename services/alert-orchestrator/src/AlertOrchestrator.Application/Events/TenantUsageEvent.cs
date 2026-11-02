using System.Security.Cryptography;
using System.Text;

namespace AlertOrchestrator.Application.Events;

/// <summary>
/// Payload of <c>tenant-usage-events</c>, consumed by tenants-billing. alert-orchestrator reports one triggered alert
/// per alert window.
/// </summary>
public sealed record TenantUsageEvent(
    Guid TenantId,
    int EventsConsumed,
    int AlertsTriggered,
    int ApiRequests,
    int ChannelUsage,
    Guid EventId)
{
    /// <summary>
    /// The usage of a window's trigger. <c>EventId</c> derives from the window, so a republished trigger is counted
    /// once by tenants-billing. Null when the window has no tenant id in GUID form.
    /// </summary>
    public static TenantUsageEvent? ForTriggeredAlert(string? tenantId, Guid alertWindowId) =>
        Guid.TryParse(tenantId, out var tenant) && tenant != Guid.Empty
            ? new TenantUsageEvent(tenant, 0, 1, 0, 0, DeterministicId($"alert-triggered:{alertWindowId:N}"))
            : null;

    private static Guid DeterministicId(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // version 8
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
        return new Guid(bytes, bigEndian: true);
    }
}
