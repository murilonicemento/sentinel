using System.Threading;
using System.Threading.Tasks;
using ChannelsService.Domain.Entities;

namespace ChannelsService.Application.Interfaces;

public interface IDeadLetterPublisher
{
    Task PublishAsync(NotificationEvent notification, DeliveryResult result, CancellationToken cancellationToken);
}
