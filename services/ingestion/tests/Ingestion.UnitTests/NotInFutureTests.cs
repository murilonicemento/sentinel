using System.ComponentModel.DataAnnotations;
using Ingestion.Application.Attributes;
using Xunit;

namespace Ingestion.UnitTests;

public class NotInFutureTests
{
    private static bool IsValid(DateTime value) =>
        new NotInFuture().GetValidationResult(value, new ValidationContext(new object())) == ValidationResult.Success;

    [Fact]
    public void RecentUtcInstant_IsValidRegardlessOfServerOffset() =>
        Assert.True(IsValid(DateTime.UtcNow.AddSeconds(-30)));

    [Fact]
    public void FutureUtcInstant_IsRejected() =>
        Assert.False(IsValid(DateTime.UtcNow.AddMinutes(5)));

    [Fact]
    public void FutureLocalInstant_IsRejected() =>
        Assert.False(IsValid(DateTime.Now.AddMinutes(5)));

    [Fact]
    public void UnspecifiedKind_IsTreatedAsUtc() =>
        Assert.True(IsValid(DateTime.SpecifyKind(DateTime.UtcNow.AddSeconds(-30), DateTimeKind.Unspecified)));
}
