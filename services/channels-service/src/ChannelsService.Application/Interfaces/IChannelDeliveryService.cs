using ChannelsService.Domain.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ChannelsService.Application.Interfaces;

public interface IChannelDeliveryService
{
    Task<DeliveryResult> DeliverAsync(NotificationEvent notification, CancellationToken cancellationToken);
}