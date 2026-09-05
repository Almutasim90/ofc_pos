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
        var expected = new[] { "sales", "discounts", "taxes", "payments", "cancellations", "voids", "refunds", "shifts-cash", "inventory", "low-stock", "kitchen", "channels", "dashboard" };
        Assert.Equal(expected, ReportingRules.ReportCodes);
    }
}
