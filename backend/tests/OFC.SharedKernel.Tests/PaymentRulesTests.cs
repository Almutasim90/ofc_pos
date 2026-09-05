using OFC.Modules.Payments;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class PaymentRulesTests
{
    [Theory]
    [InlineData(PaymentStatus.Pending, PaymentStatus.Captured, true)]
    [InlineData(PaymentStatus.Authorized, PaymentStatus.Reversed, true)]
    [InlineData(PaymentStatus.Failed, PaymentStatus.Captured, false)]
    [InlineData(PaymentStatus.Captured, PaymentStatus.Authorized, false)]
    public void Lifecycle_transitions_are_explicit(PaymentStatus from, PaymentStatus to, bool expected) => Assert.Equal(expected, PaymentRules.CanTransition(from, to));

    [Fact]
    public void Money_rounding_is_deterministic() => Assert.Equal(2.7001m, PaymentRules.RoundMoney(2.70005m));
}
