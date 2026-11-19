using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Claims.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Claims.Tests.IntegrationTests.ControllerIntegrationTestHost;

namespace Claims.Tests.IntegrationTests;

public class ClaimsControllerIntegrationTests
{
    [Fact]
    public async Task Create_InvalidInput_ReturnsFieldSpecificBadRequest()
    {
        var store = new TestStore();
        using var server = CreateServer(store);
        using var client = server.CreateClient();
        var body = new Claim
        {
            Id = "ignored", CoverId = "cover-id", Name = "Collision", Created = Today, DamageCost = 100_001m
        };

        using var response = await client.PostAsJsonAsync("/Claims", body);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("DamageCost", problem.Errors.Keys);
        Assert.NotEmpty(problem.Errors["DamageCost"][0]);
        Assert.Equal(0, store.ClaimWrites);
        Assert.Equal(0, store.CoverWrites);
        Assert.Equal(0, store.AuditWrites);
    }

    [Fact]
    public async Task Create_ValidInput_IsSavedAndAudited()
    {
        var store = new TestStore();
        using var server = CreateServer(store);
        using var client = server.CreateClient();
        var body = new Claim
        {
            Id = "original-id", CoverId = "cover-id", Name = "Collision", Created = Today.AddDays(15), DamageCost = 25_000m
        };

        using var response = await client.PostAsJsonAsync("/Claims", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, store.ClaimWrites);
        Assert.Equal(0, store.CoverWrites);
        Assert.Equal(1, store.AuditWrites);
    }

    [Fact]
    [Trait("Category", "Docker")]
    public async Task ClaimLifecycle_PersistsClaimAndProtectsReferencedCoverUntilClaimIsDeleted()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        var startDate = DateTime.UtcNow.Date.AddDays(1);
        var coverRequest = new { startDate, endDate = startDate.AddDays(1), type = "Yacht" };

        using var coverResponse = await client.PostAsJsonAsync("/Covers", coverRequest);
        Assert.Equal(HttpStatusCode.OK, coverResponse.StatusCode);
        var cover = Assert.IsType<Cover>(await coverResponse.Content.ReadFromJsonAsync<Cover>(JsonOptions));
        Assert.True(Guid.TryParse(cover.Id, out _));

        var created = startDate.AddHours(1);
        using var claimResponse = await client.PostAsJsonAsync("/Claims", new
        {
            coverId = cover.Id, created, name = "Hull damage", type = "Grounding", damageCost = 25_000m
        });
        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);
        var claim = Assert.IsType<Claim>(await claimResponse.Content.ReadFromJsonAsync<Claim>(JsonOptions));
        Assert.True(Guid.TryParse(claim.Id, out _));
        var claimPath = $"/Claims/{claim.Id}";
        var coverPath = $"/Covers/{cover.Id}";

        var saved = Assert.IsType<Claim>(await client.GetFromJsonAsync<Claim>(claimPath, JsonOptions));
        Assert.Equal(claim.Id, saved.Id);
        Assert.Equal(cover.Id, saved.CoverId);
        Assert.Equal(created, saved.Created);
        Assert.Equal("Hull damage", saved.Name);
        Assert.Equal(ClaimType.Grounding, saved.Type);
        Assert.Equal(25_000m, saved.DamageCost);
        var claims = Assert.IsType<Claim[]>(await client.GetFromJsonAsync<Claim[]>("/Claims", JsonOptions));
        Assert.Contains(claims, item => item.Id == claim.Id);

        using var otherCoverResponse = await client.PostAsJsonAsync("/Covers", coverRequest);
        Assert.Equal(HttpStatusCode.OK, otherCoverResponse.StatusCode);
        var otherCover = Assert.IsType<Cover>(await otherCoverResponse.Content.ReadFromJsonAsync<Cover>(JsonOptions));
        using var deleteOtherCover = await client.DeleteAsync($"/Covers/{otherCover.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteOtherCover.StatusCode);

        using var blocked = await client.DeleteAsync(coverPath);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using var stillPresent = await client.GetAsync(coverPath);
        Assert.Equal(HttpStatusCode.OK, stillPresent.StatusCode);

        using var delete = await client.DeleteAsync(claimPath);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var missingClaim = await client.GetAsync(claimPath);
        Assert.Equal(HttpStatusCode.NotFound, missingClaim.StatusCode);
        var remaining = Assert.IsType<Claim[]>(await client.GetFromJsonAsync<Claim[]>("/Claims", JsonOptions));
        Assert.DoesNotContain(remaining, item => item.Id == claim.Id);
        using var deleteAgain = await client.DeleteAsync(claimPath);
        Assert.Equal(HttpStatusCode.NoContent, deleteAgain.StatusCode);

        using var deleteCover = await client.DeleteAsync(coverPath);
        Assert.Equal(HttpStatusCode.NoContent, deleteCover.StatusCode);
        using var missingCover = await client.GetAsync(coverPath);
        Assert.Equal(HttpStatusCode.NotFound, missingCover.StatusCode);
        using var deleteCoverAgain = await client.DeleteAsync(coverPath);
        Assert.Equal(HttpStatusCode.NoContent, deleteCoverAgain.StatusCode);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
