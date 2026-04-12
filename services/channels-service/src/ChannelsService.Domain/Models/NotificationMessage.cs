namespace ChannelsService.Domain.Models;

public sealed class NotificationMessage
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}