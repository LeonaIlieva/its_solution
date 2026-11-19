using Claims.Application.Service;
using Claims.Controllers.Requests;
using Claims.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Claims.Controllers;

[ApiController]
[Route("[controller]")]
public class CoversController(
    CoverService covers,
    IValidator<Cover> validator,
    ILogger<CoversController> logger) : ControllerBase
{
    [HttpPost("compute")]
    public async Task<ActionResult> ComputePremium(
        DateTime startDate, DateTime endDate, CoverType coverType, CancellationToken cancellationToken)
    {
        var cover = new Cover { StartDate = startDate, EndDate = endDate, Type = coverType };
        var result = await validator.ValidateAsync(cover, cancellationToken);
        if (!result.IsValid)
        {
            logger.LogWarning("Premium computation rejected with {ValidationErrorCount} validation errors.", result.Errors.Count);
            return ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));
        }

        var premium = covers.ComputePremium(startDate, endDate, coverType);
        logger.LogDebug("Computed premium for cover type {CoverType}.", coverType);
        return Ok(premium);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Cover>>> GetAsync()
    {
        var result = await covers.GetAllAsync();
        logger.LogDebug("Retrieved {CoverCount} covers.", result.Count);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Cover>> GetAsync(string id)
    {
        var cover = await covers.GetByIdAsync(id);
        if (cover is null)
        {
            logger.LogInformation("Cover {CoverId} was not found.", id);
            return NotFound();
        }

        logger.LogDebug("Retrieved cover {CoverId}.", id);
        return Ok(cover);
    }

    [HttpPost]
    public async Task<ActionResult<Cover>> CreateAsync(CreateCoverRequest request, CancellationToken cancellationToken)
    {
        var cover = new Cover { StartDate = request.StartDate, EndDate = request.EndDate, Type = request.Type };
        var result = await validator.ValidateAsync(cover, cancellationToken);
        if (!result.IsValid)
        {
            logger.LogWarning("Cover creation rejected with {ValidationErrorCount} validation errors.", result.Errors.Count);
            return ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));
        }

        var created = await covers.CreateAsync(cover);
        logger.LogInformation("Created cover {CoverId}.", created.Id);
        return Ok(created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(string id)
    {
        if (!await covers.TryDeleteAsync(id))
        {
            logger.LogWarning("Delete rejected for cover {CoverId} because it is referenced by an existing claim.", id);
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: "The cover is referenced by an existing claim.");
        }

        logger.LogInformation("Processed delete request for cover {CoverId}.", id);
        return NoContent();
    }
}
