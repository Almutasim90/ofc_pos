using OFC.Modules.Reporting;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class ReportingRulesTests
{
    [Fact]
    public void Round_money_uses_four_decimals_and_away_from_zero()
    {
        Assert.Equal(2.7000m, ReportingRules.RoundMoney(2.7m));
        Assert.Equal(2.7001m, ReportingRules.RoundMoney(2.70012m));
        Assert.Equal(0.0001m, ReportingRules.RoundMoney(0.00005m));
    }

    [Fact]
    public void Safe_rate_is_zero_when_the_denominator_is_zero()
    {
        Assert.Equal(0m, ReportingRules.SafeRate(10m, 0m));
        Assert.Equal(0.5m, ReportingRules.SafeRate(5m, 10m));
        Assert.Equal(0.3333m, ReportingRules.SafeRate(1m, 3m));
    }

    [Fact]
    public void Only_known_report_codes_and_formats_are_accepted()
    {
        Assert.True(ReportingRules.IsKnownReport("sales"));
        Assert.True(ReportingRules.IsKnownReport("cancellations"));
        Assert.True(ReportingRules.IsKnownReport("dashboard"));
        Assert.False(ReportingRules.IsKnownReport("procurement"));
        Assert.False(ReportingRules.IsKnownReport(null));

        Assert.True(ReportingRules.ValidFormat("csv"));
        Assert.False(ReportingRules.ValidFormat("xlsx"));
        Assert.False(ReportingRules.ValidFormat(null));
    }

    [Fact]
    public void Low_stock_is_detected_at_or_below_the_threshold()
    {
        Assert.True(ReportingRules.IsLowStock(3m, 5m));
        Assert.True(ReportingRules.IsLowStock(5m, 5m));
        Assert.False(ReportingRules.IsLowStock(6m, 5m));
        Assert.False(ReportingRules.IsLowStock(null, 5m));
    }

    [Fact]
    public void Pagination_is_normalised_to_a_safe_range()
    {
        Assert.Equal(50, ReportingRules.NormalizePageSize(0));
        Assert.Equal(50, ReportingRules.NormalizePageSize(-5));
        Assert.Equal(200, ReportingRules.NormalizePageSize(200));
        Assert.Equal(200, ReportingRules.NormalizePageSize(999));
        Assert.Equal(1, ReportingRules.NormalizePage(0));
        Assert.Equal(4, ReportingRules.NormalizePage(4));
    }

    [Fact]
    public void Preparation_duration_is_bounded_and_nullable()
    {
        var started = DateTimeOffset.UtcNow;
        var completed = started.AddMinutes(8);
        Assert.Equal(8, ReportingRules.PrepDurationMinutes(started, completed));
        Assert.Equal(0, ReportingRules.PrepDurationMinutes(started, started.AddMinutes(-3)));
        Assert.Null(ReportingRules.PrepDurationMinutes(null, completed));
        Assert.Null(ReportingRules.PrepDurationMinutes(started, null));
    }

    [Fact]
    public void Retention_marks_entries_older_than_the_policy_window_as_expired()
    {
        var now = new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);
        var withinWindow = now - TimeSpan.FromDays(ReportingRules.AuditRetentionDays - 1);
        var beyondWindow = now - TimeSpan.FromDays(ReportingRules.AuditRetentionDays + 1);

        Assert.False(ReportingRules.IsExpiredForRetention(withinWindow, now));
        Assert.True(ReportingRules.IsExpiredForRetention(beyondWindow, now));
        Assert.False(ReportingRules.IsExpiredExport(now, now));
        Assert.True(ReportingRules.IsExpiredExport(now - TimeSpan.FromDays(ReportingRules.ExportRetentionDays + 1), now));
    }

    [Fact]
    public void The_report_catalog_is_a_stable_known_set()
    {
        var expected = new[] { "sales", "discounts", "taxes", "payments", "cancellations", "voids", "refunds", "shifts-cash", "inventory", "low-stock", "kitchen", "channels", "dashboard", "branch-comparison", "cancellation-analytics", "kitchen-performance", "food-cost", "inventory-trends", "profit-loss", "alerts" };
        Assert.Equal(expected, ReportingRules.ReportCodes);
    }

    [Fact]
    public void Sprint_seventeen_reports_are_known_and_exportable()
    {
        Assert.True(ReportingRules.IsKnownReport("branch-comparison"));
        Assert.True(ReportingRules.IsKnownReport("food-cost"));
        Assert.True(ReportingRules.IsKnownReport("profit-loss"));
        Assert.True(ReportingRules.IsKnownReport("alerts"));
        Assert.True(ReportingRules.ValidFormat("csv"));
    }

    [Fact]
    public void Day_start_and_day_key_are_normalised_utc()
    {
        var value = new DateTimeOffset(2026, 9, 5, 15, 30, 0, TimeSpan.Zero);
        Assert.Equal("2026-09-05", ReportingRules.DayKey(value));
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero), ReportingRules.DayStart(value));
    }

    [Fact]
    public void On_time_detection_compares_prep_minutes_to_target()
    {
        Assert.True(ReportingRules.IsOnTime(10, 15));
        Assert.True(ReportingRules.IsOnTime(15, 15));
        Assert.False(ReportingRules.IsOnTime(16, 15));
        Assert.False(ReportingRules.IsOnTime(null, 15));
        Assert.False(ReportingRules.IsOnTime(10, null));
    }

    [Fact]
    public void Rates_and_percent_change_are_computed_safely()
    {
        Assert.Equal(0m, ReportingRules.RatePercent(0, 0));
        Assert.Equal(80m, ReportingRules.RatePercent(8, 10));
        Assert.Equal(0m, ReportingRules.Percent(0m, 0m));
        Assert.Equal(50m, ReportingRules.Percent(5m, 10m));
        Assert.Equal(0m, ReportingRules.ChangePercent(0m, 0m));
        Assert.Equal(100m, ReportingRules.ChangePercent(10m, 0m));
        Assert.Equal(50m, ReportingRules.ChangePercent(150m, 100m));
        Assert.Equal(-50m, ReportingRules.ChangePercent(50m, 100m));
    }

    [Fact]
    public void Operational_threshold_rules_flag_issues()
    {
        Assert.True(ReportingRules.IsCautionRate(0.11m));
        Assert.False(ReportingRules.IsCautionRate(0.09m));
        Assert.True(ReportingRules.ExceedsFoodCostTarget(36m));
        Assert.False(ReportingRules.ExceedsFoodCostTarget(35m));
        Assert.True(ReportingRules.ExceedsWasteCaution(2.1m));
        Assert.False(ReportingRules.ExceedsWasteCaution(1.9m));
        Assert.True(ReportingRules.IsSignificantShiftVariance(0.05m));
        Assert.False(ReportingRules.IsSignificantShiftVariance(0.001m));
        Assert.False(ReportingRules.IsSignificantShiftVariance(null));
    }
}
