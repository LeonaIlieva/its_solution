using Claims.Application;
using Claims.Application.Service;
using Claims.Controllers;
using Claims.Controllers.Requests;
using Claims.Domain;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Claims.Tests.UnitTests;

public class ClaimsControllerTests : IDisposable
{
    private readonly ControllerTestDependencies dependencies = new();
    private readonly InlineValidator<Claim> validator = new();

    [Fact]
    public async Task GetAsync_ReturnsAllClaimsWithoutAuditing()
    {
        var first = new Claim { Id = "first" };
        var second = new Claim { Id = "second" };
        dependencies.Claims.AddRange([first, second]);

        var result = await CreateController().GetAsync();

        Assert.Equal(new[] { first, second }, result);
        Assert.Empty(dependencies.ClaimAudits);
    }

    [Fact]
    public async Task GetAsync_ExistingId_ReturnsMatchingClaim()
    {
        dependencies.Claims.Add(new Claim { Id = "other" });
        var claim = new Claim { Id = "claim-id" };
        dependencies.Claims.Add(claim);

        var response = await CreateController().GetAsync(claim.Id);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(claim, result.Value);
        Assert.Empty(dependencies.ClaimAudits);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNotFound()
    {
        dependencies.Claims.Add(new Claim { Id = "other" });

        var response = await CreateController().GetAsync("missing");

        Assert.IsType<NotFoundResult>(response.Result);
        Assert.Empty(dependencies.ClaimAudits);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_MapsFieldsAndReturnsSavedClaim()
    {
        var request = new CreateClaimRequest("cover-id", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            "Hull damage", ClaimType.Grounding, 25_000m);

        var response = await CreateController().CreateAsync(request, TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        var claim = Assert.IsType<Claim>(result.Value);
        Assert.Equal(request.CoverId, claim.CoverId);
        Assert.Equal(request.Created, claim.Created);
        Assert.Equal(request.Name, claim.Name);
        Assert.Equal(request.Type, claim.Type);
        Assert.Equal(request.DamageCost, claim.DamageCost);
        Assert.Same(claim, Assert.Single(dependencies.Claims));
        Assert.Equal((claim.Id, AuditOperation.Create), Assert.Single(dependencies.ClaimAudits));
    }

    [Fact]
    public async Task CreateAsync_ValidationFailure_ReturnsProblemWithoutSavingOrAuditing()
    {
        const string message = "Claim validation failed.";
        validator.RuleFor(claim => claim.CoverId).NotEmpty().WithMessage(message);
        var request = new CreateClaimRequest("", new DateTime(2030, 1, 1), "Hull damage", ClaimType.Grounding, 25_000m);

        var response = await CreateController().CreateAsync(request, TestContext.Current.CancellationToken);

        var result = Assert.IsAssignableFrom<ObjectResult>(response.Result);
        Assert.Equal(400, result.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(message, Assert.Single(problem.Errors[nameof(Claim.CoverId)]));
        Assert.Empty(dependencies.Claims);
        Assert.Empty(dependencies.ClaimAudits);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNoContentAndDeletesOnlyRequestedClaim()
    {
        dependencies.Claims.Add(new Claim { Id = "claim-id" });
        var other = new Claim { Id = "other" };
        dependencies.Claims.Add(other);

        var response = await CreateController().DeleteAsync("claim-id");

        Assert.IsType<NoContentResult>(response);
        Assert.Same(other, Assert.Single(dependencies.Claims));
        Assert.Equal(("claim-id", AuditOperation.Delete), Assert.Single(dependencies.ClaimAudits));
    }

    public void Dispose() => dependencies.Dispose();

    private ClaimsController CreateController() => new(new ClaimService(dependencies, dependencies), validator,
        NullLogger<ClaimsController>.Instance)
    {
        ControllerContext = dependencies.CreateControllerContext()
    };
}
