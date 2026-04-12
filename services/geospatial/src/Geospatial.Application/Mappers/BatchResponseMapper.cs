using Geospatial.Application.DTOs;

namespace Geospatial.Application.Mappers;

public static class BatchResponseMapper
{
    public static BatchResponseDTO FromDomain(List<(string Type, object Result)> domainResults)
    {
        var response = new BatchResponseDTO();

        foreach (var (type, result) in domainResults)
        {
            switch (type.ToLower())
            {
                case "contains-point":
                    var contains = (bool)result;
                    response.Results.Add(new BatchResultDTO
                    {
                        Type = type,
                        Contains = contains
                    });
                    break;
                case "within-radius":
                    var tuple = ((bool WithinRadius, double Distance))result;
                    response.Results.Add(new BatchResultDTO
                    {
                        Type = type,
                        WithinRadius = tuple.WithinRadius,
                        Distance = tuple.Distance
                    });
                    break;
            }
        }

        return response;
    }
}