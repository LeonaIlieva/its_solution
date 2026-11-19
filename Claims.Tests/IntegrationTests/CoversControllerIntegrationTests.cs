using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Claims.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using static Claims.Tests.IntegrationTests.ControllerIntegrationTestHost;

namespace Claims.Tests.IntegrationTests;

public class CoversControllerIntegrationTests
{
    [Theory]
    [InlineData("/Covers")]
    [InlineData("/Covers/compute?startDate=2029-12-31&endDate=2030-01-02&coverType=Yacht")]
    public async Task CreateOrCompute_InvalidInput_ReturnsFieldSpecificBadRequest(string path)
    {
        var store = new TestStore();
        using var server = CreateServer(store);
        using var client = server.CreateClient();
        var body = new Cover { Id = "ignored", StartDate = Today.AddDays(-1), EndDate = Today.AddDays(1) };

        using var response = await client.PostAsJsonAsync(path, body);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        Assert.Contains("StartDate", problem.Errors.Keys);
        Assert.NotEmpty(problem.Errors["StartDate"][0]);
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
        var body = new Cover { Id = "ignored", StartDate = Today, EndDate = Today.AddYears(1) };

        using var response = await client.PostAsJsonAsync("/Covers", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, store.ClaimWrites);
        Assert.Equal(1, store.CoverWrites);
        Assert.Equal(1, store.AuditWrites);
    }

    [Fact]
    public async Task Compute_ValidQuote_ReturnsPremiumWithoutSavingOrAuditing()
    {
        var store = new TestStore();
        using var server = CreateServer(store);
        using var client = server.CreateClient();

        using var response = await client.PostAsync(
            "/Covers/compute?startDate=2030-01-01&endDate=2030-07-01&coverType=Yacht", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(238_452.50m, await response.Content.ReadFromJsonAsync<decimal>());
        Assert.Equal(0, store.CoverWrites);
        Assert.Equal(0, store.AuditWrites);
    }

    [Fact]
    [Trait("Category", "Docker")]
    public async Task CreateAndGetCover_UsesQuotedPremiumAndGeneratedId()
    {
        await using var application = new WebApplicationFactory<Program>();
        using var client = application.CreateClient();
        var startDate = DateTime.UtcNow.Date.AddDays(1);
        var endDate = startDate.AddDays(1);
        using var quote = await client.PostAsync(
            $"/Covers/compute?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&coverType=Yacht", null);
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);
        var quotedPremium = await quote.Content.ReadFromJsonAsync<decimal>();
        Assert.Equal(1375m, quotedPremium);

        using var created = await client.PostAsJsonAsync("/Covers", new { startDate, endDate, type = "Yacht" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var createdCover = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = createdCover.GetProperty("id").GetGuid();
        Assert.Equal(quotedPremium, createdCover.GetProperty("premium").GetDecimal());

        using var lookup = await client.GetAsync($"/Covers/{id}");
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var storedCover = await lookup.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(id, storedCover.GetProperty("id").GetGuid());
        Assert.Equal("Yacht", storedCover.GetProperty("type").GetString());
        Assert.Equal(quotedPremium, storedCover.GetProperty("premium").GetDecimal());
    }
}
