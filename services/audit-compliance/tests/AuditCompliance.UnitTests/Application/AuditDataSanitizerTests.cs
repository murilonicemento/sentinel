using System.Text.Json;
using AuditCompliance.Application.Ingestion;
using AuditCompliance.Domain.Exceptions;

namespace AuditCompliance.UnitTests.Application;

public class AuditDataSanitizerTests
{
    [Fact]
    public void Sanitize_RedactsCredentialsAndDirectPersonalDataAtAnyDepth()
    {
        var element = Parse("""
                            {
                              "planId": "pro",
                              "password": "p@ss",
                              "nested": { "apiKey": "k", "contact": { "e-mail": "a@b.c", "Email": "x@y.z" } },
                              "items": [ { "access_token": "t" } ]
                            }
                            """);

        var result = AuditDataSanitizer.Sanitize(element, "metadata");
        var root = JsonDocument.Parse(result.Json).RootElement;

        Assert.Equal(5, result.RedactedFields);
        Assert.Equal("pro", root.GetProperty("planId").GetString());
        Assert.Equal(AuditDataSanitizer.RedactedValue, root.GetProperty("password").GetString());
        Assert.Equal(AuditDataSanitizer.RedactedValue, root.GetProperty("nested").GetProperty("apiKey").GetString());
        Assert.Equal(AuditDataSanitizer.RedactedValue,
            root.GetProperty("nested").GetProperty("contact").GetProperty("e-mail").GetString());
        Assert.Equal(AuditDataSanitizer.RedactedValue,
            root.GetProperty("items")[0].GetProperty("access_token").GetString());
        Assert.DoesNotContain("p@ss", result.Json);
    }

    [Fact]
    public void Sanitize_PreservesNonSensitiveValuesAndTypes()
    {
        var element = Parse("""{ "score": 0.75, "active": true, "level": null, "tags": ["a", "b"] }""");

        var result = AuditDataSanitizer.Sanitize(element, "metadata");
        var root = JsonDocument.Parse(result.Json).RootElement;

        Assert.Equal(0, result.RedactedFields);
        Assert.Equal(0.75, root.GetProperty("score").GetDouble());
        Assert.True(root.GetProperty("active").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("level").ValueKind);
        Assert.Equal(2, root.GetProperty("tags").GetArrayLength());
    }

    [Fact]
    public void Sanitize_WhenNotAnObject_Throws()
    {
        var exception = Assert.Throws<AuditDomainException>(() =>
            AuditDataSanitizer.Sanitize(Parse("[1, 2]"), "metadata"));

        Assert.Equal(AuditErrorCodes.InvalidField, exception.Code);
    }

    [Fact]
    public void Sanitize_WhenDeeperThanLimit_Throws()
    {
        var element = Parse("""{ "a": { "b": { "c": { "d": { "e": { "f": 1 } } } } } }""");

        var exception = Assert.Throws<AuditDomainException>(() => AuditDataSanitizer.Sanitize(element, "metadata"));

        Assert.Equal(AuditErrorCodes.PayloadLimitExceeded, exception.Code);
    }

    [Fact]
    public void Sanitize_WhenArrayExceedsLimit_Throws()
    {
        var items = string.Join(',', Enumerable.Range(0, AuditDataSanitizer.MaxArrayLength + 1));

        var exception = Assert.Throws<AuditDomainException>(() =>
            AuditDataSanitizer.Sanitize(Parse($$"""{ "items": [{{items}}] }"""), "metadata"));

        Assert.Equal(AuditErrorCodes.PayloadLimitExceeded, exception.Code);
    }

    [Fact]
    public void Sanitize_WhenStringExceedsLimit_Throws()
    {
        var value = new string('x', AuditDataSanitizer.MaxStringLength + 1);

        var exception = Assert.Throws<AuditDomainException>(() =>
            AuditDataSanitizer.Sanitize(Parse($$"""{ "note": "{{value}}" }"""), "metadata"));

        Assert.Equal(AuditErrorCodes.PayloadLimitExceeded, exception.Code);
    }

    private static JsonElement Parse(string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}
