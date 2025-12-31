using MediatR;
using RiskCatalog.Application.DTO;

namespace RiskCatalog.Application.Queries;

public record GetEventTypesQuery : IRequest<List<EventTypeDTO>>
{
    public bool? IsActive { get; set; }
}