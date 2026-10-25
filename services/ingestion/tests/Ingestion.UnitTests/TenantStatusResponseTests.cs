using System.Text.Json;
using Ingestion.Infrastructure.Write.Services;

namespace Ingestion.UnitTests;

public class TenantStatusResponseTests
{
    [Theory]
    [InlineData("{\"status\":1}", true)]
    [InlineData("{\"id\":\"x\",\"Status\":1}", true)]
    [InlineData("{\"status\":2}", false)]
    [InlineData("{\"statusCode\":200,\"success\":true,\"data\":{\"status\":\"Active\"}}", true)]
    [InlineData("{\"data\":{\"status\":\"active\"}}", true)]
    [InlineData("{\"data\":{\"status\":\"Suspended\"}}", false)]
    [InlineData("{\"data\":{\"status\":\"Canceled\"}}", false)]
    [InlineData("{\"name\":\"tenant\"}", false)]
    [InlineData("[]", false)]
    public void IsActive_AcceptsLegacyNumericAndV1StringStatus(string json, bool expected)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Equal(expected, TenantStatusResponse.IsActive(document.RootElement));
    }
}
