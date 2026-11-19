using Claims.Application.Repository;
using Claims.Domain;
using FluentValidation;

namespace Claims.Application.Validation;

public sealed class ClaimValidator : AbstractValidator<Claim>
{
    public ClaimValidator(ICoverRepository covers)
    {
        RuleFor(claim => claim.DamageCost)
            .LessThanOrEqualTo(100_000m)
            .WithMessage("Damage cost cannot exceed 100000.");

        RuleFor(claim => claim.CoverId)
            .NotEmpty()
            .WithMessage("A related cover is required.");

        RuleFor(claim => claim).CustomAsync(async (claim, context, cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(claim.CoverId))
                return;

            var cover = await covers.GetByIdAsync(claim.CoverId);
            if (cover is null)
                context.AddFailure(nameof(Claim.CoverId), "The related cover does not exist.");
            else if (claim.Created < cover.StartDate || claim.Created > cover.EndDate)
                context.AddFailure(nameof(Claim.Created), "Created date must be within the related cover period.");
        });
    }
}
