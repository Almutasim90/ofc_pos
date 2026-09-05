using OFC.Modules.Kitchen;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class KitchenRulesTests
{
    private static KitchenTicket Ticket(KitchenTicketStatus status = KitchenTicketStatus.New, KitchenDispatchStatus dispatch = KitchenDispatchStatus.SentToKds, DateTimeOffset? startedAt = null, DateTimeOffset? acknowledgedAt = null, int? targetMinutes = 15) =>
        new() { BranchId = Guid.NewGuid(), OrderId = Guid.NewGuid(), StationId = Guid.NewGuid(), Status = status, DispatchStatus = dispatch, TargetMinutes = targetMinutes, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10), StartedAt = startedAt, AcknowledgedAt = acknowledgedAt };

    [Theory]
    [InlineData(KitchenDispatchStatus.Pending, KitchenDispatchStatus.SentToKds, true)]
    [InlineData(KitchenDispatchStatus.Pending, KitchenDispatchStatus.PrintFallbackPending, true)]
    [InlineData(KitchenDispatchStatus.Pending, KitchenDispatchStatus.Cancelled, true)]
    [InlineData(KitchenDispatchStatus.SentToKds, KitchenDispatchStatus.KdsAcknowledged, true)]
    [InlineData(KitchenDispatchStatus.SentToKds, KitchenDispatchStatus.PrintFallbackPending, true)]
    [InlineData(KitchenDispatchStatus.SentToKds, KitchenDispatchStatus.Failed, true)]
    [InlineData(KitchenDispatchStatus.KdsAcknowledged, KitchenDispatchStatus.Cancelled, true)]
    [InlineData(KitchenDispatchStatus.PrintFallbackPending, KitchenDispatchStatus.PrintedFallback, true)]
    [InlineData(KitchenDispatchStatus.PrintFallbackPending, KitchenDispatchStatus.Failed, true)]
    [InlineData(KitchenDispatchStatus.PrintedFallback, KitchenDispatchStatus.Cancelled, true)]
    [InlineData(KitchenDispatchStatus.KdsAcknowledged, KitchenDispatchStatus.PrintedFallback, false)]
    [InlineData(KitchenDispatchStatus.Pending, KitchenDispatchStatus.KdsAcknowledged, false)]
    [InlineData(KitchenDispatchStatus.PrintedFallback, KitchenDispatchStatus.SentToKds, false)]
    [InlineData(KitchenDispatchStatus.Failed, KitchenDispatchStatus.KdsAcknowledged, false)]
    [InlineData(KitchenDispatchStatus.Cancelled, KitchenDispatchStatus.PrintedFallback, false)]
    public void Dispatch_transitions_follow_the_continuity_machine(KitchenDispatchStatus from, KitchenDispatchStatus to, bool expected)
        => Assert.Equal(expected, KitchenRules.CanDispatchTransition(from, to));

    [Theory]
    [InlineData(KitchenItemStatus.New, KitchenItemStatus.Preparing, true)]
    [InlineData(KitchenItemStatus.New, KitchenItemStatus.Cancelled, true)]
    [InlineData(KitchenItemStatus.Preparing, KitchenItemStatus.Ready, true)]
    [InlineData(KitchenItemStatus.Preparing, KitchenItemStatus.Cancelled, true)]
    [InlineData(KitchenItemStatus.Ready, KitchenItemStatus.Completed, true)]
    [InlineData(KitchenItemStatus.Ready, KitchenItemStatus.Cancelled, true)]
    [InlineData(KitchenItemStatus.Completed, KitchenItemStatus.Ready, false)]
    [InlineData(KitchenItemStatus.Cancelled, KitchenItemStatus.Ready, false)]
    [InlineData(KitchenItemStatus.New, KitchenItemStatus.Completed, false)]
    public void Item_statuses_initially_follow_the_production_machine(KitchenItemStatus from, KitchenItemStatus to, bool expected)
        => Assert.Equal(expected, KitchenRules.CanItemTransition(from, to));

    [Theory]
    [InlineData(KitchenTicketStatus.New, KitchenTicketStatus.Preparing, true)]
    [InlineData(KitchenTicketStatus.New, KitchenTicketStatus.Cancelled, true)]
    [InlineData(KitchenTicketStatus.Preparing, KitchenTicketStatus.Ready, true)]
    [InlineData(KitchenTicketStatus.Preparing, KitchenTicketStatus.Cancelled, true)]
    [InlineData(KitchenTicketStatus.Ready, KitchenTicketStatus.Completed, true)]
    [InlineData(KitchenTicketStatus.Ready, KitchenTicketStatus.Cancelled, true)]
    [InlineData(KitchenTicketStatus.Completed, KitchenTicketStatus.Ready, false)]
    [InlineData(KitchenTicketStatus.Cancelled, KitchenTicketStatus.Completed, false)]
    public void Ticket_statuses_follow_the_production_machine(KitchenTicketStatus from, KitchenTicketStatus to, bool expected)
        => Assert.Equal(expected, KitchenRules.CanTicketTransition(from, to));

    [Fact]
    public void Fallback_is_not_triggered_while_the_kds_is_available()
        => Assert.False(KitchenRules.ShouldTriggerFallback(kdsAvailable: true, kdsAttempts: 5, createdAt: DateTimeOffset.UtcNow.AddMinutes(-5), now: DateTimeOffset.UtcNow));

    [Fact]
    public void Fallback_is_not_triggered_after_a_single_transient_error_within_timeout()
        => Assert.False(KitchenRules.ShouldTriggerFallback(kdsAvailable: false, kdsAttempts: 1, createdAt: DateTimeOffset.UtcNow, now: DateTimeOffset.UtcNow));

    [Fact]
    public void Fallback_triggered_once_the_attempt_budget_is_exhausted()
        => Assert.True(KitchenRules.ShouldTriggerFallback(kdsAvailable: false, kdsAttempts: KitchenRules.MaxKdsAttempts, createdAt: DateTimeOffset.UtcNow, now: DateTimeOffset.UtcNow.AddSeconds(1)));

    [Fact]
    public void Fallback_triggered_once_the_kds_timeout_elapses()
        => Assert.True(KitchenRules.ShouldTriggerFallback(kdsAvailable: false, kdsAttempts: 0, createdAt: DateTimeOffset.UtcNow, now: DateTimeOffset.UtcNow.AddSeconds(KitchenRules.KdsTimeoutSeconds)));

    [Fact]
    public void A_completed_ticket_is_never_overdue()
    {
        var ticket = Ticket(KitchenTicketStatus.Completed);
        Assert.False(KitchenRules.IsOverdue(ticket, DateTimeOffset.UtcNow.AddHours(2)));
    }

    [Fact]
    public void A_cancelled_ticket_is_never_overdue()
    {
        var ticket = Ticket(KitchenTicketStatus.Cancelled);
        Assert.False(KitchenRules.IsOverdue(ticket, DateTimeOffset.UtcNow.AddHours(2)));
    }

    [Fact]
    public void A_started_ticket_past_its_target_is_overdue()
    {
        var started = DateTimeOffset.UtcNow.AddMinutes(-20);
        var ticket = Ticket(KitchenTicketStatus.Preparing, startedAt: started, targetMinutes: 15);
        Assert.True(KitchenRules.IsOverdue(ticket, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void A_ticket_within_its_target_is_not_overdue()
    {
        var started = DateTimeOffset.UtcNow.AddMinutes(-5);
        var ticket = Ticket(KitchenTicketStatus.Preparing, startedAt: started, targetMinutes: 15);
        Assert.False(KitchenRules.IsOverdue(ticket, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void An_active_ticket_is_one_still_in_production()
    {
        var active = Ticket(KitchenTicketStatus.Preparing, KitchenDispatchStatus.KdsAcknowledged);
        Assert.True(KitchenRules.IsActive(active));
        var terminal = Ticket(KitchenTicketStatus.Completed, KitchenDispatchStatus.PrintedFallback);
        Assert.False(KitchenRules.IsActive(terminal));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(15, true)]
    [InlineData(999, true)]
    [InlineData(0, false)]
    [InlineData(1000, false)]
    public void Target_preparation_time_must_be_within_range(int? minutes, bool expected)
        => Assert.Equal(expected, KitchenRules.ValidTargetMinutes(minutes));
}
