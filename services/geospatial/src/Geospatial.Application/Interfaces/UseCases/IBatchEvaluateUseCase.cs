using Geospatial.Application.DTOs;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IBatchEvaluateUseCase
{
    Task<BatchResponseDTO> ExecuteAsync(BatchRequestDTO request, CancellationToken cancellationToken = default);
}