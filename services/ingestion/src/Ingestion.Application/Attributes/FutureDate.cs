using System.ComponentModel.DataAnnotations;

namespace Ingestion.Application.Attributes;

public class FutureDate : ValidationAttribute
{
    public FutureDate()
    {
        ErrorMessage = "Date can't be in future.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null)
            return ValidationResult.Success;
        if (value is not DateTime dateTime)
            return new ValidationResult("Date is not in a valid format.");

        return dateTime > DateTime.Now ? new ValidationResult(ErrorMessage) : ValidationResult.Success;
    }
}