using MediatR;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Queries;

public record GetEventTypesQuery : IRequest<EventTypeDTO>
{
    public bool? IsActive { get; set; }
}