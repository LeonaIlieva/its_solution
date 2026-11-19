using Claims.Application.Repository;
using Claims.Application.Validation;
using Claims.Domain;
using Xunit;

namespace Claims.Tests;

public class ValidationUnitTests
{
    private static readonly DateTime Today = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-1, 1, "StartDate")]
    [InlineData(0, -1, "EndDate")]
    [InlineData(0, 366, "EndDate")]
    public async Task Cover_InvalidPeriod_ReportsFieldError(
        int startOffset, int endOffset, string field)
    {
        var validator = new CoverValidator(new FixedTimeProvider());
        var cover = new Cover
        {
            StartDate = Today.AddDays(startOffset),
            EndDate = Today.AddDays(endOffset)
        };

        var result = await validator.ValidateAsync(cover);

        Assert.False(result.IsValid);
        Assert.Contains(field, result.ToDictionary().Keys);
    }

    [Theory]
    [InlineData(2030, 1, 1)]
    [InlineData(2030, 1, 2)]
    [InlineData(2032, 1, 1)]
    [InlineData(2032, 2, 29)]
    public async Task Cover_UpToOneCalendarYear_IsAccepted(int year, int month, int day)
    {
        var start = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        var cover = new Cover { StartDate = start, EndDate = start.AddYears(1), Type = CoverType.Yacht };
        var validator = new CoverValidator(new FixedTimeProvider());

        var result = await validator.ValidateAsync(cover);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Cover_OneTickBeyondCalendarYear_IsRejected()
    {
        var start = new DateTime(2032, 2, 29);
        var validator = new CoverValidator(new FixedTimeProvider());

        var result = validator.Validate(new Cover { StartDate = start, EndDate = start.AddYears(1).AddTicks(1) });

        Assert.False(result.IsValid);
        Assert.Contains(nameof(Cover.EndDate), result.ToDictionary().Keys);
    }

    [Fact]
    public async Task Claim_DamageCostOverLimit_IsRejected()
    {
        var validator = new ClaimValidator(new TestStore());
        var claim = CreateClaim();
        claim.DamageCost = 100_000.01m;

        var result = await validator.ValidateAsync(claim);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(Claim.DamageCost), result.ToDictionary().Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Claim_CreatedOutsideCover_IsRejected(bool afterEnd)
    {
        var store = new TestStore();
        var claim = CreateClaim();
        claim.Created = afterEnd ? Today.AddDays(30).AddTicks(1) : Today.AddTicks(-1);

        var result = await new ClaimValidator(store).ValidateAsync(claim);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(Claim.Created), result.ToDictionary().Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    public async Task Claim_AtDamageLimitAndWithinCover_IsAccepted(int day)
    {
        var store = new TestStore();
        var claim = CreateClaim();
        claim.Created = Today.AddDays(day);
        claim.DamageCost = 100_000m;

        var result = await new ClaimValidator(store).ValidateAsync(claim);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("missing-cover")]
    public async Task Claim_MissingRelatedCover_IsRejected(string coverId)
    {
        var store = new TestStore();
        var claim = CreateClaim();
        claim.CoverId = coverId;

        var result = await new ClaimValidator(store).ValidateAsync(claim);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(Claim.CoverId), result.ToDictionary().Keys);
    }

    [Fact]
    public async Task Claim_MultipleInvalidFields_ReportsAllErrors()
    {
        var store = new TestStore();
        var claim = CreateClaim();
        claim.DamageCost = 100_001m;
        claim.Created = Today.AddDays(-1);

        var result = await new ClaimValidator(store).ValidateAsync(claim);

        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(nameof(Claim.DamageCost), result.ToDictionary().Keys);
        Assert.Contains(nameof(Claim.Created), result.ToDictionary().Keys);
    }

    private static Claim CreateClaim() => new()
    {
        Id = "original-id",
        CoverId = "cover-id",
        Name = "Collision",
        Created = Today.AddDays(15),
        DamageCost = 25_000m
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Today.AddHours(12));
    }

    private sealed class TestStore : ICoverRepository
    {
        public Task<Cover?> GetByIdAsync(string id) => Task.FromResult<Cover?>(id == "cover-id"
            ? new Cover { Id = id, StartDate = Today, EndDate = Today.AddDays(30) }
            : null);

        public Task<IReadOnlyList<Cover>> GetAllAsync() => throw new NotSupportedException();
        public Task AddAsync(Cover cover) => throw new NotSupportedException();
        public Task DeleteAsync(string id) => throw new NotSupportedException();
    }
}
