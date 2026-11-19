using Claims.Domain;
using FluentValidation;

namespace Claims.Application.Validation;

public sealed class CoverValidator : AbstractValidator<Cover>
{
    public CoverValidator(TimeProvider timeProvider)
    {
        // Compare UTC calendar dates so earlier times today remain valid.
        RuleFor(cover => cover.StartDate)
            .Must(startDate => startDate.Date >= timeProvider.GetUtcNow().UtcDateTime.Date)
            .WithMessage("Start date cannot be in the past.");

        RuleFor(cover => cover.EndDate)
            .Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(cover => cover.StartDate)
            .WithMessage("End date cannot be before start date.")
            // Use a calendar year for leap years; skip AddYears when it would overflow.
            .Must((cover, endDate) => cover.StartDate.Year == DateTime.MaxValue.Year
                || endDate <= cover.StartDate.AddYears(1))
            .WithMessage("The insurance period cannot exceed one year.");
    }
}
