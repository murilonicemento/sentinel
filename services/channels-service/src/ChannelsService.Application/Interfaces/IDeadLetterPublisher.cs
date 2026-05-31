using ChannelsService.Domain.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ChannelsService.Application.Interfaces;

public interface IDeadLetterPublisher
{
    Task PublishAsync(NotificationEvent notification, DeliveryResult result, CancellationToken cancellationToken);
}
