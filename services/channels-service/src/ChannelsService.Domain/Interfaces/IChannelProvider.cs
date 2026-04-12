using ChannelsService.Domain.Enums;
using ChannelsService.Domain.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ChannelsService.Domain.Interfaces;

public interface IChannelProvider
{
    ChannelType ChannelType { get; }
    Task<DeliveryResult> SendAsync(NotificationEvent notification, CancellationToken cancellationToken);
}
