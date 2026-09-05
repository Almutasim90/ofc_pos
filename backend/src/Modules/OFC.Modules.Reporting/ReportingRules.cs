namespace OFC.Modules.Reporting;

public static class ReportingRules
{
    public const int MoneyPrecision = 4;
    public const int QueryDefaultPageSize = 50;
    public const int QueryMaxPageSize = 200;
    public const int ExportMaxRows = 2000;
    public const int AuditRetentionDays = 180;
    public const int ExportRetentionDays = 30;
    public const decimal LowStockDefaultThreshold = 5m;
    public const int DashboardDefaultDays = 1;
    public const int InventoryTrendDefaultDays = 30;
    public const int KitchenOnTimeTargetMinutes = 15;
    public const int KitchenOnTimeTargetPercent = 80;
    public const int FoodCostTargetPercent = 35;
    public const decimal CancellationRateCaution = 0.10m;
    public const decimal WasteCautionPercent = 2m;
    public const decimal ShiftVarianceTolerance = 0.01m;

    public static readonly string[] ReportCodes =
    [
        "sales", "discounts", "taxes", "payments", "cancellations", "voids", "refunds",
        "shifts-cash", "inventory", "low-stock", "kitchen", "channels", "dashboard",
        "branch-comparison", "cancellation-analytics", "kitchen-performance", "food-cost",
        "inventory-trends", "profit-loss", "alerts"
    ];

    public static readonly string[] ExportFormats = ["csv"];

    public static decimal RoundMoney(decimal value) => decimal.Round(value, MoneyPrecision, MidpointRounding.AwayFromZero);

    public static decimal SafeRate(decimal numerator, decimal denominator) =>
        denominator == 0m ? 0m : RoundMoney(numerator / denominator);

    public static bool IsKnownReport(string? code) => code is not null && ReportCodes.Contains(code);

    public static bool ValidFormat(string? format) => format is not null && ExportFormats.Contains(format);

    public static bool IsLowStock(decimal? stockOnHand, decimal threshold) =>
        stockOnHand.HasValue && stockOnHand.Value <= threshold;

    public static int NormalizePageSize(int requested) => Math.Clamp(requested <= 0 ? QueryDefaultPageSize : requested, 1, QueryMaxPageSize);

    public static int NormalizePage(int requested) => Math.Max(requested <= 0 ? 1 : requested, 1);

    public static int? PrepDurationMinutes(DateTimeOffset? started, DateTimeOffset? completed)
    {
        if (!started.HasValue || !completed.HasValue) return null;
        var minutes = (int)Math.Round((completed.Value - started.Value).TotalMinutes, MidpointRounding.AwayFromZero);
        return Math.Max(minutes, 0);
    }

    public static DateTimeOffset AuditRetentionCutoff(DateTimeOffset now) => now - TimeSpan.FromDays(AuditRetentionDays);

    public static DateTimeOffset ExportRetentionCutoff(DateTimeOffset now) => now - TimeSpan.FromDays(ExportRetentionDays);

    public static bool IsExpiredForRetention(DateTimeOffset occurredAt, DateTimeOffset now) => occurredAt < AuditRetentionCutoff(now);

    public static bool IsExpiredExport(DateTimeOffset createdAt, DateTimeOffset now) => createdAt < ExportRetentionCutoff(now);

    public static DateTimeOffset DayStart(DateTimeOffset value) => new(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);

    public static string DayKey(DateTimeOffset value) => value.ToString("yyyy-MM-dd");

    public static bool IsOnTime(int? prepMinutes, int? targetMinutes) =>
        prepMinutes.HasValue && targetMinutes.HasValue && prepMinutes.Value <= targetMinutes.Value;

    public static decimal RatePercent(int numerator, int denominator) =>
        denominator == 0 ? 0m : RoundMoney((decimal)numerator / denominator * 100m);

    public static decimal Percent(decimal numerator, decimal denominator) =>
        denominator == 0 ? 0m : RoundMoney(numerator / denominator * 100m);

    public static decimal ChangePercent(decimal current, decimal previous) =>
        previous == 0m ? (current == 0m ? 0m : 100m) : RoundMoney((current - previous) / previous * 100m);

    public static bool IsCautionRate(decimal rate) => rate > CancellationRateCaution;

    public static bool ExceedsFoodCostTarget(decimal foodCostPercent) => foodCostPercent > FoodCostTargetPercent;

    public static bool ExceedsWasteCaution(decimal wastePercent) => wastePercent > WasteCautionPercent;

    public static bool IsSignificantShiftVariance(decimal? variance) => variance.HasValue && Math.Abs(variance.Value) > ShiftVarianceTolerance;
}
