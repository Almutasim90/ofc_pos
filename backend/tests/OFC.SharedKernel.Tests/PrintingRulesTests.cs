using OFC.Modules.Printing;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class PrintingRulesTests
{
    private static PrintJob Job(PrintJobStatus status, int attempts = 0, int maxAttempts = 3, DateTimeOffset? nextAttemptAt = null) =>
        new() { BranchId = Guid.NewGuid(), ClientRequestId = Guid.NewGuid(), Kind = PrintJobKind.Receipt, Status = status, AttemptCount = attempts, MaxAttempts = maxAttempts, NextAttemptAt = nextAttemptAt };

    [Theory]
    [InlineData(PrintJobStatus.Pending, PrintJobStatus.Printing, true)]
    [InlineData(PrintJobStatus.Pending, PrintJobStatus.Failed, true)]
    [InlineData(PrintJobStatus.Retrying, PrintJobStatus.Printing, true)]
    [InlineData(PrintJobStatus.Printing, PrintJobStatus.Printed, true)]
    [InlineData(PrintJobStatus.Printing, PrintJobStatus.Failed, true)]
    [InlineData(PrintJobStatus.Printing, PrintJobStatus.Retrying, true)]
    [InlineData(PrintJobStatus.Failed, PrintJobStatus.Retrying, true)]
    [InlineData(PrintJobStatus.Printed, PrintJobStatus.Pending, false)]
    [InlineData(PrintJobStatus.Failed, PrintJobStatus.Printing, false)]
    [InlineData(PrintJobStatus.Pending, PrintJobStatus.Printed, false)]
    public void State_transitions_follow_the_outbox_machine(PrintJobStatus from, PrintJobStatus to, bool expected)
        => Assert.Equal(expected, PrintingRules.CanTransition(from, to));

    [Fact]
    public void A_failed_job_with_budget_left_is_retryable_when_its_backoff_elapsed()
    {
        var job = Job(PrintJobStatus.Failed, attempts: 1, maxAttempts: 3, nextAttemptAt: DateTimeOffset.UtcNow.AddSeconds(-1));
        Assert.True(PrintingRules.ShouldRetry(job, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_failed_job_is_not_retryable_after_exhausting_attempts()
        => Assert.False(PrintingRules.ShouldRetry(Job(PrintJobStatus.Failed, attempts: 3, maxAttempts: 3), DateTimeOffset.UtcNow));

    [Fact]
    public void A_failed_job_is_not_retryable_before_its_backoff_elapsed()
    {
        var job = Job(PrintJobStatus.Failed, attempts: 1, maxAttempts: 3, nextAttemptAt: DateTimeOffset.UtcNow.AddSeconds(60));
        Assert.False(PrintingRules.ShouldRetry(job, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_completed_job_is_not_retryable()
        => Assert.False(PrintingRules.ShouldRetry(Job(PrintJobStatus.Printed), DateTimeOffset.UtcNow));

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 30)]
    [InlineData(3, 120)]
    public void Retry_backoff_backs_off_exponentially(int attemptCount, int expectedSeconds)
        => Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), PrintingRules.RetryBackoff(attemptCount));

    [Fact]
    public void Route_picks_the_high_priority_route_for_the_product_station()
    {
        var stationId = Guid.NewGuid();
        var low = new PrinterRoute { BranchId = Guid.NewGuid(), PreparationStationId = stationId, Priority = 10, IsActive = true };
        var high = new PrinterRoute { BranchId = Guid.NewGuid(), PreparationStationId = stationId, Priority = 1, IsActive = true };
        Assert.Same(high, PrintingRules.PickRoute(new[] { low, high }, stationId));
    }

    [Fact]
    public void Route_falls_back_to_the_default_route_when_no_station_route_exists()
    {
        var defaultRoute = new PrinterRoute { BranchId = Guid.NewGuid(), PreparationStationId = null, Priority = 1, IsActive = true };
        Assert.Same(defaultRoute, PrintingRules.PickRoute(new[] { defaultRoute }, Guid.NewGuid()));
    }

    [Fact]
    public void Route_does_not_resolve_inactive_routes()
    {
        var inactive = new PrinterRoute { BranchId = Guid.NewGuid(), PreparationStationId = Guid.NewGuid(), Priority = 1, IsActive = false };
        Assert.Null(PrintingRules.PickRoute(new[] { inactive }, inactive.PreparationStationId));
    }

    [Theory]
    [InlineData(42, true)]
    [InlineData(20, true)]
    [InlineData(120, true)]
    [InlineData(19, false)]
    [InlineData(121, false)]
    public void Template_width_must_remain_within_the_supported_range(int width, bool expected)
        => Assert.Equal(expected, PrintingRules.ValidWidth(width));
}
