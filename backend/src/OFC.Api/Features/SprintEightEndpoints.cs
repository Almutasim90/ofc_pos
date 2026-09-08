using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Payments;
using OFC.Modules.Shifts;

namespace OFC.Api.Features;

public static class SprintEightEndpoints
{
    public static void MapSprintEightEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/shifts", List).RequireAuthorization();
        api.MapGet("/shifts/current", Current).RequireAuthorization();
        api.MapGet("/shifts/{id:guid}", Get).RequireAuthorization();
        api.MapPost("/shifts", Open).RequireAuthorization();
        api.MapPost("/shifts/{id:guid}/movements", Move).RequireAuthorization();
        api.MapPost("/shifts/{id:guid}/blind-close", BlindClose).RequireAuthorization();
        api.MapPost("/shifts/{id:guid}/review", Review).RequireAuthorization();
        api.MapGet("/shifts/report", Report).RequireAuthorization();
    }

    private static async Task<IResult> List(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!(user.HasClaim("permission", "shifts.report") || user.HasClaim("permission", "shifts.view-variance") || user.HasClaim("permission", "shifts.manage")) || !await HasBranch(db, user, branchId, ct)) return Forbidden();
        return Results.Ok(await db.Shifts.AsNoTracking().Where(x => x.BranchId == branchId).OrderByDescending(x => x.OpenedAt).Take(100).Select(x => new { x.Id, x.Status, x.OpeningCash, x.OpenedAt, x.ClosedAt, x.OpenedByUserId, x.ClosedByUserId, x.ExpectedCash, x.ActualCash, x.CashVariance, x.CardVariance, x.ReviewStatus }).ToListAsync(ct));
    }

    private static async Task<IResult> Current(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await HasBranch(db, user, branchId, ct)) return Forbidden();
        var shift = await db.Shifts.AsNoTracking().Include(x => x.Movements).SingleOrDefaultAsync(x => x.BranchId == branchId && x.Status == ShiftStatus.Open, ct);
        if (shift is null) return Results.Ok(new { shift = (object?)null });
        var ledger = await LiveLedger(db, shift, ct);
        return Results.Ok(new { shift = ShiftResponse(shift, ledger, includeClosed: false) });
    }

    private static async Task<IResult> Get(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var shift = await db.Shifts.Include(x => x.Movements).Include(x => x.Denominations).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (shift is null) return Results.NotFound();
        if (!await HasBranch(db, user, shift.BranchId, ct) || !(user.HasClaim("permission", "shifts.manage") || user.HasClaim("permission", "shifts.view-variance") || user.HasClaim("permission", "shifts.report") || user.HasClaim("permission", "shifts.open") || UserId(user) == shift.OpenedByUserId)) return Forbidden();
        var ledger = shift.Status == ShiftStatus.Open ? await LiveLedger(db, shift, ct) : new ShiftLedger(shift.CashSales ?? 0m, shift.CardSales ?? 0m, shift.CashRefunds ?? 0m, shift.CardRefunds ?? 0m);
        var includeClosed = shift.Status != ShiftStatus.Open && CanViewVariance(user, shift);
        return Results.Ok(ShiftResponse(shift, ledger, includeClosed));
    }

    private static async Task<IResult> Open(OpenRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!(user.HasClaim("permission", "shifts.open") || user.HasClaim("permission", "shifts.manage")) || !await HasBranch(db, user, request.BranchId, ct)) return Forbidden();
        if (request.OpeningCash < 0m) return Validation("openingCash", "The opening cash cannot be negative.");
        if (await db.Shifts.AnyAsync(x => x.BranchId == request.BranchId && x.Status == ShiftStatus.Open, ct)) return Validation("shift", "A shift is already open for this branch.");
        var shift = new Shift { BranchId = request.BranchId, OpenedByUserId = UserId(user), DeviceId = DeviceId(user), OpeningCash = ShiftRules.RoundMoney(request.OpeningCash) };
        db.Shifts.Add(shift);
        identity.Audit(UserId(user), shift.BranchId, DeviceId(user), "shift.open", "shift", shift.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { shift.OpeningCash }));
        // The AnyAsync check above is a fast-path UX check, not the guarantee: a partial unique index on
        // (BranchId) WHERE Status = 'Open' is what actually rejects two concurrent opens for one branch.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Validation("shift", "A shift is already open for this branch."); }
        return Results.Created($"/api/v1/shifts/{shift.Id}", new { shift.Id, shift.BranchId, shift.OpeningCash, shift.OpenedAt, shift.Status });
    }

    private static async Task<IResult> Move(Guid id, MovementRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var shift = await db.Shifts.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (shift is null) return Results.NotFound();
        if (!await HasBranch(db, user, shift.BranchId, ct) || !(user.HasClaim("permission", "shifts.manage") || UserId(user) == shift.OpenedByUserId)) return Forbidden();
        if (shift.Status != ShiftStatus.Open) return Validation("shift", "Cash movements can only be recorded on an open shift.");
        if (!Enum.IsDefined(request.Type) || request.Amount <= 0m || request.Amount > 1_000_000m || request.Reason?.Trim().Length > ShiftRules.ReasonMax || request.Note?.Trim().Length > ShiftRules.NoteMax) return Validation("movement", "Provide a valid cash movement type and amount.");
        var movement = new ShiftMovement { ShiftId = shift.Id, Type = request.Type, Amount = ShiftRules.RoundMoney(request.Amount), Reason = request.Reason?.Trim(), Note = request.Note?.Trim(), CreatedByUserId = UserId(user), DeviceId = DeviceId(user) };
        db.ShiftMovements.Add(movement);
        identity.Audit(UserId(user), shift.BranchId, DeviceId(user), "shift.movement", "shift_movement", movement.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { shiftId = shift.Id, movement.Type, movement.Amount, movement.Reason }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/shifts/{shift.Id}/movements/{movement.Id}", new { movement.Id, movement.Type, movement.Amount, movement.Reason, movement.CreatedAt });
    }

    private static async Task<IResult> BlindClose(Guid id, BlindCloseRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var shift = await db.Shifts.Include(x => x.Movements).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (shift is null) return Results.NotFound();
        if (!await HasBranch(db, user, shift.BranchId, ct) || !(user.HasClaim("permission", "shifts.close") || user.HasClaim("permission", "shifts.manage") || UserId(user) == shift.OpenedByUserId)) return Forbidden();
        if (shift.Status != ShiftStatus.Open) return Validation("shift", "This shift is already closed.");
        if (request.ActualCash < 0m || request.ActualCardTotal < 0m) return Validation("cash", "Actual cash and card totals cannot be negative.");
        if (request.Denominations is { Count: > ShiftRules.DenominationKindMax } || request.Denominations.Any(x => !ShiftRules.IsValidDenomination(x.Denomination) || x.Count is < 0 or > 10000)) return Validation("denominations", "Provide valid currency denominations and positive counts.");
        var actualCash = ShiftRules.RoundMoney(request.ActualCash);
        var denominationTotal = ShiftRules.DenominationTotal(request.Denominations.Select(x => (x.Denomination, x.Count)));
        if (!ShiftRules.DenominationSumMatches(actualCash, request.Denominations.Select(x => (x.Denomination, x.Count)))) return Validation("denominations", "The cash denomination total must match the entered actual cash.");

        var ledger = await LiveLedger(db, shift, ct);
        var cashIn = shift.Movements.Where(x => x.Type == ShiftMovementType.CashIn).Sum(x => x.Amount);
        var cashOut = shift.Movements.Where(x => x.Type == ShiftMovementType.CashOut).Sum(x => x.Amount);
        var pettyCash = shift.Movements.Where(x => x.Type == ShiftMovementType.PettyCash).Sum(x => x.Amount);
        var cashDrops = shift.Movements.Where(x => x.Type == ShiftMovementType.CashDrop).Sum(x => x.Amount);
        var expectedCash = ShiftRules.ExpectedCash(shift.OpeningCash, ledger.CashSales, ledger.CashRefunds, cashIn, cashOut, pettyCash, cashDrops);
        var cardExpected = ShiftRules.RoundMoney(ledger.CardSales - ledger.CardRefunds);
        var cashVariance = ShiftRules.Variance(actualCash, expectedCash);
        var cardVariance = ShiftRules.RoundMoney(request.ActualCardTotal - cardExpected);

        shift.CashSales = ledger.CashSales; shift.CardSales = ledger.CardSales; shift.CashRefunds = ledger.CashRefunds; shift.CardRefunds = ledger.CardRefunds;
        shift.CashInTotal = ShiftRules.RoundMoney(cashIn); shift.CashOutTotal = ShiftRules.RoundMoney(cashOut); shift.PettyCashTotal = ShiftRules.RoundMoney(pettyCash); shift.CashDropsTotal = ShiftRules.RoundMoney(cashDrops);
        shift.ExpectedCash = expectedCash; shift.CardExpectedTotal = cardExpected; shift.ActualCash = actualCash; shift.ActualCardTotal = ShiftRules.RoundMoney(request.ActualCardTotal);
        shift.CashVariance = cashVariance; shift.CardVariance = cardVariance;
        shift.ClosedByUserId = UserId(user); shift.ClosedAt = DateTimeOffset.UtcNow; shift.Status = ShiftStatus.Closed;
        foreach (var item in request.Denominations) shift.Denominations.Add(new ShiftDenomination { ShiftId = shift.Id, Denomination = item.Denomination, Count = item.Count, Total = ShiftRules.RoundMoney(item.Denomination * item.Count) });

        identity.Audit(UserId(user), shift.BranchId, DeviceId(user), "shift.blind-close", "shift", shift.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { expectedCash, shift.ActualCash, cashVariance, cardExpected, shift.ActualCardTotal, cardVariance, denominationTotal, movements = shift.Movements.Count }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { shift.Id, shift.Status, shift.OpeningCash, expectedCash, actualCash = shift.ActualCash, cashVariance, cardExpected, actualCardTotal = shift.ActualCardTotal, cardVariance, denominationTotal, shift.ClosedAt });
    }

    private static async Task<IResult> Review(Guid id, ReviewRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var shift = await db.Shifts.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (shift is null) return Results.NotFound();
        if (!await HasBranch(db, user, shift.BranchId, ct) || !(user.HasClaim("permission", "shifts.approve") || user.HasClaim("permission", "shifts.manage"))) return Forbidden();
        if (shift.Status == ShiftStatus.Open) return Validation("shift", "An open shift must be closed before it can be reviewed.");
        if (request.Status is not (ShiftReviewStatus.Approved or ShiftReviewStatus.Rejected)) return Validation("status", "Review status must be approved or rejected.");
        shift.ReviewStatus = request.Status; shift.ReviewedByUserId = UserId(user); shift.ReviewedAt = DateTimeOffset.UtcNow; shift.ReviewNote = request.Note?.Trim(); shift.Status = request.Status == ShiftReviewStatus.Approved ? ShiftStatus.Reviewed : ShiftStatus.Closed;
        identity.Audit(UserId(user), shift.BranchId, DeviceId(user), "shift.review", "shift", shift.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { shift.ReviewStatus, shift.ReviewNote, shift.CashVariance, shift.CardVariance }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { shift.Id, shift.Status, shift.ReviewStatus, shift.ReviewedByUserId, shift.ReviewedAt, shift.ReviewNote });
    }

    private static async Task<IResult> Report(Guid branchId, DateTimeOffset? from, DateTimeOffset? to, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!(user.HasClaim("permission", "shifts.report") || user.HasClaim("permission", "shifts.view-variance") || user.HasClaim("permission", "shifts.manage")) || !await HasBranch(db, user, branchId, ct)) return Forbidden();
        var start = from ?? DateTimeOffset.UtcNow.AddDays(-30); var end = to ?? DateTimeOffset.UtcNow;
        if (start > end || end - start > TimeSpan.FromDays(366)) return Validation("range", "Provide a date range of no more than 366 days.");
        var shifts = await db.Shifts.AsNoTracking().Where(x => x.BranchId == branchId && x.OpenedAt >= start && x.OpenedAt <= end).Join(db.Users, x => x.OpenedByUserId, u => u.Id, (x, u) => new { x, user = u.DisplayName }).ToListAsync(ct);
        var closed = shifts.Where(x => x.x.Status != ShiftStatus.Open).Select(x => x.x).ToList();
        return Results.Ok(new { count = shifts.Count, open = shifts.Count(x => x.x.Status == ShiftStatus.Open), closed = closed.Count, expectedCash = closed.Sum(x => x.ExpectedCash ?? 0m), actualCash = closed.Sum(x => x.ActualCash ?? 0m), cashVariance = closed.Sum(x => x.CashVariance ?? 0m), cardVariance = closed.Sum(x => x.CardVariance ?? 0m), byCashier = shifts.GroupBy(x => x.user).Select(x => new { user = x.Key, count = x.Count(), cashVariance = x.Sum(y => y.x.CashVariance ?? 0m) }), records = shifts.OrderByDescending(x => x.x.OpenedAt).Select(x => new { x.x.Id, x.x.Status, x.x.OpeningCash, x.x.OpenedAt, x.x.ClosedAt, x.x.ExpectedCash, x.x.ActualCash, x.x.CashVariance, x.x.CardVariance, x.x.ReviewStatus, user = x.user }) });
    }

    private static async Task<ShiftLedger> LiveLedger(OFCDbContext db, Shift shift, CancellationToken ct)
    {
        var transactions = await db.FinancialTransactions.AsNoTracking().Where(x => x.ShiftId == shift.Id).ToListAsync(ct);
        var methodIds = transactions.Select(x => x.PaymentMethodId).Distinct().ToList();
        var kinds = methodIds.Count == 0 ? new Dictionary<Guid, PaymentMethodKind>() : await db.PaymentMethods.AsNoTracking().Where(x => methodIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Kind, ct);
        decimal cashSales = 0m, cardSales = 0m, cashRefunds = 0m, cardRefunds = 0m;
        foreach (var t in transactions)
        {
            var isCash = kinds.TryGetValue(t.PaymentMethodId, out var kind) && PaymentRules.IsCash(kind);
            if (t.Type == FinancialTransactionType.Sale) { if (isCash) cashSales += t.Amount; else cardSales += t.Amount; }
            else if (t.Type == FinancialTransactionType.Refund) { var value = Math.Abs(t.Amount); if (isCash) cashRefunds += value; else cardRefunds += value; }
        }
        return new ShiftLedger(ShiftRules.RoundMoney(cashSales), ShiftRules.RoundMoney(cardSales), ShiftRules.RoundMoney(cashRefunds), ShiftRules.RoundMoney(cardRefunds));
    }

    private static object ShiftResponse(Shift shift, ShiftLedger ledger, bool includeClosed) => new
    {
        shift.Id, shift.Status, shift.OpeningCash, shift.OpenedAt, shift.OpenedByUserId, shift.ClosedAt, shift.ClosedByUserId,
        shift.ReviewStatus, shift.ReviewedByUserId, shift.ReviewedAt, shift.ReviewNote,
        ledger.CashSales, ledger.CardSales, ledger.CashRefunds, ledger.CardRefunds,
        cashVariance = includeClosed ? shift.CashVariance : null,
        cardVariance = includeClosed ? shift.CardVariance : null,
        expectedCash = includeClosed ? shift.ExpectedCash : null,
        actualCash = includeClosed ? shift.ActualCash : null,
        movements = shift.Movements.OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.Type, x.Amount, x.Reason, x.Note, x.CreatedAt }),
        denominations = shift.Denominations.OrderBy(x => x.Denomination).Select(x => new { x.Denomination, x.Count, x.Total })
    };

    private static bool CanViewVariance(ClaimsPrincipal user, Shift shift) => user.HasClaim("permission", "shifts.view-variance") || user.HasClaim("permission", "shifts.report") || user.HasClaim("permission", "shifts.manage") || UserId(user) == shift.OpenedByUserId;
    private static async Task<bool> HasBranch(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private sealed record ShiftLedger(decimal CashSales, decimal CardSales, decimal CashRefunds, decimal CardRefunds);
    private sealed record OpenRequest(Guid BranchId, decimal OpeningCash);
    private sealed record MovementRequest(ShiftMovementType Type, decimal Amount, string? Reason, string? Note);
    private sealed record BlindCloseRequest(decimal ActualCash, decimal ActualCardTotal, List<DenominationRequest> Denominations);
    private sealed record DenominationRequest(decimal Denomination, int Count);
    private sealed record ReviewRequest(ShiftReviewStatus Status, string? Note);
}
