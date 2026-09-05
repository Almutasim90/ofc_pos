using OFC.Modules.Ordering;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class OrderRulesTests
{
    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Ready, OrderStatus.Completed, true)]
    [InlineData(OrderStatus.Completed, OrderStatus.Draft, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    public void Status_transitions_are_explicit(OrderStatus from, OrderStatus to, bool expected) => Assert.Equal(expected, OrderRules.CanTransition(from, to));
}
