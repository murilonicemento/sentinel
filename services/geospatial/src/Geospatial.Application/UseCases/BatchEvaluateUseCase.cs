using Geospatial.Application.DTOs;
using Geospatial.Application.Mappers;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;

namespace Geospatial.Application.UseCases;

public class BatchEvaluateUseCase
{
    private readonly ContainsPointUseCase _containsPointUseCase;
    private readonly WithinRadiusUseCase _withinRadiusUseCase;

    public BatchEvaluateUseCase(ContainsPointUseCase containsPointUseCase, WithinRadiusUseCase withinRadiusUseCase)
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
                    throw new NotSupportedException($"Unknown evaluation type {eval.Type}");
            }
        }).ToList();

        return BatchResponseMapper.FromDomain(domainResults);
    }
}