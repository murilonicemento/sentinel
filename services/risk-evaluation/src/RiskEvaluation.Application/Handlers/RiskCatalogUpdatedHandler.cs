using MediatR;

namespace RiskEvaluation.Application.Handlers;

public class RiskCatalogUpdatedHandler : INotificationHandler<RiskCatalogUpdated>
{
    public Task Handle(RiskCatalogUpdated notification, CancellationToken cancellationToken)
    {
        // Update risk model if needed
        // For now, just log
        Console.WriteLine($"Risk model updated to version {notification.Version}");
        return Task.CompletedTask;
    }
}