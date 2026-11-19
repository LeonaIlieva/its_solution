namespace Claims.Domain;

public static class PremiumCalculator
{
    private const decimal BaseDayRate = 1250m;
    private const int LastFullRateDay = 30;
    private const int LastMidTierDay = 180;

    public static decimal Calculate(DateTime startDate, DateTime endDate, CoverType coverType)
    {
        var dayRate = BaseDayRate * (coverType switch
        {
            CoverType.Yacht => 1.10m,
            CoverType.PassengerShip => 1.20m,
            CoverType.Tanker => 1.50m,
            _ => 1.30m
        });

        // A partial elapsed day is billed as a full day.
        var billableDays = Math.Max(0, (int)Math.Ceiling((endDate - startDate).TotalDays));
        // Split days into non-overlapping bands; discounts apply only within each band.
        var fullRateDays = Math.Min(billableDays, LastFullRateDay);
        var midTierDays = Math.Clamp(billableDays, LastFullRateDay, LastMidTierDay) - LastFullRateDay;
        var finalTierDays = Math.Max(0, billableDays - LastMidTierDay);

        var (midTierFactor, finalTierFactor) = coverType == CoverType.Yacht
            ? (0.95m, 0.92m)
            : (0.98m, 0.97m);

        return dayRate * (fullRateDays + midTierDays * midTierFactor + finalTierDays * finalTierFactor);
    }
}
