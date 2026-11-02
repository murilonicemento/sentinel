using System.ComponentModel.DataAnnotations;

namespace Ingestion.Application.Attributes;

public class NotInFuture : ValidationAttribute
{
    public NotInFuture()
    {
        ErrorMessage = "Date can't be in future.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;
        if (value is not DateTime dateTime)
            return new ValidationResult("Date is not in a valid format.");

        // Compare instants in UTC: DateTime comparison ignores Kind, so comparing a UTC value with DateTime.Now
        // shifts the limit by the server's offset. Values without a Kind are UTC per the API contract.
        var instant = dateTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
            : dateTime.ToUniversalTime();

        return instant > DateTime.UtcNow ? new ValidationResult(ErrorMessage) : ValidationResult.Success;
    }
}