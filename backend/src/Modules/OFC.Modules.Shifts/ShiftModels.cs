namespace OFC.Modules.Shifts;

public enum ShiftStatus { Open = 0, Closed = 1, Reviewed = 2 }
public enum ShiftReviewStatus { NotReviewed = 0, Approved = 1, Rejected = 2 }
public enum ShiftMovementType { CashIn = 0, CashOut = 1, PettyCash = 2, CashDrop = 3 }

public sealed class Shift
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid BranchId { get; set; }
    public Guid OpenedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal OpeningCash { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Open;

    // Populated at blind close; never surfaced to the cashier while open.
    public decimal? CashSales { get; set; }
    public decimal? CardSales { get; set; }
    public decimal? CashRefunds { get; set; }
    public decimal? CardRefunds { get; set; }
    public decimal? CashInTotal { get; set; }
    public decimal? CashOutTotal { get; set; }
    public decimal? PettyCashTotal { get; set; }
    public decimal? CashDropsTotal { get; set; }
    public decimal? ExpectedCash { get; set; }
    public decimal? CardExpectedTotal { get; set; }
    public decimal? ActualCash { get; set; }
    public decimal? ActualCardTotal { get; set; }
    public decimal? CashVariance { get; set; }
    public decimal? CardVariance { get; set; }

    public Guid? ClosedByUserId { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    public ShiftReviewStatus ReviewStatus { get; set; } = ShiftReviewStatus.NotReviewed;
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public ICollection<ShiftMovement> Movements { get; set; } = [];
    public ICollection<ShiftDenomination> Denominations { get; set; } = [];
}

public sealed class ShiftMovement
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ShiftId { get; set; }
    public ShiftMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ShiftDenomination
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ShiftId { get; set; }
    public decimal Denomination { get; set; }
    public int Count { get; set; }
    public decimal Total { get; set; }
}

public static class ShiftRules
{
    public const int ReasonMax = 200;
    public const int NoteMax = 500;
    public const int MovementCountMax = 200;
    public const int DenominationKindMax = 30;
    public const decimal MoneyTolerance = 0.0001m;

    public static readonly decimal[] SupportedDenominations = [50m, 20m, 10m, 5m, 1m, 0.5m, 0.1m, 0.05m, 0.025m];

    public static decimal RoundMoney(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    public static decimal ExpectedCash(decimal openingFloat, decimal cashSales, decimal cashRefunds, decimal cashIn, decimal cashOut, decimal pettyCash, decimal cashDrops)
        => RoundMoney(openingFloat + cashSales + cashIn - cashRefunds - cashOut - pettyCash - cashDrops);

    public static decimal Variance(decimal actualCash, decimal expectedCash) => RoundMoney(actualCash - expectedCash);

    public static decimal DenominationTotal(IEnumerable<ShiftDenomination> denominations)
        => RoundMoney(denominations.Sum(x => RoundMoney(x.Denomination * x.Count)));

    public static decimal DenominationTotal(IEnumerable<(decimal Denomination, int Count)> denominations)
        => RoundMoney(denominations.Sum(x => RoundMoney(x.Denomination * x.Count)));

    public static bool DenominationSumMatches(decimal actualCash, IEnumerable<(decimal Denomination, int Count)> denominations)
        => Math.Abs(DenominationTotal(denominations) - actualCash) <= MoneyTolerance;

    public static decimal MovementEffect(ShiftMovementType type, decimal amount) => type switch
    {
        ShiftMovementType.CashIn => amount,
        ShiftMovementType.CashOut => -amount,
        ShiftMovementType.PettyCash => -amount,
        ShiftMovementType.CashDrop => -amount,
        _ => 0m
    };

    public static bool IsValidDenomination(decimal denomination) => SupportedDenominations.Contains(denomination);
}
