using OFC.Modules.Ordering;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class CancellationRulesTests
{
    [Theory]
    [InlineData(OrderStatus.SentToKitchen, true)]
    [InlineData(OrderStatus.Preparing, true)]
    [InlineData(OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Pending, false)]
    public void Kitchen_cancellation_flag_tracks_kitchen_states(OrderStatus status, bool expected) => Assert.Equal(expected, CancellationRules.WasSentToKitchen(status));

    [Fact]
    public void Orders_are_never_transitioned_from_paid_to_cancelled() => Assert.False(OrderRules.CanTransition(OrderStatus.Paid, OrderStatus.Cancelled));

    [Theory]
    [InlineData("OTHER", false, true)]
    [InlineData("KITCHEN_DELAY", true, true)]
    [InlineData("KITCHEN_DELAY", false, false)]
    public void Other_reason_always_requires_a_note(string code, bool configured, bool expected) => Assert.Equal(expected, CancellationRules.RequiresNote(new CancellationReason { Code = code, NameAr = "سبب", NameEn = "Reason", RequiresNote = configured }));
}
