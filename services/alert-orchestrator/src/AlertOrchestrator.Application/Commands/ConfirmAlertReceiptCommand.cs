using MediatR;

namespace AlertOrchestrator.Application.Commands;

public sealed record ConfirmAlertReceiptCommand(
    Guid AlertWindowId,
    string ConfirmedBy,
    DateTime ConfirmedAt,
    string? Notes = null
) : IRequest;