using Geospatial.Application.DTOs;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.Mappers;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.UseCases;

public class BatchEvaluateUseCase : IBatchEvaluateUseCase
{
    private readonly IContainsPointUseCase _containsPointUseCase;
    private readonly IWithinRadiusUseCase _withinRadiusUseCase;

    public BatchEvaluateUseCase(IContainsPointUseCase containsPointUseCase, IWithinRadiusUseCase withinRadiusUseCase)
    {
        _containsPointUseCase = containsPointUseCase;
        _withinRadiusUseCase = withinRadiusUseCase;
    }

    public BatchResponseDTO Execute(BatchRequestDTO request)
    {
        var domainRequests = BatchRequestMapper.ToDomain(request);
        var domainResults = domainRequests.Select(eval =>
        {
            switch (eval.Type.ToLower())
            {
                case "contains-point":
                    var (area, point) = ((GeoPolygon, GeoPoint))eval.DomainPayload;
                    return (eval.Type, (object)_containsPointUseCase.Execute(area, point));
                case "within-radius":
                    var (center, target, radius) = ((GeoPoint, GeoPoint, GeoRadius))eval.DomainPayload;
                    return (eval.Type, (object)_withinRadiusUseCase.Execute(center, target, radius));
                default:
                    throw new ArgumentException($"Unknown evaluation type {eval.Type}");
            }
        }).ToList();

        return BatchResponseMapper.FromDomain(domainResults);
    }
}