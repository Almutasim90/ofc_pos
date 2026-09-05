using OFC.Modules.Shifts;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class ShiftRulesTests
{
    [Fact]
    public void Expected_cash_follows_srs_formula()
    {
        var expected = ShiftRules.ExpectedCash(
            openingFloat: 50m,
            cashSales: 120.5m,
            cashRefunds: 10m,
            cashIn: 30m,
            cashOut: 5m,
            pettyCash: 8.25m,
            cashDrops: 20m);
        Assert.Equal(157.25m, expected);
    }

    [Fact]
    public void Cash_variance_is_actual_minus_expected()
        => Assert.Equal(2.75m, ShiftRules.Variance(160m, 157.25m));

    [Fact]
    public void Denomination_total_sums_each_denomination_multiplied_by_count()
    {
        var denominations = new[] { (Denomination: 50m, Count: 1), (Denomination: 1m, Count: 3), (Denomination: 0.5m, Count: 4) };
        Assert.Equal(55m, ShiftRules.DenominationTotal(denominations));
    }

    [Fact]
    public void Denomination_sum_matches_entered_cash_within_tolerance()
    {
        var denominations = new[] { (Denomination: 10m, Count: 2), (Denomination: 1m, Count: 5) };
        Assert.True(ShiftRules.DenominationSumMatches(25m, denominations));
        Assert.False(ShiftRules.DenominationSumMatches(26m, denominations));
    }

    [Theory]
    [InlineData(ShiftMovementType.CashIn, 30d, 30d)]
    [InlineData(ShiftMovementType.CashOut, 5d, -5d)]
    [InlineData(ShiftMovementType.PettyCash, 8.25d, -8.25d)]
    [InlineData(ShiftMovementType.CashDrop, 20d, -20d)]
    public void Movement_effect_depends_on_direction(ShiftMovementType type, double amount, double expected)
        => Assert.Equal((decimal)expected, ShiftRules.MovementEffect(type, (decimal)amount));

    [Theory]
    [InlineData(50d, true)]
    [InlineData(0.025d, true)]
    [InlineData(3d, false)]
    public void Denomination_is_validated_against_supported_set(double value, bool expected)
        => Assert.Equal(expected, ShiftRules.IsValidDenomination((decimal)value));

    [Fact]
    public void Money_rounding_is_deterministic()
        => Assert.Equal(2.7501m, ShiftRules.RoundMoney(2.75005m));
}
