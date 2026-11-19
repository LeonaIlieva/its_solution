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

public class CoversControllerTests : IDisposable
{
    private static readonly DateTime StartDate = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly ControllerTestDependencies dependencies = new();
    private readonly InlineValidator<Cover> validator = new();

    [Fact]
    public async Task GetAsync_ReturnsAllCoversWithoutAuditing()
    {
        var first = new Cover { Id = "first" };
        var second = new Cover { Id = "second" };
        dependencies.Covers.AddRange([first, second]);

        var response = await CreateController().GetAsync();

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal(new[] { first, second }, Assert.IsAssignableFrom<IEnumerable<Cover>>(result.Value));
        Assert.Empty(dependencies.CoverAudits);
    }

    [Fact]
    public async Task GetAsync_ExistingId_ReturnsMatchingCover()
    {
        dependencies.Covers.Add(new Cover { Id = "other" });
        var cover = new Cover { Id = "cover-id" };
        dependencies.Covers.Add(cover);

        var response = await CreateController().GetAsync(cover.Id);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(cover, result.Value);
        Assert.Empty(dependencies.CoverAudits);
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNotFound()
    {
        dependencies.Covers.Add(new Cover { Id = "other" });

        var response = await CreateController().GetAsync("missing");

        Assert.IsType<NotFoundResult>(response.Result);
        Assert.Empty(dependencies.CoverAudits);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_MapsFieldsAndReturnsSavedCoverWithPremium()
    {
        var request = new CreateCoverRequest(StartDate, StartDate.AddDays(1), CoverType.Yacht);

        var response = await CreateController().CreateAsync(request, TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(response.Result);
        var cover = Assert.IsType<Cover>(result.Value);
        Assert.Equal(request.StartDate, cover.StartDate);
        Assert.Equal(request.EndDate, cover.EndDate);
        Assert.Equal(request.Type, cover.Type);
        Assert.Equal(1_375m, cover.Premium);
        Assert.Same(cover, Assert.Single(dependencies.Covers));
        Assert.Equal((cover.Id, AuditOperation.Create), Assert.Single(dependencies.CoverAudits));
    }

    [Fact]
    public async Task ComputePremium_ValidRequest_ReturnsQuoteWithoutSavingOrAuditing()
    {
        var response = await CreateController().ComputePremium(StartDate, StartDate.AddDays(1), CoverType.Tanker,
            TestContext.Current.CancellationToken);

        var result = Assert.IsType<OkObjectResult>(response);
        Assert.Equal(1_875m, Assert.IsType<decimal>(result.Value));
        Assert.Empty(dependencies.Covers);
        Assert.Empty(dependencies.CoverAudits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateOrCompute_ValidationFailure_ReturnsProblemWithoutSavingOrAuditing(bool computeOnly)
    {
        const string message = "Cover validation failed.";
        validator.RuleFor(cover => cover.EndDate).GreaterThanOrEqualTo(cover => cover.StartDate).WithMessage(message);
        var controller = CreateController();
        var request = new CreateCoverRequest(StartDate, StartDate.AddDays(-1), CoverType.Yacht);
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = computeOnly
            ? await controller.ComputePremium(request.StartDate, request.EndDate, request.Type, cancellationToken)
            : (await controller.CreateAsync(request, cancellationToken)).Result;

        var result = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(400, result.StatusCode);
        var problem = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(message, Assert.Single(problem.Errors[nameof(Cover.EndDate)]));
        Assert.Empty(dependencies.Covers);
        Assert.Empty(dependencies.CoverAudits);
    }

    [Fact]
    public async Task DeleteAsync_UnreferencedCover_ReturnsNoContentAndDeletesCover()
    {
        dependencies.Covers.Add(new Cover { Id = "cover-id" });
        var other = new Cover { Id = "other" };
        dependencies.Covers.Add(other);
        dependencies.Claims.Add(new Claim { CoverId = other.Id });

        var response = await CreateController().DeleteAsync("cover-id");

        Assert.IsType<NoContentResult>(response);
        Assert.Same(other, Assert.Single(dependencies.Covers));
        Assert.Equal(("cover-id", AuditOperation.Delete), Assert.Single(dependencies.CoverAudits));
    }

    [Fact]
    public async Task DeleteAsync_ReferencedCover_ReturnsConflictAndAuditsAttemptWithoutDeleting()
    {
        var cover = new Cover { Id = "cover-id" };
        dependencies.Covers.Add(cover);
        dependencies.Claims.Add(new Claim { CoverId = cover.Id });

        var response = await CreateController().DeleteAsync(cover.Id);

        var result = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(409, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("The cover is referenced by an existing claim.", problem.Detail);
        Assert.Same(cover, Assert.Single(dependencies.Covers));
        Assert.Equal((cover.Id, AuditOperation.Delete), Assert.Single(dependencies.CoverAudits));
    }

    public void Dispose() => dependencies.Dispose();

    private CoversController CreateController() => new(new CoverService(dependencies, dependencies, dependencies),
        validator, NullLogger<CoversController>.Instance)
    {
        ControllerContext = dependencies.CreateControllerContext()
    };
}
