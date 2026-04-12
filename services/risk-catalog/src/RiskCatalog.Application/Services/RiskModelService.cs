using System.Text.Json;
using Microsoft.Extensions.Logging;
using RiskCatalog.Application.DTO;
using RiskCatalog.Application.Events;
using RiskCatalog.Application.Interfaces;
using RiskCatalog.Application.Services.Interfaces;
using RiskCatalog.Domain.Enums;
using RiskCatalog.Domain.IRepositories;
using RiskCatalog.Domain.RiskModels;
using PolygonDTO = RiskCatalog.Application.DTO.PolygonDTO;

namespace RiskCatalog.Application.Services;

public class RiskModelService : IRiskModelService
{
    private readonly IEventTypeRepository _eventTypeRepository;
    private readonly IRiskMatrixRepository _riskMatrixRepository;
    private readonly IIDFCurveRepository _IDFCurveRepository;
    private readonly IRegionalParameterRepository _regionalParameterRepository;
    private readonly ICacheService _cacheService;
    private readonly IPublisher _publisher;
    private readonly IGeospatialValidationService _geospatialValidationService;
    private readonly ILogger<RiskModelService> _logger;
    private const string RiskMatrixCachePrefix = "risk-matrix";
    private const string IDFCurveCachePrefix = "idf-curve";
    private const string RegionalParameterCachePrefix = "regional-parameter";
    private const string RiskCatalogPublishedTopic = "risk-catalog-published";

    public RiskModelService(
        IEventTypeRepository eventTypeRepository,
        IRiskMatrixRepository riskMatrixRepository,
        IIDFCurveRepository idfCurveRepository,
        IRegionalParameterRepository regionalParameterRepository,
        ICacheService cacheService,
        IPublisher publisher,
        IGeospatialValidationService geospatialValidationService,
        ILogger<RiskModelService> logger)
    {
        _eventTypeRepository = eventTypeRepository;
        _riskMatrixRepository = riskMatrixRepository;
        _IDFCurveRepository = idfCurveRepository;
        _regionalParameterRepository = regionalParameterRepository;
        _cacheService = cacheService;
        _publisher = publisher;
        _geospatialValidationService = geospatialValidationService;
        _logger = logger;
    }

    public async Task<RiskMatrixDTO?> GetRiskMatrixForEventTypeAsync(
        string eventTypeCode,
        string severityLevel,
        int? version,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{RiskMatrixCachePrefix}:{eventTypeCode}:{severityLevel}:{version ?? 0}";

        _logger.LogInformation(
            "Retrieving risk matrix for event type. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
            eventTypeCode,
            severityLevel,
            version);

        var cachedResult = await _cacheService.GetAsync<RiskMatrixDTO>(cacheKey, cancellationToken);

        if (cachedResult != null)
        {
            _logger.LogInformation("Risk matrix retrieved from cache. EventTypeCode: {eventTypeCode}", eventTypeCode);
            return cachedResult;
        }

        var riskMatrix = await _riskMatrixRepository.GetRiskMatrixForEventTypeAsync(
            eventTypeCode,
            severityLevel,
            version,
            cancellationToken);

        if (riskMatrix is null)
        {
            _logger.LogWarning(
                "Risk matrix not found for event type. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
                eventTypeCode,
                severityLevel,
                version);
            return null;
        }

        var result = new RiskMatrixDTO
        {
            EventTypeCode = riskMatrix.EventType.Code,
            SeverityLevel = riskMatrix.SeverityLevel.ToString(),
            RiskLevel = riskMatrix.RiskLevel.ToString(),
            Version = riskMatrix.Version
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "Risk matrix retrieved successfully. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, RiskLevel: {riskLevel}, Version: {version}",
            riskMatrix.EventType.Code,
            riskMatrix.SeverityLevel,
            riskMatrix.RiskLevel,
            riskMatrix.Version);

        return result;
    }

    public async Task<IDFCurvesDTO?> GetIDFCurvesForEventTypeAsync(
        string eventTypeCode,
        int? returnPeriodYears,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{IDFCurveCachePrefix}:{eventTypeCode}:{returnPeriodYears ?? 0}";

        _logger.LogInformation(
            "Retrieving IDF curves for event type. EventTypeCode: {eventTypeCode}, ReturnPeriodYears: {returnPeriodYears}",
            eventTypeCode,
            returnPeriodYears);

        var cachedResult = await _cacheService.GetAsync<IDFCurvesDTO>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            _logger.LogInformation("IDF curve retrieved from cache. EventTypeCode: {eventTypeCode}", eventTypeCode);
        
            return cachedResult;
        }

        var IDFCurve = await _IDFCurveRepository.GetIDFCurveForEventTypeAsync(
            eventTypeCode,
            returnPeriodYears,
            cancellationToken);

        if (IDFCurve == null)
        {
            _logger.LogWarning(
                "IDF curve not found for event type. EventTypeCode: {eventTypeCode}, ReturnPeriodYears: {returnPeriodYears}",
                eventTypeCode,
                returnPeriodYears);

            return null;
        }

        var result = new IDFCurvesDTO
        {
            DurationMinutes = IDFCurve.DurationMinutes,
            Intensity = IDFCurve.Intensity,
            ReturnPeriodYears = IDFCurve.ReturnPeriodYears,
            Version = IDFCurve.Version,
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "IDF curve retrieved successfully. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, Intensity: {intensity}, ReturnPeriodYears: {returnPeriodYears}, Version: {version}",
            IDFCurve.EventType.Code,
            IDFCurve.DurationMinutes,
            IDFCurve.Intensity,
            IDFCurve.ReturnPeriodYears,
            IDFCurve.Version);

        return result;
    }

    public async Task<RegionalRiskParametersDTO?> GetRegionalRiskParametersAsync(
        Guid regionId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{RegionalParameterCachePrefix}:{regionId}";

        _logger.LogInformation("Retrieving regional risk parameters. RegionId: {regionId}", regionId);

        var cachedResult = await _cacheService.GetAsync<RegionalRiskParametersDTO>(cacheKey, cancellationToken);
        if (cachedResult != null)
        {
            _logger.LogInformation("Regional risk parameters retrieved from cache. RegionId: {regionId}", regionId);

            return cachedResult;
        }

        var regionalParameter = await _regionalParameterRepository.GetRegionByIdAsync(regionId, cancellationToken);

        if (regionalParameter is null)
        {
            _logger.LogWarning("Regional risk parameters not found. RegionId: {regionId}", regionId);

            return null;
        }

        PolygonDTO? regionBounds = null;
        if (!string.IsNullOrEmpty(regionalParameter.RegionBoundsJson))
        {
            try
            {
                regionBounds = System.Text.Json.JsonSerializer.Deserialize<PolygonDTO>(regionalParameter.RegionBoundsJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize region bounds for RegionId: {regionId}", regionId);
            }
        }

        var result = new RegionalRiskParametersDTO
        {
            RegionId = regionalParameter.Id,
            AdjustmentFactor = regionalParameter.AdjustmentFactor,
            Description = regionalParameter.Description,
            CenterLatitude = regionalParameter.CenterLatitude,
            CenterLongitude = regionalParameter.CenterLongitude,
            RegionBounds = regionBounds,
            CoverageRadiusKm = regionalParameter.CoverageRadiusKm
        };

        await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1), cancellationToken);

        _logger.LogInformation(
            "Regional risk parameters retrieved successfully. RegionId: {regionId}, AdjustmentFactor: {adjustmentFactor}",
            regionalParameter.Id,
            regionalParameter.AdjustmentFactor);

        return result;
    }

    public async Task<bool> CreateRiskMatrixAsync(
        RiskMatrixDTO riskMatrixDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating risk matrix. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, RiskLevel: {riskLevel}, Version: {version}",
            riskMatrixDto.EventTypeCode,
            riskMatrixDto.SeverityLevel,
            riskMatrixDto.RiskLevel,
            riskMatrixDto.Version);

        var eventType = await _eventTypeRepository.GetEventTypeByCode(riskMatrixDto.EventTypeCode, cancellationToken);

        if (eventType is null)
        {
            _logger.LogError(
                "Failed to create risk matrix. Event type does not exist. EventTypeCode: {eventTypeCode}",
                riskMatrixDto.EventTypeCode);

            throw new ArgumentException("Event type does not exist.");
        }

        var riskMatrixExist = await _riskMatrixRepository.GetRiskMatrixForEventTypeAsync(
            riskMatrixDto.EventTypeCode,
            riskMatrixDto.SeverityLevel,
            riskMatrixDto.Version,
            cancellationToken) is not null;

        if (riskMatrixExist)
        {
            _logger.LogWarning(
                "Failed to create risk matrix. Risk matrix already exists. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.Version);

            throw new ArgumentException(
                "Risk matrix already exists for the given event type, severity level, and version.");
        }

        var riskMatrix = new RiskMatrix
        {
            EventTypeId = eventType.Id,
            SeverityLevel = Enum.Parse<SeverityLevelEnum>(riskMatrixDto.SeverityLevel),
            RiskLevel = Enum.Parse<RiskLevelEnum>(riskMatrixDto.RiskLevel),
            Version = riskMatrixDto.Version
        };

        var isCreated = await _riskMatrixRepository.AddRiskMatrixAsync(riskMatrix, cancellationToken);

        if (isCreated)
        {
            var cachePattern = $"{RiskMatrixCachePrefix}:{riskMatrixDto.EventTypeCode}:{riskMatrixDto.SeverityLevel}:*";
            await _cacheService.RemoveByPatternAsync(cachePattern, cancellationToken);

            _logger.LogInformation(
                "Risk matrix created successfully. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, RiskLevel: {riskLevel}, Version: {version}",
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.RiskLevel,
                riskMatrixDto.Version);
        }
        else
        {
            _logger.LogError(
                "Failed to create risk matrix. Database operation returned false. EventTypeCode: {eventTypeCode}, SeverityLevel: {severityLevel}, Version: {version}",
                riskMatrixDto.EventTypeCode,
                riskMatrixDto.SeverityLevel,
                riskMatrixDto.Version);
        }

        return isCreated;
    }

    public async Task<bool> CreateIDFCurvesAsync(
        CreateIDFCurvesDTO idfCurvesDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating IDF curve. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, Intensity: {intensity}, ReturnPeriodYears: {returnPeriodYears}, Version: {version}",
            idfCurvesDto.EventTypeCode,
            idfCurvesDto.DurationMinutes,
            idfCurvesDto.Intensity,
            idfCurvesDto.ReturnPeriodYears,
            idfCurvesDto.Version);

        var eventType = await _eventTypeRepository.GetEventTypeByCode(idfCurvesDto.EventTypeCode, cancellationToken);

        if (eventType is null)
        {
            _logger.LogError(
                "Failed to create IDF curve. Event type does not exist. EventTypeCode: {eventTypeCode}",
                idfCurvesDto.EventTypeCode);

            throw new ArgumentException("Event type does not exist.");
        }

        var idfCurveExist = await _IDFCurveRepository.GetIDFCurveForDurationAndPeriod(
            idfCurvesDto.EventTypeCode,
            idfCurvesDto.DurationMinutes,
            idfCurvesDto.ReturnPeriodYears,
            cancellationToken) is not null;

        if (idfCurveExist)
        {
            _logger.LogWarning(
                "Failed to create IDF curve. IDF curve already exists. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, ReturnPeriodYears: {returnPeriodYears}",
                idfCurvesDto.EventTypeCode,
                idfCurvesDto.DurationMinutes,
                idfCurvesDto.ReturnPeriodYears);

            throw new ArgumentException(
                "IDF Curve already exists for the given event type, duration minutes and return period years.");
        }

        var idfCurve = new IDFCurve
        {
            EventTypeId = eventType.Id,
            DurationMinutes = idfCurvesDto.DurationMinutes,
            Intensity = idfCurvesDto.Intensity,
            ReturnPeriodYears = idfCurvesDto.ReturnPeriodYears,
            Version = idfCurvesDto.Version
        };

        var isCreated = await _IDFCurveRepository.AddIDFCurveAsync(idfCurve, cancellationToken);

        if (isCreated)
        {
            var cachePattern = $"{IDFCurveCachePrefix}:{idfCurvesDto.EventTypeCode}:*";
            await _cacheService.RemoveByPatternAsync(cachePattern, cancellationToken);

            _logger.LogInformation(
                "IDF curve created successfully. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, ReturnPeriodYears: {returnPeriodYears}, Version: {version}",
                idfCurvesDto.EventTypeCode,
                idfCurvesDto.DurationMinutes,
                idfCurvesDto.ReturnPeriodYears,
                idfCurvesDto.Version);
        }
        else
        {
            _logger.LogError(
                "Failed to create IDF curve. Database operation returned false. EventTypeCode: {eventTypeCode}, DurationMinutes: {durationMinutes}, ReturnPeriodYears: {returnPeriodYears}",
                idfCurvesDto.EventTypeCode,
                idfCurvesDto.DurationMinutes,
                idfCurvesDto.ReturnPeriodYears);
        }

        return isCreated;
    }

    public async Task<bool> CreateRegionalRiskParametersAsync(
        CreateRegionalRiskParameterDTO regionalRiskParametersDto,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Creating regional risk parameters. Adjustment factor: {adjustmentFactor}, Description: {description}",
            regionalRiskParametersDto.AdjustmentFactor,
            regionalRiskParametersDto.Description
        );

        // Validate geospatial coordinates if provided
        if (regionalRiskParametersDto.CenterLatitude.HasValue && regionalRiskParametersDto.CenterLongitude.HasValue)
        {
            var isValidCoordinates = await _geospatialValidationService.ValidateCoordinatesAsync(
                regionalRiskParametersDto.CenterLatitude.Value,
                regionalRiskParametersDto.CenterLongitude.Value,
                cancellationToken);

            if (!isValidCoordinates)
            {
                _logger.LogWarning(
                    "Invalid coordinates provided. Latitude: {latitude}, Longitude: {longitude}",
                    regionalRiskParametersDto.CenterLatitude.Value,
                    regionalRiskParametersDto.CenterLongitude.Value);
                throw new ArgumentException("Invalid coordinates provided for regional parameters.");
            }
        }

        // Validate region bounds if provided
        if (regionalRiskParametersDto.RegionBounds != null)
        {
            var isValidBounds = await _geospatialValidationService.ValidateRegionBoundsAsync(
                regionalRiskParametersDto.RegionBounds,
                cancellationToken);

            if (!isValidBounds)
            {
                _logger.LogWarning("Invalid region bounds provided");
                throw new ArgumentException("Invalid region bounds provided for regional parameters.");
            }
        }

        var regionalParameterExist = await _regionalParameterRepository.GetByAdjustmentFactorAsync(
            regionalRiskParametersDto.AdjustmentFactor,
            cancellationToken) is not null;

        if (regionalParameterExist)
        {
            _logger.LogWarning(
                "Failed to create regional risk parameters. Regional risk parameters already exist. AdjustmentFactor: {adjustmentFactor}",
                regionalRiskParametersDto.AdjustmentFactor);

            throw new ArgumentException(
                "Regional risk parameters already exist for the given region.");
        }

        var regionBoundsJson = regionalRiskParametersDto.RegionBounds != null 
            ? System.Text.Json.JsonSerializer.Serialize(regionalRiskParametersDto.RegionBounds) 
            : null;

        var regionalParameter = new RegionalParameter
        {
            AdjustmentFactor = regionalRiskParametersDto.AdjustmentFactor,
            Description = regionalRiskParametersDto.Description,
            CenterLatitude = regionalRiskParametersDto.CenterLatitude,
            CenterLongitude = regionalRiskParametersDto.CenterLongitude,
            RegionBoundsJson = regionBoundsJson,
            CoverageRadiusKm = regionalRiskParametersDto.CoverageRadiusKm
        };

        var isCreated =
            await _regionalParameterRepository.AddRegionalParameterAsync(regionalParameter, cancellationToken);

        if (isCreated)
        {
            var regionalParameterCreated = await _regionalParameterRepository.GetByAdjustmentFactorAsync(
                regionalRiskParametersDto.AdjustmentFactor,
                cancellationToken);
            var cacheKey = $"{RegionalParameterCachePrefix}:{regionalParameterCreated.Id}";
            await _cacheService.RemoveAsync(cacheKey, cancellationToken);

            _logger.LogInformation(
                "Regional risk parameters created successfully. RegionId: {regionId}, AdjustmentFactor: {adjustmentFactor}",
                regionalParameterCreated.Id,
                regionalParameterCreated.AdjustmentFactor);
        }
        else
        {
            _logger.LogError(
                "Failed to create regional risk parameters. Database operation returned false. AdjustmentFactor: {adjustmentFactor}",
                regionalRiskParametersDto.AdjustmentFactor);
        }

        return isCreated;
    }

    public async Task<bool> PublishCatalogVersionAsync(
        CatalogPublishDTO catalogPublishDto,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Publishing catalog version. Version: {version}, Notes: {notes}",
            catalogPublishDto.Version,
            catalogPublishDto.Notes);

        var riskMatrixExists = await _riskMatrixRepository.MarkVersionAsActiveAsync(
            catalogPublishDto.Version,
            cancellationToken);
        var idfCurveExists = await _IDFCurveRepository.MarkVersionAsActiveAsync(
            catalogPublishDto.Version,
            cancellationToken);

        if (!riskMatrixExists && !idfCurveExists)
        {
            _logger.LogWarning(
                "Failed to publish catalog version. No data found for version: {version}",
                catalogPublishDto.Version);
            throw new ArgumentException($"No data found for version {catalogPublishDto.Version}");
        }

        var publishedEvent = new RiskCatalogPublishedEvent
        {
            Version = catalogPublishDto.Version,
            Notes = catalogPublishDto.Notes,
            PublishedAt = DateTime.UtcNow,
            PublishedBy = tenantId
        };

        var eventPayload = JsonSerializer.Serialize(publishedEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _publisher.PublishAsync(RiskCatalogPublishedTopic, eventPayload, cancellationToken);

        await _cacheService.RemoveByPatternAsync($"{RiskMatrixCachePrefix}:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync($"{IDFCurveCachePrefix}:*", cancellationToken);
        await _cacheService.RemoveByPatternAsync($"{RegionalParameterCachePrefix}:*", cancellationToken);

        _logger.LogInformation(
            "Catalog version published successfully. Version: {version}, Notes: {notes}",
            catalogPublishDto.Version,
            catalogPublishDto.Notes);

        return true;
    }
}