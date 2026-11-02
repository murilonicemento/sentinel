using System.Security.Cryptography;
using System.Text;
using ChannelsService.Domain.Enums;

namespace ChannelsService.Application.DTOs;

/// <summary>
/// Payload of <c>tenant-usage-events</c>, consumed by tenants-billing. channels reports one unit of channel usage per
/// channel a notification was delivered on.
/// </summary>
public sealed record TenantUsageEventDTO(
    Guid TenantId,
    int EventsConsumed,
    int AlertsTriggered,
    int ApiRequests,
    int ChannelUsage,
    Guid EventId)
{
    /// <summary>
    /// The usage of one delivered channel. <c>EventId</c> derives from the tenant, the notification and the channel,
    /// so a redelivered notification is counted once per channel by tenants-billing. Null without a tenant id in
    /// GUID form or a notification id.
    /// </summary>
    public static TenantUsageEventDTO? ForDeliveredChannel(string? tenantId, string? notificationEventId,
        ChannelTypeEnum channel) =>
        Guid.TryParse(tenantId, out var tenant) && tenant != Guid.Empty && !string.IsNullOrWhiteSpace(notificationEventId)
            ? new TenantUsageEventDTO(tenant, 0, 0, 0, 1,
                DeterministicId($"channel-delivered:{tenant:N}:{notificationEventId}:{channel}"))
            : null;

    private static Guid DeterministicId(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80); // version 8
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 9562 variant
        return new Guid(bytes, bigEndian: true);
    }
}
