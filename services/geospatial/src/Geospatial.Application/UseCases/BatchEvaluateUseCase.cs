using Geospatial.Application.DTOs;
using Geospatial.Application.Interfaces.UseCases;
using Geospatial.Application.Mappers;
using Geospatial.Domain.Geometry;
using Geospatial.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Geospatial.Application.UseCases;

public class BatchEvaluateUseCase : IBatchEvaluateUseCase
{
    private readonly IContainsPointUseCase _containsPointUseCase;
    private readonly IWithinRadiusUseCase _withinRadiusUseCase;
    private readonly ILogger<BatchEvaluateUseCase> _logger;

    public BatchEvaluateUseCase(
        IContainsPointUseCase containsPointUseCase,
        IWithinRadiusUseCase withinRadiusUseCase,
        ILogger<BatchEvaluateUseCase> logger)
    {
        _containsPointUseCase = containsPointUseCase;
        _withinRadiusUseCase = withinRadiusUseCase;
        _logger = logger;
    }

    public async Task<BatchResponseDTO> ExecuteAsync(BatchRequestDTO request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[batch-evaluate] Input: {Count} evaluations | Timestamp: {Timestamp}",
            request.Evaluations?.Count ?? 0, DateTimeOffset.UtcNow);

        var domainRequests = BatchRequestMapper.ToDomain(request);
        var domainResults = new List<(string Type, object Result)>();

        foreach (var eval in domainRequests)
        {
            switch (eval.Type.ToLower())
            {
                case "contains-point":
                    var (area, point) = ((GeoPolygon, GeoPoint))eval.DomainPayload;
                    var contains = await _containsPointUseCase.ExecuteAsync(area, point, cancellationToken);
                    domainResults.Add((eval.Type, contains));
                    break;
                case "within-radius":
                    var (center, target, radius) = ((GeoPoint, GeoPoint, GeoRadius))eval.DomainPayload;
                    var withinResult = await _withinRadiusUseCase.ExecuteAsync(center, target, radius, cancellationToken);
                    domainResults.Add((eval.Type, withinResult));
                    break;
                default:
                    throw new ArgumentException($"Unknown evaluation type {eval.Type}");
            }
        }

        var response = BatchResponseMapper.FromDomain(domainResults);

        _logger.LogInformation("[batch-evaluate] Completed: {Count} results | Timestamp: {Timestamp}",
            domainResults.Count, DateTimeOffset.UtcNow);

        return response;
    }
}
