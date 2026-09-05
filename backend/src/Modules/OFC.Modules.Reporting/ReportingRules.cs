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

    public static readonly string[] ReportCodes =
    [
        "sales", "discounts", "taxes", "payments", "cancellations", "voids", "refunds",
        "shifts-cash", "inventory", "low-stock", "kitchen", "channels", "dashboard"
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
}
