using ChannelsService.Domain.Enums;
using System.Threading;
using System.Threading.Tasks;
using ChannelsService.Domain.Entities;

namespace ChannelsService.Domain.Interfaces;

public interface IChannelProvider
{
    ChannelTypeEnum ChannelTypeEnum { get; }
    string ProviderName { get; }
    Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken);
}
