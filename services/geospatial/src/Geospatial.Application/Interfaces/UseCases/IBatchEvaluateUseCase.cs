using Geospatial.Application.DTOs;

namespace Geospatial.Application.Interfaces.UseCases;

public interface IBatchEvaluateUseCase
{
    public BatchResponseDTO Execute(BatchRequestDTO request);
}