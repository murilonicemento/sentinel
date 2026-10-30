using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RiskCatalog.IntegrationTests.Stack;

/// <summary>RC-14: the region code identifies a region; the adjustment factor does not.</summary>
[Collection(RiskCatalogStackCollection.Name)]
public class RegionCodeTests(RiskCatalogStackFactory stack)
{
    private const string V1 = "/api/v1/risk-catalog/regional-risk-parameters";

    private static object Region(string? regionCode, double factor = 1.5) => new
    {
        regionCode,
        adjustmentFactor = factor,
        description = $"Região {Guid.NewGuid():N}",
        centerLatitude = -23.55,
        centerLongitude = -46.63,
        coverageRadiusKm = 30.0
    };

    private static string NewCode() => $"region-{Guid.NewGuid():N}"[..30];

    [Fact]
    public async Task V1_RequiresTheRegionCode()
    {
        var response = await stack.ClientFor("RiskCatalog.Write").PostAsJsonAsync(V1, Region(null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("regionCode", out _));
    }

    [Fact]
    public async Task V1_TwoRegionsMayShareTheFactor_ButNotTheCode()
    {
        var client = stack.ClientFor("RiskCatalog.Write");
        var code = NewCode();

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(V1, Region(code, 1.7))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(V1, Region(NewCode(), 1.7))).StatusCode);
        var duplicate = await client.PostAsJsonAsync(V1, Region(code.ToUpperInvariant(), 2.0));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(1L, await stack.CountAsync("SELECT count(*) FROM regional_parameter WHERE region_code = @p", code));
    }

    [Fact]
    public async Task V1_Listing_CarriesTheRegionCode()
    {
        var code = NewCode();
        (await stack.ClientFor("RiskCatalog.Write").PostAsJsonAsync(V1, Region(code))).EnsureSuccessStatusCode();

        var found = false;
        for (var page = 1; !found; page++)
        {
            var body = await stack.ClientFor("RiskCatalog.Read")
                .GetFromJsonAsync<JsonElement>($"{V1}?page={page}&pageSize=50");
            var items = body.GetProperty("data").GetProperty("items").EnumerateArray().ToList();
            found = items.Any(item => item.GetProperty("regionCode").GetString() == code);
            if (items.Count < 50) break;
        }

        Assert.True(found);
    }

    [Fact]
    public async Task LegacyRoute_WithoutCode_GetsTheGeneratedLegacyCode()
    {
        var region = Region(null);
        var description = (string)region.GetType().GetProperty("description")!.GetValue(region)!;

        var response = await stack.ClientFor("RiskCatalog.Write")
            .PostAsJsonAsync("/api/risk-model/regional-risk-parameters", region);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1L, await stack.CountAsync(
            "SELECT count(*) FROM regional_parameter WHERE description = @p AND region_code = 'legacy-' || replace(id::text, '-', '')",
            description));
    }
}
