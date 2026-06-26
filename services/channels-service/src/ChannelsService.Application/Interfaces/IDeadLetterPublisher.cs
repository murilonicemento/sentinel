using System.Threading;
using System.Threading.Tasks;
using ChannelsService.Application.DTOs;
using ChannelsService.Domain.Entities;
using ChannelsService.Domain.Events;

namespace ChannelsService.Application.Interfaces;

public interface IDeadLetterPublisher
{
    Task PublishAsync(NotificationEvent notification, DeliveryResultDTO resultDto, CancellationToken cancellationToken);
}
