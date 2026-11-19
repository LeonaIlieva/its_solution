using Claims.Application.Service;
using Claims.Controllers.Requests;
using Claims.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Controllers;

[ApiController]
[Route("[controller]")]
public class ClaimsController(
    ClaimService claims,
    IValidator<Claim> validator,
    ILogger<ClaimsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<Claim>> GetAsync()
    {
        var result = await claims.GetAllAsync();
        logger.LogDebug("Retrieved {ClaimCount} claims.", result.Count);
        return result;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Claim>> GetAsync(string id)
    {
        var claim = await claims.GetByIdAsync(id);
        if (claim is null)
        {
            logger.LogInformation("Claim {ClaimId} was not found.", id);
            return NotFound();
        }

        logger.LogDebug("Retrieved claim {ClaimId}.", id);
        return Ok(claim);
    }

    [HttpPost]
    public async Task<ActionResult<Claim>> CreateAsync(CreateClaimRequest request, CancellationToken cancellationToken)
    {
        var claim = new Claim
        {
            CoverId = request.CoverId,
            Created = request.Created,
            Name = request.Name,
            Type = request.Type,
            DamageCost = request.DamageCost
        };
        var result = await validator.ValidateAsync(claim, cancellationToken);
        if (!result.IsValid)
        {
            logger.LogWarning("Claim creation rejected with {ValidationErrorCount} validation errors.", result.Errors.Count);
            return ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));
        }

        var created = await claims.CreateAsync(claim);
        logger.LogInformation("Created claim {ClaimId}.", created.Id);
        return Ok(created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        await claims.DeleteAsync(id);
        logger.LogInformation("Processed delete request for claim {ClaimId}.", id);
        return NoContent();
    }
}
