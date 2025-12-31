using MediatR;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Queries;

public record GetEventTypeByCodeQuery : IRequest<EventTypeDTO?>
{
    public string EventTypeCode { get; set; }
}