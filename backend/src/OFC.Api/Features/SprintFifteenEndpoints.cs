using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Inventory;
using OFC.Modules.Ordering;
using OFC.Modules.Shifts;

namespace OFC.Api.Features;

public static class SprintFifteenEndpoints
{
    public static void MapSprintFifteenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/inventory");
        api.MapGet("/counts", ListCounts).RequireAuthorization();
        api.MapGet("/counts/{id:guid}", GetCount).RequireAuthorization();
        api.MapPost("/counts", CreateCount).RequireAuthorization();
        api.MapPost("/counts/{id:guid}/approve", ApproveCount).RequireAuthorization();
        api.MapPost("/counts/{id:guid}/post", PostCount).RequireAuthorization();
        api.MapPost("/counts/{id:guid}/cancel", CancelCount).RequireAuthorization();
        api.MapGet("/transfers", ListTransfers).RequireAuthorization();
        api.MapGet("/transfers/{id:guid}", GetTransfer).RequireAuthorization();
        api.MapPost("/transfers", CreateTransfer).RequireAuthorization();
        api.MapPost("/transfers/{id:guid}/ship", ShipTransfer).RequireAuthorization();
        api.MapPost("/transfers/{id:guid}/receive", ReceiveTransfer).RequireAuthorization();
        api.MapPost("/transfers/{id:guid}/cancel", CancelTransfer).RequireAuthorization();
        api.MapGet("/waste", ListWaste).RequireAuthorization();
        api.MapGet("/waste/categories", WasteCategories).RequireAuthorization();
        api.MapGet("/waste/{id:guid}", GetWaste).RequireAuthorization();
        api.MapPost("/waste", CreateWaste).RequireAuthorization();
        api.MapGet("/costing/value", InventoryValuation).RequireAuthorization();
        api.MapGet("/costing/recipes/{id:guid}", RecipeCost).RequireAuthorization();
        api.MapGet("/costing/summary", CostingSummary).RequireAuthorization();
    }

    private static async Task<IResult> ListCounts(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "inventory.view")) return Forbidden();
        var counts = await db.InventoryCounts.AsNoTracking().Include(x => x.Lines).Where(x => x.BranchId == branchId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var itemIds = counts.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(counts.Select(x => CountRow(x, items)));
    }

    private static async Task<IResult> GetCount(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        var count = await db.InventoryCounts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (count is null) return Results.NotFound();
        if (!await CanOperate(db, user, count.BranchId, ct, "inventory.view")) return Forbidden();
        var items = await db.InventoryItems.AsNoTracking().Include(x => x.BaseUnit).Where(x => count.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(CountDetail(count, items));
    }

    private static async Task<IResult> CreateCount(CreateCountRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct, "inventory.counts.manage")) return Forbidden();
        if (!InventoryRules.ValidNote(request.Note)) return Validation("count", "The count note is invalid.");
        if (request.Lines is not { Count: > 0 } || !InventoryRules.ValidDocumentLineCount(request.Lines.Count)) return Validation("lines", "A count requires between 1 and 500 lines.");
        var existing = await db.InventoryCounts.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.ClientCountId == request.ClientCountId, ct);
        if (existing is not null) return Results.Ok(CountRow(existing, await LoadCountItems(db, existing, ct)));
        var itemIds = request.Lines.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, x => x, ct);
        if (items.Count != itemIds.Count) return Validation("lines", "One or more count lines reference an unknown or inactive inventory item.");
        if (request.Lines.Any(x => !InventoryRules.ValidCountedQuantity(x.CountedQuantity))) return Validation("lines", "A counted quantity must be between 0 and 9,999,999.");
        var count = new InventoryCount { Number = await NextCountNumber(db, request.BranchId, ct), BranchId = request.BranchId, Status = InventoryCountStatus.Draft, Note = request.Note?.Trim(), CreatedByUserId = UserId(user), ClientCountId = request.ClientCountId };
        foreach (var line in request.Lines)
        {
            var item = items[line.InventoryItemId];
            var system = await SystemBalance(db, request.BranchId, item.Id, ct);
            var counted = InventoryRules.RoundQuantity(line.CountedQuantity);
            count.Lines.Add(new InventoryCountLine { InventoryItemId = item.Id, UnitId = item.BaseUnitId, SystemQuantity = system, CountedQuantity = counted, Variance = InventoryRules.CountVariance(system, counted), Reason = line.Reason?.Trim() });
        }
        db.InventoryCounts.Add(count);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "inventory.count.create", "inventory_count", count.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { count.Number, lineCount = count.Lines.Count, count.Status }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/inventory/counts/{count.Id}", CountDetail(count, items));
    }

    private static async Task<IResult> ApproveCount(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var count = await LoadCount(db, id, ct);
        if (count is null) return Results.NotFound();
        if (!await CanOperate(db, user, count.BranchId, ct, "inventory.counts.manage")) return Forbidden();
        if (!InventoryRules.CanApproveCount(count.Status)) return Validation("status", "Only a draft count can be approved.");
        if (count.Lines.Any(l => InventoryRules.IsSignificantVariance(l.Variance) && string.IsNullOrWhiteSpace(l.Reason))) return Validation("reason", "Every line with a count variance must record a reason before approval.");
        count.ApprovedByUserId = UserId(user); count.ApprovedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), count.BranchId, DeviceId(user), "inventory.count.approve", "inventory_count", count.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { count.Number, count.ApprovedByUserId, adjustedLines = count.Lines.Count(l => InventoryRules.IsSignificantVariance(l.Variance)) }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(CountDetail(count, await LoadCountItems(db, count, ct)));
    }

    private static async Task<IResult> PostCount(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var count = await LoadCount(db, id, ct);
        if (count is null) return Results.NotFound();
        if (!await CanOperate(db, user, count.BranchId, ct, "inventory.counts.manage")) return Forbidden();
        if (!InventoryRules.CanPostCount(count.Status)) return Validation("status", "Only a draft count can be posted.");
        if (!count.ApprovedAt.HasValue) return Validation("status", "A count must be approved before it is posted.");
        if (count.Lines.Any(l => InventoryRules.IsSignificantVariance(l.Variance) && string.IsNullOrWhiteSpace(l.Reason))) return Validation("reason", "Every line with a count variance must record a reason before it is posted.");
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var items = await db.InventoryItems.Include(x => x.BaseUnit).Where(x => count.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var now = DateTimeOffset.UtcNow;
        var adjusted = 0;
        foreach (var line in count.Lines.Where(l => InventoryRules.IsSignificantVariance(l.Variance)))
        {
            if (!items.TryGetValue(line.InventoryItemId, out var item)) return Validation("lines", $"Item {line.InventoryItemId} is not a known inventory item.");
            if (!InventoryRules.TryConvert(line.Variance, line.UnitId, item.BaseUnitId, conversions, out var baseVariance))
            {
                if (line.UnitId != item.BaseUnitId) return Validation("lines", $"There is no conversion path for item {item.NameAr}.");
                baseVariance = InventoryRules.RoundQuantity(line.Variance);
            }
            var movement = new InventoryMovement { BranchId = count.BranchId, InventoryItemId = item.Id, Type = InventoryMovementType.CountAdjustment, Quantity = baseVariance, UnitId = item.BaseUnitId, Reference = count.Number, Reason = line.Reason, CreatedByUserId = UserId(user), DeviceId = DeviceId(user), OccurredAt = now };
            db.InventoryMovements.Add(movement);
            item.StockOnHand = InventoryRules.RoundQuantity(await SystemBalance(db, count.BranchId, item.Id, ct) + baseVariance);
            adjusted++;
        }
        count.Status = InventoryCountStatus.Posted; count.PostedByUserId = UserId(user); count.PostedAt = now;
        identity.Audit(UserId(user), count.BranchId, DeviceId(user), "inventory.count.post", "inventory_count", count.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { count.Number, movementCount = adjusted, count.Status }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(CountDetail(count, await LoadCountItems(db, count, ct)));
    }

    private static async Task<IResult> CancelCount(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var count = await LoadCount(db, id, ct);
        if (count is null) return Results.NotFound();
        if (!await CanOperate(db, user, count.BranchId, ct, "inventory.counts.manage")) return Forbidden();
        if (!InventoryRules.CanCancelCount(count.Status)) return Validation("status", "Only a draft count can be cancelled.");
        count.Status = InventoryCountStatus.Cancelled;
        identity.Audit(UserId(user), count.BranchId, DeviceId(user), "inventory.count.cancel", "inventory_count", count.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { count.Number, count.Status }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(CountDetail(count, await LoadCountItems(db, count, ct)));
    }

    private static async Task<IResult> ListTransfers(Guid branchId, string? status, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "inventory.view")) return Forbidden();
        IQueryable<StockTransfer> query = db.StockTransfers.AsNoTracking().Include(x => x.Lines).Where(x => x.SourceBranchId == branchId || x.DestinationBranchId == branchId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<StockTransferStatus>(status, ignoreCase: true, out var parsed)) return Validation("status", "The transfer status is invalid.");
            query = query.Where(x => x.Status == parsed);
        }
        var transfers = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var itemIds = transfers.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(transfers.Select(x => TransferRow(x, items)));
    }

    private static async Task<IResult> GetTransfer(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        var transfer = await db.StockTransfers.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (transfer is null) return Results.NotFound();
        if (!await CanOperate(db, user, transfer.SourceBranchId, ct, "inventory.view") && !await CanOperate(db, user, transfer.DestinationBranchId, ct, "inventory.view")) return Forbidden();
        var items = await db.InventoryItems.AsNoTracking().Include(x => x.BaseUnit).Where(x => transfer.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(TransferDetail(transfer, items));
    }

    private static async Task<IResult> CreateTransfer(CreateTransferRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.SourceBranchId, ct, "inventory.transfers.manage")) return Forbidden();
        if (request.SourceBranchId == request.DestinationBranchId) return Validation("destinationBranchId", "The source and destination branches must differ.");
        if (!await db.Branches.AsNoTracking().AnyAsync(x => x.Id == request.DestinationBranchId && x.IsActive, ct)) return Validation("destinationBranchId", "The destination branch does not exist.");
        if (!InventoryRules.ValidNote(request.Note) || !InventoryRules.ValidReference(request.Reference)) return Validation("transfer", "The transfer note or reference is invalid.");
        if (request.Lines is not { Count: > 0 } || !InventoryRules.ValidDocumentLineCount(request.Lines.Count)) return Validation("lines", "A transfer requires between 1 and 500 lines.");
        var existing = await db.StockTransfers.AsNoTracking().SingleOrDefaultAsync(x => x.SourceBranchId == request.SourceBranchId && x.ClientTransferId == request.ClientTransferId, ct);
        if (existing is not null) return Results.Ok(TransferRow(existing, await LoadTransferItems(db, existing, ct)));
        var itemIds = request.Lines.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, x => x, ct);
        if (items.Count != itemIds.Count) return Validation("lines", "One or more transfer lines reference an unknown or inactive inventory item.");
        var transfer = new StockTransfer { Number = await NextTransferNumber(db, request.SourceBranchId, ct), SourceBranchId = request.SourceBranchId, DestinationBranchId = request.DestinationBranchId, Status = StockTransferStatus.Draft, Note = request.Note?.Trim(), Reference = request.Reference?.Trim(), CreatedByUserId = UserId(user), ClientTransferId = request.ClientTransferId };
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0m || line.Quantity > 9999999m) return Validation("lines", "A transfer quantity must be between 0 and 9,999,999.");
            var item = items[line.InventoryItemId];
            transfer.Lines.Add(new StockTransferLine { InventoryItemId = item.Id, UnitId = item.BaseUnitId, Quantity = InventoryRules.RoundQuantity(line.Quantity), UnitCost = item.UnitCost });
        }
        db.StockTransfers.Add(transfer);
        identity.Audit(UserId(user), request.SourceBranchId, DeviceId(user), "inventory.transfer.create", "stock_transfer", transfer.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { transfer.Number, transfer.SourceBranchId, transfer.DestinationBranchId, lineCount = transfer.Lines.Count, transfer.Status }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/inventory/transfers/{transfer.Id}", TransferDetail(transfer, items));
    }

    private static async Task<IResult> ShipTransfer(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var transfer = await LoadTransfer(db, id, ct);
        if (transfer is null) return Results.NotFound();
        if (!await CanOperate(db, user, transfer.SourceBranchId, ct, "inventory.transfers.manage")) return Forbidden();
        if (!InventoryRules.CanShipTransfer(transfer.Status)) return Validation("status", "Only a draft transfer can be shipped.");
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var items = await db.InventoryItems.Include(x => x.BaseUnit).Where(x => transfer.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var now = DateTimeOffset.UtcNow;
        var posted = 0;
        foreach (var line in transfer.Lines)
        {
            if (!items.TryGetValue(line.InventoryItemId, out var item)) return Validation("lines", $"Item {line.InventoryItemId} is not a known inventory item.");
            var baseQty = line.Quantity;
            if (!InventoryRules.TryConvert(line.Quantity, line.UnitId, item.BaseUnitId, conversions, out var converted))
            {
                if (line.UnitId != item.BaseUnitId) return Validation("lines", $"There is no conversion path for item {item.NameAr}.");
            } else { baseQty = InventoryRules.RoundQuantity(converted); }
            var balance = await SystemBalance(db, transfer.SourceBranchId, item.Id, ct);
            if (InventoryRules.RoundQuantity(balance - baseQty) < 0m) return Validation("lines", $"Insufficient stock at the source branch for item {item.NameAr}.");
            var movement = new InventoryMovement { BranchId = transfer.SourceBranchId, InventoryItemId = item.Id, Type = InventoryMovementType.TransferOut, Quantity = InventoryRules.RoundQuantity(-baseQty), UnitId = item.BaseUnitId, Reference = transfer.Number, Reason = "stock-transfer", CreatedByUserId = UserId(user), DeviceId = DeviceId(user), OccurredAt = now };
            db.InventoryMovements.Add(movement);
            item.StockOnHand = InventoryRules.RoundQuantity(balance - baseQty);
            posted++;
        }
        transfer.Status = StockTransferStatus.InTransit; transfer.ShippedAt = now;
        identity.Audit(UserId(user), transfer.SourceBranchId, DeviceId(user), "inventory.transfer.ship", "stock_transfer", transfer.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { transfer.Number, movementCount = posted, transfer.Status, transfer.DestinationBranchId }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(TransferDetail(transfer, await LoadTransferItems(db, transfer, ct)));
    }

    private static async Task<IResult> ReceiveTransfer(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var transfer = await LoadTransfer(db, id, ct);
        if (transfer is null) return Results.NotFound();
        if (!await CanOperate(db, user, transfer.DestinationBranchId, ct, "inventory.transfers.manage")) return Forbidden();
        if (!InventoryRules.CanReceiveTransfer(transfer.Status)) return Validation("status", "Only an in-transit transfer can be received.");
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var items = await db.InventoryItems.Include(x => x.BaseUnit).Where(x => transfer.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var now = DateTimeOffset.UtcNow;
        var posted = 0;
        foreach (var line in transfer.Lines)
        {
            if (!items.TryGetValue(line.InventoryItemId, out var item)) return Validation("lines", $"Item {line.InventoryItemId} is not a known inventory item.");
            var baseQty = line.Quantity;
            if (!InventoryRules.TryConvert(line.Quantity, line.UnitId, item.BaseUnitId, conversions, out var converted))
            {
                if (line.UnitId != item.BaseUnitId) return Validation("lines", $"There is no conversion path for item {item.NameAr}.");
            } else { baseQty = InventoryRules.RoundQuantity(converted); }
            var balance = await SystemBalance(db, transfer.DestinationBranchId, item.Id, ct);
            var movement = new InventoryMovement { BranchId = transfer.DestinationBranchId, InventoryItemId = item.Id, Type = InventoryMovementType.TransferIn, Quantity = InventoryRules.RoundQuantity(baseQty), UnitId = item.BaseUnitId, Reference = transfer.Number, Reason = "stock-transfer-receipt", CreatedByUserId = UserId(user), DeviceId = DeviceId(user), OccurredAt = now };
            db.InventoryMovements.Add(movement);
            item.StockOnHand = InventoryRules.RoundQuantity(balance + baseQty);
            posted++;
        }
        transfer.Status = StockTransferStatus.Received; transfer.ReceivedByUserId = UserId(user); transfer.ReceivedAt = now;
        identity.Audit(UserId(user), transfer.DestinationBranchId, DeviceId(user), "inventory.transfer.receive", "stock_transfer", transfer.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { transfer.Number, movementCount = posted, transfer.Status, transfer.SourceBranchId }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(TransferDetail(transfer, await LoadTransferItems(db, transfer, ct)));
    }

    private static async Task<IResult> CancelTransfer(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var transfer = await LoadTransfer(db, id, ct);
        if (transfer is null) return Results.NotFound();
        if (!await CanOperate(db, user, transfer.SourceBranchId, ct, "inventory.transfers.manage")) return Forbidden();
        if (!InventoryRules.CanCancelTransfer(transfer.Status)) return Validation("status", "A received transfer cannot be cancelled.");
        transfer.Status = StockTransferStatus.Cancelled;
        identity.Audit(UserId(user), transfer.SourceBranchId, DeviceId(user), "inventory.transfer.cancel", "stock_transfer", transfer.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { transfer.Number, transfer.Status }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(TransferDetail(transfer, await LoadTransferItems(db, transfer, ct)));
    }

    private static async Task<IResult> ListWaste(Guid branchId, WasteCategory? category, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "inventory.view")) return Forbidden();
        IQueryable<WasteRecord> query = db.WasteRecords.AsNoTracking().Where(x => x.BranchId == branchId);
        if (category.HasValue) query = query.Where(x => x.Category == category.Value);
        var records = await query.OrderByDescending(x => x.OccurredAt).Take(200).ToListAsync(ct);
        var itemIds = records.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(records.Select(x => WasteRow(x, items.TryGetValue(x.InventoryItemId, out var i) ? i : null)));
    }

    private static async Task<IResult> GetWaste(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.view")) return Forbidden();
        var record = await db.WasteRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (record is null) return Results.NotFound();
        if (!await CanOperate(db, user, record.BranchId, ct, "inventory.view")) return Forbidden();
        var item = await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == record.InventoryItemId, ct);
        return Results.Ok(WasteDetail(record, item));
    }

    private static IResult WasteCategories() => Results.Ok(new[] { new { code = "Expired", nameAr = "منتهي الصلاحية", nameEn = "Expired" }, new { code = "Damaged", nameAr = "تالف", nameEn = "Damaged" }, new { code = "PreparationWaste", nameAr = "هدر التحضير", nameEn = "Preparation waste" }, new { code = "FinishedProductWaste", nameAr = "هدر المنتج النهائي", nameEn = "Finished product waste" }, new { code = "CancelledOrderWaste", nameAr = "هدر طلب ملغي", nameEn = "Cancelled order waste" } });

    private static async Task<IResult> CreateWaste(CreateWasteRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct, "inventory.waste.manage")) return Forbidden();
        if (!InventoryRules.ValidWasteQuantity(request.Quantity)) return Validation("quantity", "A waste quantity must be between 0.000001 and 9,999.");
        if (!InventoryRules.ValidReason(request.Reason) || !InventoryRules.ValidNote(request.Note) || !InventoryRules.ValidPhotoUrl(request.PhotoUrl) || !InventoryRules.ValidReference(request.Reference)) return Validation("waste", "The waste reason, note, reference, or photo is invalid.");
        var existing = await db.WasteRecords.AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == request.BranchId && x.ClientRecordId == request.ClientRecordId, ct);
        if (existing is not null) return Results.Ok(WasteDetail(existing, await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(x => x.Id == existing.InventoryItemId, ct)));
        if (request.OrderId.HasValue && !await db.Orders.AsNoTracking().AnyAsync(x => x.Id == request.OrderId, ct)) return Validation("orderId", "The referenced order does not exist.");
        var item = await db.InventoryItems.Include(x => x.BaseUnit).SingleOrDefaultAsync(x => x.Id == request.InventoryItemId && x.IsActive, ct);
        if (item is null) return Validation("inventoryItemId", "The inventory item does not exist.");
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var baseQty = request.Quantity;
        if (!InventoryRules.TryConvert(request.Quantity, request.UnitId, item.BaseUnitId, conversions, out var converted))
        {
            if (request.UnitId != item.BaseUnitId) return Validation("unit", "There is no conversion between the supplied unit and the item base unit.");
        } else { baseQty = InventoryRules.RoundQuantity(converted); }
        var now = request.OccurredAt ?? DateTimeOffset.UtcNow;
        var number = await NextWasteNumber(db, request.BranchId, ct);
        var movement = new InventoryMovement { BranchId = request.BranchId, InventoryItemId = item.Id, Type = InventoryMovementType.Waste, Quantity = InventoryRules.RoundQuantity(-baseQty), UnitId = item.BaseUnitId, Reference = number, Reason = request.Reason, OrderId = request.OrderId, ShiftId = request.ShiftId, CreatedByUserId = UserId(user), DeviceId = DeviceId(user), OccurredAt = now };
        db.InventoryMovements.Add(movement);
        item.StockOnHand = InventoryRules.RoundQuantity(await SystemBalance(db, request.BranchId, item.Id, ct) - baseQty);
        var record = new WasteRecord { Number = number, BranchId = request.BranchId, InventoryItemId = item.Id, Category = request.Category, Quantity = InventoryRules.RoundQuantity(baseQty), UnitId = item.BaseUnitId, Reason = request.Reason?.Trim(), Note = request.Note?.Trim(), PhotoUrl = request.PhotoUrl?.Trim(), Reference = request.Reference?.Trim(), OrderId = request.OrderId, OrderLineId = request.OrderLineId, ShiftId = request.ShiftId, InventoryMovementId = movement.Id, CreatedByUserId = UserId(user), DeviceId = DeviceId(user), ClientRecordId = request.ClientRecordId, OccurredAt = now };
        db.WasteRecords.Add(record);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "inventory.waste.create", "waste_record", record.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { record.Number, record.Category, record.Quantity, record.InventoryItemId, orderId = record.OrderId }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/inventory/waste/{record.Id}", WasteDetail(record, item));
    }

    private static async Task<IResult> InventoryValuation(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "inventory.costing.view")) return Forbidden();
        var movements = await db.InventoryMovements.AsNoTracking().Where(x => x.BranchId == branchId).ToListAsync(ct);
        var itemIds = movements.Select(x => x.InventoryItemId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).Include(x => x.BaseUnit).ToListAsync(ct);
        var rows = items.Select(item => new { Item = item, Balance = movements.Where(x => x.InventoryItemId == item.Id).Sum(x => x.Quantity) }).Where(x => InventoryRules.RoundQuantity(x.Balance) != 0m).OrderByDescending(x => x.Balance).ToList();
        var totalValue = InventoryRules.RoundMoney(rows.Sum(x => x.Balance * x.Item.UnitCost));
        return Results.Ok(new { branchId, totalValue, lineCount = rows.Count, rows = rows.Select(x => new { x.Item.Id, x.Item.Sku, itemNameAr = x.Item.NameAr, itemNameEn = x.Item.NameEn, baseUnitId = x.Item.BaseUnitId, baseUnitCode = x.Item.BaseUnit?.Code, balance = InventoryRules.RoundQuantity(x.Balance), unitCost = x.Item.UnitCost, value = InventoryRules.RoundMoney(x.Balance * x.Item.UnitCost) }) });
    }

    private static async Task<IResult> RecipeCost(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "inventory.costing.view")) return Forbidden();
        var recipe = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (recipe is null) return Results.NotFound();
        var items = await db.InventoryItems.AsNoTracking().Where(x => recipe.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).Include(x => x.BaseUnit).ToDictionaryAsync(x => x.Id, x => x, ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(x => recipe.Lines.Select(l => l.UnitId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var reproducible = InventoryRules.TryComputeRecipeCost(recipe.Lines.ToList(), items, conversions, out var recipeCost);
        return Results.Ok(new { recipe.Id, recipe.VersionNumber, recipe.ProductId, recipeCost, reproducible, lines = recipe.Lines.Select(x => new { x.Id, x.InventoryItemId, itemNameAr = items.TryGetValue(x.InventoryItemId, out var item) ? item.NameAr : null, itemNameEn = items.TryGetValue(x.InventoryItemId, out var item2) ? item2.NameEn : null, x.Quantity, x.UnitId, unitCode = units.TryGetValue(x.UnitId, out var unit) ? unit.Code : null, unitCost = items.TryGetValue(x.InventoryItemId, out var it) ? it.UnitCost : 0m }) });
    }

    private static async Task<IResult> CostingSummary(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "inventory.costing.view")) return Forbidden();
        var recipes = await db.RecipeVersions.AsNoTracking().Include(x => x.Lines).Where(x => x.Status == RecipeStatus.Active).ToListAsync(ct);
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var itemIds = recipes.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var productIds = recipes.Select(x => x.ProductId).Distinct().ToList();
        var products = productIds.Count == 0 ? new Dictionary<Guid, Product>() : await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var now = DateTimeOffset.UtcNow;
        var rows = new List<object>();
        foreach (var group in recipes.GroupBy(x => x.ProductId))
        {
            var recipe = group.OrderByDescending(x => x.VersionNumber).First();
            var product = products.TryGetValue(recipe.ProductId, out var p) ? p : null;
            if (product is null) continue;
            var reproducible = InventoryRules.TryComputeRecipeCost(recipe.Lines.ToList(), items, conversions, out var recipeCost);
            var price = await CurrentSellingPrice(db, product.Id, branchId, now, ct);
            var sellingPrice = price ?? product.BasePrice ?? 0m;
            rows.Add(new { recipe.Id, recipe.VersionNumber, productId = product.Id, productSku = product.Sku, productNameAr = product.NameAr, productNameEn = product.NameEn, recipeCost, reproducible, sellingPrice = InventoryRules.RoundMoney(sellingPrice), foodCostPercent = InventoryRules.FoodCostPercent(recipeCost, sellingPrice), grossMargin = InventoryRules.GrossMargin(recipeCost, sellingPrice), grossMarginPercent = InventoryRules.GrossMarginPercent(recipeCost, sellingPrice) });
        }
        return Results.Ok(new { branchId, lineCount = rows.Count, rows });
    }

    private static async Task<InventoryCount?> LoadCount(OFCDbContext db, Guid id, CancellationToken ct) => await db.InventoryCounts.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
    private static async Task<StockTransfer?> LoadTransfer(OFCDbContext db, Guid id, CancellationToken ct) => await db.StockTransfers.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
    private static async Task<Dictionary<Guid, InventoryItem>> LoadCountItems(OFCDbContext db, InventoryCount count, CancellationToken ct) => await db.InventoryItems.AsNoTracking().Include(x => x.BaseUnit).Where(x => count.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
    private static async Task<Dictionary<Guid, InventoryItem>> LoadTransferItems(OFCDbContext db, StockTransfer transfer, CancellationToken ct) => await db.InventoryItems.AsNoTracking().Include(x => x.BaseUnit).Where(x => transfer.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
    private static async Task<decimal> SystemBalance(OFCDbContext db, Guid branchId, Guid itemId, CancellationToken ct) => InventoryRules.RoundQuantity(await db.InventoryMovements.Where(x => x.BranchId == branchId && x.InventoryItemId == itemId).SumAsync(x => (decimal?)x.Quantity, ct).ConfigureAwait(false) ?? 0m);
    private static async Task<decimal?> CurrentSellingPrice(OFCDbContext db, Guid productId, Guid branchId, DateTimeOffset now, CancellationToken ct)
    {
        var rules = await db.PriceRules.AsNoTracking().Where(x => x.ProductId == productId && x.IsActive && x.EffectiveFrom <= now && (!x.EffectiveTo.HasValue || x.EffectiveTo > now) && (x.BranchId == branchId || x.BranchId == null)).OrderByDescending(x => x.EffectiveFrom).ToListAsync(ct);
        var branchSpecific = rules.FirstOrDefault(x => x.BranchId == branchId);
        var generic = rules.FirstOrDefault(x => x.BranchId == null);
        return (branchSpecific ?? generic)?.Price;
    }

    private static async Task<string> NextCountNumber(OFCDbContext db, Guid branchId, CancellationToken ct) => $"CNT-{DateTimeOffset.UtcNow:yyyyMMdd}-{await db.InventoryCounts.CountAsync(x => x.BranchId == branchId, ct) + 1:D4}";
    private static async Task<string> NextTransferNumber(OFCDbContext db, Guid branchId, CancellationToken ct) => $"TRF-{DateTimeOffset.UtcNow:yyyyMMdd}-{await db.StockTransfers.CountAsync(x => x.SourceBranchId == branchId, ct) + 1:D4}";
    private static async Task<string> NextWasteNumber(OFCDbContext db, Guid branchId, CancellationToken ct) => $"WST-{DateTimeOffset.UtcNow:yyyyMMdd}-{await db.WasteRecords.CountAsync(x => x.BranchId == branchId, ct) + 1:D4}";

    private static object CountRow(InventoryCount count, Dictionary<Guid, InventoryItem> items) => new { count.Id, count.Number, count.BranchId, status = count.Status.ToString(), count.CreatedByUserId, count.ApprovedByUserId, count.ApprovedAt, count.PostedByUserId, count.PostedAt, count.CreatedAt, lineCount = count.Lines.Count, varianceLineCount = count.Lines.Count(l => InventoryRules.IsSignificantVariance(l.Variance)) };
    private static object CountDetail(InventoryCount count, Dictionary<Guid, InventoryItem> items) => new { count.Id, count.Number, count.BranchId, status = count.Status.ToString(), count.Note, count.CreatedByUserId, count.ApprovedByUserId, count.ApprovedAt, count.PostedByUserId, count.PostedAt, count.CreatedAt, lines = count.Lines.Select(x => new { x.Id, x.InventoryItemId, itemNameAr = items.TryGetValue(x.InventoryItemId, out var item) ? item.NameAr : null, itemNameEn = items.TryGetValue(x.InventoryItemId, out var item2) ? item2.NameEn : null, x.SystemQuantity, x.CountedQuantity, x.Variance, x.Reason, x.UnitId, adjusted = InventoryRules.IsSignificantVariance(x.Variance) }).ToList() };
    private static object TransferRow(StockTransfer transfer, Dictionary<Guid, InventoryItem> items) => new { transfer.Id, transfer.Number, transfer.SourceBranchId, transfer.DestinationBranchId, status = transfer.Status.ToString(), transfer.Reference, transfer.CreatedByUserId, transfer.ShippedAt, transfer.ReceivedByUserId, transfer.ReceivedAt, transfer.CreatedAt, lineCount = transfer.Lines.Count, totalQuantity = transfer.Lines.Sum(x => x.Quantity) };
    private static object TransferDetail(StockTransfer transfer, Dictionary<Guid, InventoryItem> items) => new { transfer.Id, transfer.Number, transfer.SourceBranchId, transfer.DestinationBranchId, status = transfer.Status.ToString(), transfer.Note, transfer.Reference, transfer.CreatedByUserId, transfer.ShippedAt, transfer.ReceivedByUserId, transfer.ReceivedAt, transfer.CreatedAt, lines = transfer.Lines.Select(x => new { x.Id, x.InventoryItemId, itemNameAr = items.TryGetValue(x.InventoryItemId, out var item) ? item.NameAr : null, itemNameEn = items.TryGetValue(x.InventoryItemId, out var item2) ? item2.NameEn : null, x.Quantity, x.UnitId, x.UnitCost }) };
    private static object WasteRow(WasteRecord record, InventoryItem? item) => new { record.Id, record.Number, record.BranchId, record.InventoryItemId, itemNameAr = item?.NameAr, itemNameEn = item?.NameEn, category = record.Category.ToString(), record.Quantity, record.UnitId, record.Reason, record.Reference, record.OrderId, record.OccurredAt, record.CreatedByUserId };
    private static object WasteDetail(WasteRecord record, InventoryItem? item) => new { record.Id, record.Number, record.BranchId, record.InventoryItemId, itemNameAr = item?.NameAr, itemNameEn = item?.NameEn, itemSku = item?.Sku, category = record.Category.ToString(), record.Quantity, record.UnitId, record.Reason, record.Note, record.PhotoUrl, record.Reference, record.OrderId, record.OrderLineId, record.ShiftId, record.InventoryMovementId, record.CreatedByUserId, record.DeviceId, record.ClientRecordId, record.OccurredAt, record.CreatedAt };

    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct, string permission) => user.HasClaim("permission", permission) && (user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct));
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private sealed record CreateCountLineRequest(Guid InventoryItemId, decimal CountedQuantity, string? Reason);
    private sealed record CreateCountRequest(Guid BranchId, Guid ClientCountId, string? Note, List<CreateCountLineRequest> Lines);
    private sealed record CreateTransferLineRequest(Guid InventoryItemId, decimal Quantity);
    private sealed record CreateTransferRequest(Guid SourceBranchId, Guid DestinationBranchId, Guid ClientTransferId, string? Note, string? Reference, List<CreateTransferLineRequest> Lines);
    private sealed record CreateWasteRequest(Guid BranchId, Guid InventoryItemId, WasteCategory Category, decimal Quantity, Guid UnitId, string? Reason, string? Note, string? PhotoUrl, string? Reference, Guid? OrderId, Guid? OrderLineId, Guid? ShiftId, Guid ClientRecordId, DateTimeOffset? OccurredAt);
}
