using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Inventory;
using OFC.Modules.Ordering;
using OFC.Modules.Payments;
using OFC.Modules.Shifts;
using OFC.Modules.Sync;

namespace OFC.Api.Features;

public static class SyncEndpoints
{
    private const string OrderType = "order.create";
    private const string MovementType = "inventory.movement.post";

    public static void MapSprintTwelveEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapPost("/sync", SyncBatch).RequireAuthorization();
        api.MapGet("/sync/state", SyncState).RequireAuthorization();
    }

    private static async Task<IResult> SyncBatch(SyncBatchRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!SyncRules.ValidBatchSize(request.Operations.Count)) return Validation("operations", $"A batch must contain between 1 and {SyncRules.BatchMax} operations.");

        // Sync must always be attributed to the device the caller actually authenticated as — a
        // device-less (e.g. back-office) session could otherwise assert any active device ID for a
        // branch it can already reach (security review finding L8), muddying which physical device a
        // synced order/movement really came from.
        var sessionDeviceId = DeviceId(user);
        if (sessionDeviceId is null) return Validation("deviceId", "A device-bound session is required to sync.");
        var effectiveDeviceId = request.DeviceId != Guid.Empty ? request.DeviceId : sessionDeviceId;
        if (sessionDeviceId != effectiveDeviceId) return Forbidden("The session device does not match the request device.");
        if (!await CanOperate(db, user, request.BranchId, ct)) return Forbidden();
        if (!await db.PosDevices.AsNoTracking().AnyAsync(x => x.Id == effectiveDeviceId && x.BranchId == request.BranchId && x.IsActive, ct)) return Forbidden("The device is not registered and active for this branch.");

        var userId = UserId(user);
        var currentCatalogVersion = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).Select(x => x.Number).FirstOrDefaultAsync(ct);
        var syncState = await db.SyncStates.FindAsync([request.BranchId], ct);
        if (syncState is null)
        {
            syncState = new SyncState { BranchId = request.BranchId, CurrentVersion = 0 };
            db.SyncStates.Add(syncState);
        }

        var seenKeys = new HashSet<Guid>();
        var results = new List<object>();

        foreach (var operation in request.Operations)
        {
            var opResult = await ApplyOperation(db, identity, user, context, request.BranchId, effectiveDeviceId.Value, userId, currentCatalogVersion, syncState, operation, seenKeys, ct);
            results.Add(opResult);
        }

        await db.SaveChangesAsync(ct);

        var pendingConflicts = await db.SyncOperations.AsNoTracking().CountAsync(x => x.BranchId == request.BranchId && x.DeviceId == effectiveDeviceId && x.Status == SyncOperationStatus.Conflict, ct);
        return Results.Ok(new
        {
            serverVersion = syncState.CurrentVersion,
            currentCatalogVersion,
            pendingConflicts,
            results
        });
    }

    private static async Task<IResult> SyncState(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct)) return Forbidden();
        var deviceId = DeviceId(user);
        var state = await db.SyncStates.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId, ct);
        var pending = await db.SyncOperations.AsNoTracking().Where(x => x.BranchId == branchId && x.DeviceId == deviceId && x.Status == SyncOperationStatus.Conflict).OrderByDescending(x => x.CreatedAt).Take(50).Select(x => new { x.Id, x.IdempotencyKey, x.OperationType, status = x.Status.ToString(), x.ConflictReason, x.Error, x.Result, x.ServerVersion, x.ClientOccurredAt }).ToListAsync(ct);
        return Results.Ok(new
        {
            serverVersion = state?.CurrentVersion ?? 0,
            currentCatalogVersion = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).Select(x => x.Number).FirstOrDefaultAsync(ct),
            lastSyncedAt = state?.UpdatedAt,
            pendingConflicts = pending.Count,
            conflicts = pending
        });
    }

    private static async Task<object> ApplyOperation(OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, Guid branchId, Guid deviceId, Guid userId, int currentCatalogVersion, SyncState syncState, SyncOperationRequest operation, HashSet<Guid> seenKeys, CancellationToken ct)
    {
        var key = operation.IdempotencyKey;
        var opType = operation.OperationType?.Trim();
        if (!SyncRules.ValidIdempotencyKey(key)) return OperationResult(key, opType, "failed", error: "The idempotency key is missing or invalid.");
        if (!SyncRules.ValidOperationType(opType)) return OperationResult(key, opType, "failed", error: "The operation type is unknown or invalid.");

        var permission = SyncRules.RequiredPermission(opType!);
        if (!string.IsNullOrEmpty(permission) && !user.HasClaim("permission", permission)) return OperationResult(key, opType, "failed", error: "You do not have permission to perform this operation.");

        if (!seenKeys.Add(key))
        {
            var duplicate = await db.SyncOperations.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.DeviceId == deviceId && x.IdempotencyKey == key, ct);
            if (duplicate is not null) return DuplicateResult(key, opType, duplicate);
        }

        var recorded = await db.SyncOperations.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.DeviceId == deviceId && x.IdempotencyKey == key, ct);
        if (recorded is not null)
        {
            if (SyncRules.IsSettled(recorded.Status)) return DuplicateResult(key, opType, recorded);
            if (recorded.Status == SyncOperationStatus.Failed) return OperationResult(key, opType, "failed", error: recorded.Error);
        }

        return opType switch
        {
            OrderType => await ApplyOrder(db, identity, user, context, branchId, deviceId, userId, currentCatalogVersion, syncState, operation, ct),
            MovementType => await ApplyMovement(db, identity, user, context, branchId, deviceId, userId, syncState, operation, ct),
            _ => OperationResult(key, opType, "failed", error: "The operation type is not supported yet.")
        };
    }

    private static async Task<object> ApplyOrder(OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, Guid branchId, Guid deviceId, Guid userId, int currentCatalogVersion, SyncState syncState, SyncOperationRequest operation, CancellationToken ct)
    {
        var key = operation.IdempotencyKey;
        OfflineOrderRequest? orderRequest;
        try { orderRequest = JsonSerializer.Deserialize<OfflineOrderRequest>(operation.Payload.GetRawText(), JsonOptions()); }
        catch { return OperationResult(key, OrderType, "failed", error: "The operation payload is invalid."); }

        if (orderRequest is null) return OperationResult(key, OrderType, "failed", error: "The operation payload is invalid.");
        if (orderRequest.SalesChannelId == Guid.Empty) return OperationResult(key, OrderType, "failed", error: "A sales channel is required.");
        if (orderRequest.Lines is not { Count: > 0 } || orderRequest.Lines.Count > 100) return OperationResult(key, OrderType, "failed", error: "An offline order must contain between 1 and 100 lines.");
        if (!await db.SalesChannels.AsNoTracking().AnyAsync(x => x.Id == orderRequest.SalesChannelId && x.IsActive, ct)) return OperationResult(key, OrderType, "conflict", conflictReason: "entity-conflict", error: "The sales channel is no longer active.");
        // An offline device may only hand back an order it could have legally reached from Draft in one
        // uninterrupted flow: held (Draft), sent for payment (Pending), cancelled, or already paid (Paid) —
        // the last of which must be backed by a real, validated payment (below), never a bare status flag.
        if (orderRequest.Status is not (OrderStatus.Draft or OrderStatus.Pending or OrderStatus.Paid or OrderStatus.Cancelled))
            return OperationResult(key, OrderType, "failed", error: "An offline order may only be created as Draft, Pending, Paid, or Cancelled.");
        if (orderRequest.Status == OrderStatus.Paid && !OrderRules.CanTransition(OrderStatus.Pending, OrderStatus.Paid))
            return OperationResult(key, OrderType, "failed", error: "The order status transition is not permitted.");
        if (orderRequest.Status is OrderStatus.Pending or OrderStatus.Cancelled && !OrderRules.CanTransition(OrderStatus.Draft, orderRequest.Status))
            return OperationResult(key, OrderType, "failed", error: "The order status transition is not permitted.");

        var existingOrder = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == branchId && x.ClientRequestId == key, ct);
        if (existingOrder is not null)
        {
            var prior = RecordDuplicate(db, branchId, deviceId, userId, key, OrderType, operation, existingOrder);
            return OperationResult(key, OrderType, "duplicate", result: OrderResponseJson(existingOrder), serverVersion: prior.ServerVersion);
        }

        if (orderRequest.Lines.Any(x => x.Quantity is < 1 or > 99)) return OperationResult(key, OrderType, "failed", error: "A line quantity must be between 1 and 99.");

        // Pricing is never trusted from an offline client: recompute it server-side from the live
        // catalog exactly like the online order path (OrderingEngine.Build), instead of taking the
        // client's Unit*/Tax* fields as-is — see security review finding H3 (offline sync order path
        // trusted client-supplied prices/totals with no server-side re-pricing).
        var productIds = orderRequest.Lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product)
            .Where(x => productIds.Contains(x.Id) && x.IsActive && x.BranchAvailability.Any(a => a.BranchId == branchId && a.IsAvailable))
            .ToDictionaryAsync(x => x.Id, x => x, ct);
        if (products.Count != productIds.Count) return OperationResult(key, OrderType, "failed", error: "One or more products on this offline order no longer exist or are unavailable at this branch.");

        var at = DateTimeOffset.UtcNow;
        var prices = await db.PriceRules.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToListAsync(ct);
        var promotions = await db.Promotions.AsNoTracking().Where(x => x.ProductId == null || productIds.Contains(x.ProductId.Value)).ToListAsync(ct);
        var taxIds = products.Values.Where(x => x.TaxCategoryId.HasValue).Select(x => x.TaxCategoryId!.Value).Distinct().ToList();
        var taxes = await db.TaxRules.AsNoTracking().Where(x => taxIds.Contains(x.TaxCategoryId)).ToListAsync(ct);
        var version = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);
        var lineInputs = orderRequest.Lines.Select(x => new OrderLineInput(x.ProductId, x.Quantity, x.Note,
            x.Selections?.Select(s => new GroupSelectionInput(s.SelectionGroupId, s.Choices.Select(c => new ChoiceInput(c.OptionId, c.Quantity)).ToList())).ToList())).ToList();
        var built = OrderingEngine.Build(branchId, orderRequest.SalesChannelId, orderRequest.Source, userId, null, deviceId, orderRequest.Note, key, at, lineInputs, products, prices, promotions, taxes, version);
        if (!built.Succeeded) return OperationResult(key, OrderType, "failed", error: built.Error!);
        var order = built.Order!;
        order.Status = orderRequest.Status;

        // The client's offline-estimated total is kept only to flag catalog drift for the operator's
        // attention, never to influence the recomputed (authoritative) order total above.
        var clientEstimatedGross = orderRequest.Lines.Sum(x => x.UnitGrossAmount * x.Quantity);
        var stalePricing = Math.Abs(order.GrossAmount - PricingRules.RoundMoney(clientEstimatedGross)) > PaymentRules.MoneyTolerance;

        Payment? payment = null;
        if (order.Status == OrderStatus.Paid)
        {
            var paymentRequest = orderRequest.Payment;
            if (paymentRequest is null) return OperationResult(key, OrderType, "failed", error: "A Paid offline order must include its payment.");
            var method = await db.PaymentMethods.AsNoTracking().SingleOrDefaultAsync(x => x.Id == paymentRequest.PaymentMethodId && x.BranchId == branchId && x.IsActive, ct);
            if (method is null) return OperationResult(key, OrderType, "failed", error: "The offline payment method is unavailable at this branch.");
            var isCash = PaymentRules.IsCash(method.Kind);
            var tendered = PaymentRules.RoundMoney(paymentRequest.TenderedAmount);
            var applied = PaymentRules.RoundMoney(paymentRequest.Amount);
            if ((isCash && tendered < applied) || (!isCash && tendered != applied) || Math.Abs(applied - order.GrossAmount) > PaymentRules.MoneyTolerance)
                return OperationResult(key, OrderType, "failed", error: "The offline payment does not cover the order total.");
            payment = new Payment { OrderId = order.Id, BranchId = branchId, PaymentMethodId = method.Id, ClientRequestId = paymentRequest.ClientRequestId, Amount = applied, TenderedAmount = tendered, ChangeAmount = isCash ? tendered - applied : 0m, Status = PaymentStatus.Captured, CreatedByUserId = userId, DeviceId = deviceId };
            payment.StatusHistory.Add(new PaymentStatusHistory { FromStatus = PaymentStatus.Pending, ToStatus = PaymentStatus.Captured, ChangedByUserId = userId, Note = isCash ? "Cash accepted offline" : "Captured offline" });
            order.StatusHistory.Add(new OrderStatusHistory { FromStatus = OrderStatus.Draft, ToStatus = OrderStatus.Pending, ChangedByUserId = userId, Note = "Synced offline" });
            order.StatusHistory.Add(new OrderStatusHistory { FromStatus = OrderStatus.Pending, ToStatus = OrderStatus.Paid, ChangedByUserId = userId, Note = "Payment captured offline" });
        }
        else
        {
            order.StatusHistory.Add(new OrderStatusHistory { FromStatus = OrderStatus.Draft, ToStatus = order.Status, ChangedByUserId = userId, Note = "Synced offline" });
        }

        db.Orders.Add(order);
        if (payment is not null)
        {
            db.Payments.Add(payment);
            var shiftId = await db.Shifts.AsNoTracking().Where(x => x.BranchId == branchId && x.Status == ShiftStatus.Open).OrderByDescending(x => x.OpenedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            db.FinancialTransactions.Add(new FinancialTransaction { OrderId = order.Id, PaymentId = payment.Id, BranchId = branchId, PaymentMethodId = payment.PaymentMethodId, DeviceId = deviceId, ShiftId = shiftId, Amount = payment.Amount, Reference = payment.Id.ToString(), CreatedByUserId = userId });
        }
        syncState.CurrentVersion += 1;
        var resultObj = new { orderId = order.Id, order.ClientRequestId, status = order.Status.ToString(), order.GrossAmount, stalePricing };
        db.SyncOperations.Add(NewSyncOperation(branchId, deviceId, userId, key, OrderType, operation, SyncOperationStatus.Applied, resultObj, syncState.CurrentVersion, stalePricing ? SyncRules.ConflictReasonStalePricing : null));
        identity.Audit(userId, branchId, deviceId, "sync.apply", "sync_operation", key.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { operationType = OrderType, status = "Applied", serverVersion = syncState.CurrentVersion, orderId = order.Id, stalePricing }));

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            var duplicate = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == branchId && x.ClientRequestId == key, ct);
            if (duplicate is not null) return OperationResult(key, OrderType, "duplicate", result: OrderResponseJson(duplicate), serverVersion: syncState.CurrentVersion);
            throw;
        }

        var flags = stalePricing ? new[] { SyncRules.ConflictReasonStalePricing } : Array.Empty<string>();
        return ResultsOkOperation(key, OrderType, "applied", flags, null, null, resultObj, syncState.CurrentVersion);
    }

    private static async Task<object> ApplyMovement(OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, Guid branchId, Guid deviceId, Guid userId, SyncState syncState, SyncOperationRequest operation, CancellationToken ct)
    {
        var key = operation.IdempotencyKey;
        OfflineMovementRequest? request;
        try { request = JsonSerializer.Deserialize<OfflineMovementRequest>(operation.Payload.GetRawText(), JsonOptions()); }
        catch { return OperationResult(key, MovementType, "failed", error: "The operation payload is invalid."); }

        if (request is null) return OperationResult(key, MovementType, "failed", error: "The operation payload is invalid.");
        if (request.BranchId != branchId) return OperationResult(key, MovementType, "conflict", conflictReason: "entity-conflict", error: "The movement belongs to a different branch.");
        if (!InventoryRules.ValidDirection(request.Type, request.Quantity)) return OperationResult(key, MovementType, "failed", error: "The movement quantity or direction is invalid for this movement type.");
        if (!InventoryRules.ValidReference(request.Reference) || !InventoryRules.ValidReason(request.Reason)) return OperationResult(key, MovementType, "failed", error: "The movement reference or reason is invalid.");

        var item = await db.InventoryItems.Include(x => x.BaseUnit).SingleOrDefaultAsync(x => x.Id == request.ItemId, ct);
        if (item is null || !item.IsActive) return OperationResult(key, MovementType, "failed", error: "The inventory item does not exist.");

        var existingMovement = await db.InventoryMovements.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.ClientMovementId == key, ct);
        if (existingMovement is not null)
        {
            var prior = RecordDuplicate(db, branchId, deviceId, userId, key, MovementType, operation, existingMovement);
            return OperationResult(key, MovementType, "duplicate", result: MovementResponseJson(existingMovement, item), serverVersion: prior.ServerVersion);
        }

        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var baseQuantity = request.Quantity;
        if (!InventoryRules.TryConvert(request.Quantity, request.UnitId, item.BaseUnitId, conversions, out var converted))
        {
            if (request.UnitId != item.BaseUnitId) return OperationResult(key, MovementType, "failed", error: "There is no conversion between the supplied unit and the item base unit.");
        }
        else
        {
            baseQuantity = converted;
        }

        var at = DateTimeOffset.UtcNow;
        var movement = new InventoryMovement
        {
            BranchId = branchId,
            InventoryItemId = item.Id,
            Type = request.Type,
            Quantity = baseQuantity,
            UnitId = item.BaseUnitId,
            Reference = request.Reference?.Trim(),
            Reason = request.Reason?.Trim(),
            CreatedByUserId = userId,
            DeviceId = deviceId,
            ClientMovementId = key,
            OrderId = request.OrderId,
            OccurredAt = operation.OccurredAt ?? at
        };
        db.InventoryMovements.Add(movement);
        // The response's "balance"/"negative" flag is a ledger snapshot for display, distinct from the
        // persisted cache (see InventoryStock.ApplyDelta below, applied atomically after SaveChanges to
        // avoid the read-then-write race two concurrent syncs on the same item used to hit).
        var balance = InventoryRules.RoundQuantity((await db.InventoryMovements.Where(x => x.BranchId == branchId && x.InventoryItemId == item.Id).SumAsync(x => (decimal?)x.Quantity, ct) ?? 0m) + movement.Quantity);
        var negative = balance < 0m;

        syncState.CurrentVersion += 1;
        var resultObj = new { movementId = movement.Id, movement.ClientMovementId, type = movement.Type.ToString(), movement.Quantity, balance, negative };
        db.SyncOperations.Add(NewSyncOperation(branchId, deviceId, userId, key, MovementType, operation, SyncOperationStatus.Applied, resultObj, syncState.CurrentVersion, negative ? SyncRules.ConflictReasonNegativeStock : null));
        identity.Audit(userId, branchId, deviceId, "sync.apply", "sync_operation", key.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { operationType = MovementType, status = "Applied", serverVersion = syncState.CurrentVersion, movementId = movement.Id, negative }));

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            var duplicate = await db.InventoryMovements.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == branchId && x.ClientMovementId == key, ct);
            if (duplicate is not null) return OperationResult(key, MovementType, "duplicate", result: MovementResponseJson(duplicate, item), serverVersion: syncState.CurrentVersion);
            throw;
        }
        await InventoryStock.ApplyDelta(db, item.Id, movement.Quantity, ct);

        var flags = negative ? new[] { SyncRules.ConflictReasonNegativeStock } : Array.Empty<string>();
        return ResultsOkOperation(key, MovementType, "applied", flags, null, null, resultObj, syncState.CurrentVersion);
    }

    private static SyncOperation NewSyncOperation(Guid branchId, Guid deviceId, Guid userId, Guid key, string opType, SyncOperationRequest operation, SyncOperationStatus status, object result, long serverVersion, string? conflictReason) => new()
    {
        BranchId = branchId,
        DeviceId = deviceId,
        CreatedByUserId = userId,
        IdempotencyKey = key,
        OperationType = opType,
        Payload = operation.Payload.GetRawText(),
        BaseVersion = operation.BaseVersion,
        BaseCatalogVersion = operation.BaseCatalogVersion,
        Status = status,
        Result = JsonSerializer.Serialize(result, JsonOptions()),
        ConflictReason = conflictReason,
        ServerVersion = serverVersion,
        ClientOccurredAt = operation.OccurredAt,
        AppliedAt = DateTimeOffset.UtcNow
    };

    private static SyncOperation RecordDuplicate(OFCDbContext db, Guid branchId, Guid deviceId, Guid userId, Guid key, string opType, SyncOperationRequest operation, object existing)
    {
        var recorded = new SyncOperation
        {
            BranchId = branchId, DeviceId = deviceId, CreatedByUserId = userId, IdempotencyKey = key,
            OperationType = opType, Payload = operation.Payload.GetRawText(), BaseVersion = operation.BaseVersion,
            BaseCatalogVersion = operation.BaseCatalogVersion, Status = SyncOperationStatus.Duplicate,
            Result = JsonSerializer.Serialize(existing, JsonOptions()), ServerVersion = null,
            ClientOccurredAt = operation.OccurredAt, AppliedAt = DateTimeOffset.UtcNow
        };
        db.SyncOperations.Add(recorded);
        return recorded;
    }

    private static object DuplicateResult(Guid key, string? opType, SyncOperation recorded)
    {
        JsonElement result = default;
        if (recorded.Result is not null) result = JsonSerializer.Deserialize<JsonElement>(recorded.Result, JsonOptions());
        return OperationResult(key, opType, "duplicate", result: result, serverVersion: recorded.ServerVersion, conflictReason: recorded.ConflictReason);
    }

    private static object ResultsOkOperation(Guid key, string? opType, string status, string[]? flags, string? conflictReason, string? error, object result, long? serverVersion) =>
        OperationResult(key, opType, status, flags, conflictReason, error, result, serverVersion);

    private static object OperationResult(Guid key, string? opType, string status, string[]? flags = null, string? conflictReason = null, string? error = null, object? result = null, long? serverVersion = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key,
            ["operationType"] = opType,
            ["status"] = status,
            ["flags"] = flags ?? Array.Empty<string>(),
            ["conflictReason"] = conflictReason,
            ["error"] = error,
            ["serverVersion"] = serverVersion
        };
        if (result is not null) payload["result"] = result;
        return payload;
    }

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct) => user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private static object OrderResponseJson(Order order) => new { order.Id, order.BranchId, order.SalesChannelId, order.ClientRequestId, source = order.Source.ToString(), status = order.Status.ToString(), order.Note, order.NetAmount, order.TaxAmount, order.GrossAmount, order.CreatedAt, lines = order.Lines.Select(x => new { x.Id, x.ProductId, x.ProductNameAr, x.ProductNameEn, x.Quantity, x.Note, x.SelectionsSnapshot, x.UnitGrossAmount, x.UnitNetAmount, x.UnitTaxAmount, x.UnitDiscountAmount, x.CatalogVersionNumber }) };

    private static object MovementResponseJson(InventoryMovement movement, InventoryItem? item) => new { movement.Id, movement.BranchId, movement.InventoryItemId, itemNameAr = item?.NameAr, itemNameEn = item?.NameEn, type = movement.Type.ToString(), movement.Quantity, movement.UnitId, movement.Reference, movement.Reason, movement.ClientMovementId, movement.OccurredAt };

    private sealed record SyncBatchRequest(Guid BranchId, Guid DeviceId, long LastSyncVersion, int? BaseCatalogVersion, List<SyncOperationRequest> Operations);
    private sealed record SyncOperationRequest(Guid IdempotencyKey, string OperationType, long? BaseVersion, int? BaseCatalogVersion, DateTimeOffset? OccurredAt, JsonElement Payload);
    private sealed record OfflineOrderRequest(Guid SalesChannelId, OrderSource Source, OrderStatus Status, string? Note, List<OfflineOrderLineRequest> Lines, OfflineOrderPaymentRequest? Payment);
    private sealed record OfflineOrderPaymentRequest(Guid ClientRequestId, Guid PaymentMethodId, decimal Amount, decimal TenderedAmount);
    // Unit*/Tax*/PriceSource/PriceRuleId/PromotionId/TaxRuleId/CatalogVersion* are accepted for backward
    // compatibility with already-queued client payloads but are never used to build the order — pricing
    // is always recomputed server-side from Selections (see ApplyOrder / OrderingEngine.Build).
    private sealed record OfflineOrderLineRequest(Guid ProductId, string? ProductNameAr, string? ProductNameEn, int Quantity, string? Note, string? SelectionsSnapshot, List<OfflineGroupSelection>? Selections, decimal UnitListAmount, decimal UnitDiscountAmount, decimal UnitNetAmount, decimal UnitTaxAmount, decimal UnitGrossAmount, decimal TaxRate, TaxCalculationMode TaxCalculationMode, string? PriceSource, Guid? PriceRuleId, Guid? PromotionId, Guid? TaxRuleId, Guid? CatalogVersionId, int? CatalogVersionNumber);
    private sealed record OfflineGroupSelection(Guid SelectionGroupId, List<OfflineChoice> Choices);
    private sealed record OfflineChoice(Guid OptionId, int Quantity);
    private sealed record OfflineMovementRequest(Guid BranchId, Guid ItemId, Guid UnitId, InventoryMovementType Type, decimal Quantity, string? Reference, string? Reason, Guid? OrderId);
}
