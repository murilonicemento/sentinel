using Geospatial.Application.DTOs;
using Geospatial.Application.Mappers;

namespace Geospatial.UnitTests.Mappers;

public class BatchResponseMapperTests
{
    [Fact]
    public void FromDomain_WithContainsPointResult_MapsCorrectly()
    {
        var domainResults = new List<(string Type, object Result)>
        {
            ("contains-point", true)
        };

        var result = BatchResponseMapper.FromDomain(domainResults);

        Assert.Single(result.Results);
        Assert.Equal("contains-point", result.Results[0].Type);
        Assert.True(result.Results[0].Contains);
    }

    [Fact]
    public void FromDomain_WithWithinRadiusResult_MapsCorrectly()
    {
        var domainResults = new List<(string Type, object Result)>
        {
            ("within-radius", (true, 250.5))
        };

        var result = BatchResponseMapper.FromDomain(domainResults);

        Assert.Single(result.Results);
        Assert.Equal("within-radius", result.Results[0].Type);
        Assert.True(result.Results[0].WithinRadius);
        Assert.Equal(250.5, result.Results[0].Distance);
    }

    [Fact]
    public void FromDomain_WithMixedResults_MapsAll()
    {
        var domainResults = new List<(string Type, object Result)>
        {
            ("contains-point", false),
            ("within-radius", (false, 1500.0))
        };

        var result = BatchResponseMapper.FromDomain(domainResults);

        Assert.Equal(2, result.Results.Count);
        Assert.False(result.Results[0].Contains);
        Assert.False(result.Results[1].WithinRadius);
        Assert.Equal(1500.0, result.Results[1].Distance);
    }

    [Fact]
    public void FromDomain_WithEmptyList_ReturnsEmptyResults()
    {
        var domainResults = new List<(string Type, object Result)>();

        var result = BatchResponseMapper.FromDomain(domainResults);

        Assert.Empty(result.Results);
    }
}
