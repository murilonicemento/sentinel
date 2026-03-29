using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Commands;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Domain.Entities;
using RiskEvaluation.Domain.Repositories;

namespace RiskEvaluation.Application.Handlers;

public class UpdateRiskModelCommandHandler : IRequestHandler<UpdateRiskModelCommand, UpdateRiskModelResponse>
{
    private readonly IRiskModelRepository _riskModelRepository;
    private readonly ILogger<UpdateRiskModelCommandHandler> _logger;

    public UpdateRiskModelCommandHandler(
        IRiskModelRepository riskModelRepository,
        ILogger<UpdateRiskModelCommandHandler> logger)
    {
        _riskModelRepository = riskModelRepository;
        _logger = logger;
    }

    public async Task<UpdateRiskModelResponse> Handle(UpdateRiskModelCommand request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new UpdateRiskModelResponse
            {
                Success = false,
                Version = string.Empty,
                Message = "Request cannot be null"
            };
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return new UpdateRiskModelResponse
            {
                Success = false,
                Version = request.Version,
                Message = "Version is required"
            };
        }

        if (string.IsNullOrWhiteSpace(request.Formula))
        {
            return new UpdateRiskModelResponse
            {
                Success = false,
                Version = request.Version,
                Message = "Formula is required"
            };
        }

        try
        {
            var riskModel = new RiskModel(
                request.Version,
                request.Parameters ?? new Dictionary<string, double>(),
                request.Formula);

            await _riskModelRepository.SaveAsync(riskModel, cancellationToken);

            _logger.LogInformation("Risk model updated to version {Version}", riskModel.Version);

            return new UpdateRiskModelResponse
            {
                Success = true,
                Version = riskModel.Version,
                Message = $"Risk model updated to version {riskModel.Version}"
            };
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to update risk model. Version: {Version}", request.Version);

            return new UpdateRiskModelResponse
            {
                Success = false,
                Version = request.Version,
                Message = $"Failed to update risk model: {ex.Message}"
            };
        }
    }
}