using Claims.Domain;
using Xunit;

namespace Claims.Tests;

public class PremiumCalculatorTests
{
    private static readonly DateTime StartDate = new(2030, 1, 1);

    [Theory]
    [InlineData(CoverType.Yacht, 1375)]
    [InlineData(CoverType.PassengerShip, 1500)]
    [InlineData(CoverType.Tanker, 1875)]
    [InlineData(CoverType.ContainerShip, 1625)]
    [InlineData(CoverType.BulkCarrier, 1625)]
    public void Calculate_OneDay_UsesCoverTypeRate(CoverType coverType, int expected)
    {
        Assert.Equal((decimal)expected, PremiumCalculator.Calculate(StartDate, StartDate.AddDays(1), coverType));
    }

    public static TheoryData<CoverType, int, decimal> TierCases => new()
    {
        { CoverType.Yacht, 30, 41_250m },
        { CoverType.Yacht, 31, 42_556.25m },
        { CoverType.Yacht, 180, 237_187.50m },
        { CoverType.Yacht, 181, 238_452.50m },
        { CoverType.ContainerShip, 30, 48_750m },
        { CoverType.ContainerShip, 31, 50_342.50m },
        { CoverType.ContainerShip, 180, 287_625m },
        { CoverType.ContainerShip, 181, 289_201.25m },
        { CoverType.Yacht, 366, 472_477.50m }
    };

    [Theory]
    [MemberData(nameof(TierCases))]
    public void Calculate_ProgressivelyPricesDaysInEachBand(CoverType coverType, int days, decimal expected)
    {
        Assert.Equal(expected, PremiumCalculator.Calculate(StartDate, StartDate.AddDays(days), coverType));
    }

    [Fact]
    public void Calculate_ZeroLengthPeriod_ReturnsZero()
    {
        Assert.Equal(0m, PremiumCalculator.Calculate(StartDate, StartDate, CoverType.Yacht));
    }

    [Fact]
    public void Calculate_PartialDayAfterDay30_ChargesOneDiscountedDay()
    {
        var endDate = StartDate.AddDays(30).AddHours(1);

        Assert.Equal(42_556.25m, PremiumCalculator.Calculate(StartDate, endDate, CoverType.Yacht));
    }
}
