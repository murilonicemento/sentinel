using MediatR;
using Microsoft.Extensions.Logging;
using RiskEvaluation.Application.Commands;
using RiskEvaluation.Application.DTOs;
using RiskEvaluation.Domain.Entities;

namespace RiskEvaluation.Application.Handlers;

public class UpdateRiskModelCommandHandler : IRequestHandler<UpdateRiskModelCommand, UpdateRiskModelResponse>
{
    private readonly ILogger<UpdateRiskModelCommandHandler> _logger;

    public UpdateRiskModelCommandHandler(ILogger<UpdateRiskModelCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task<UpdateRiskModelResponse> Handle(UpdateRiskModelCommand request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return Task.FromResult(new UpdateRiskModelResponse
            {
                Success = false,
                Version = string.Empty,
                Message = "Request cannot be null"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            return Task.FromResult(new UpdateRiskModelResponse
            {
                Success = false,
                Version = request.Version,
                Message = "Version is required"
            });
        }

        if (string.IsNullOrWhiteSpace(request.Formula))
        {
            return Task.FromResult(new UpdateRiskModelResponse
            {
                Success = false,
                Version = request.Version,
                Message = "Formula is required"
            });
        }

        try
        {
            var riskModel = new RiskModel(
                request.Version,
                request.Parameters ?? new Dictionary<string, double>(),
                request.Formula);

            _logger.LogInformation($"Risk model updated to version {riskModel.Version}");

            return Task.FromResult(new UpdateRiskModelResponse
            {
                Success = true,
                Version = riskModel.Version,
                Message = $"Risk model updated to version {riskModel.Version}"
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to update risk model. Version: {Version}", request.Version);

            return Task.FromResult(new UpdateRiskModelResponse
            {
                Success = false,
                Version = request.Version,
                Message = $"Failed to update risk model: {ex.Message}"
            });
        }
    }
}