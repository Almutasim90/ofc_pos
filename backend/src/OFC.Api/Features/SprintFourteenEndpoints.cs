using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Inventory;
using OFC.Modules.Procurement;

namespace OFC.Api.Features;

public static class SprintFourteenEndpoints
{
    public static void MapSprintFourteenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/procurement");
        api.MapGet("/suppliers", ListSuppliers).RequireAuthorization();
        api.MapGet("/suppliers/{id:guid}", GetSupplier).RequireAuthorization();
        api.MapPost("/suppliers", CreateSupplier).RequireAuthorization();
        api.MapPut("/suppliers/{id:guid}", UpdateSupplier).RequireAuthorization();
        api.MapGet("/suppliers/{id:guid}/history", SupplierHistory).RequireAuthorization();
        api.MapGet("/purchase-orders", ListPurchaseOrders).RequireAuthorization();
        api.MapGet("/purchase-orders/{id:guid}", GetPurchaseOrder).RequireAuthorization();
        api.MapPost("/purchase-orders", CreatePurchaseOrder).RequireAuthorization();
        api.MapPost("/purchase-orders/{id:guid}/submit", SubmitPurchaseOrder).RequireAuthorization();
        api.MapPost("/purchase-orders/{id:guid}/approve", ApprovePurchaseOrder).RequireAuthorization();
        api.MapPost("/purchase-orders/{id:guid}/reject", RejectPurchaseOrder).RequireAuthorization();
        api.MapPost("/purchase-orders/{id:guid}/cancel", CancelPurchaseOrder).RequireAuthorization();
        api.MapPost("/purchase-orders/{id:guid}/receive", ReceivePurchaseOrder).RequireAuthorization();
        api.MapGet("/goods-receipts", ListGoodsReceipts).RequireAuthorization();
        api.MapGet("/goods-receipts/{id:guid}", GetGoodsReceipt).RequireAuthorization();
        api.MapPost("/goods-receipts", CreateGoodsReceipt).RequireAuthorization();
        api.MapPost("/goods-receipts/{id:guid}/post", PostGoodsReceipt).RequireAuthorization();
    }

    private static async Task<IResult> ListSuppliers(string? search, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "procurement.view")) return Forbidden();
        IQueryable<Supplier> query = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLowerInvariant(); query = query.Where(x => x.NameAr.ToLower().Contains(term) || x.NameEn.ToLower().Contains(term) || x.Code.ToLower().Contains(term)); }
        var suppliers = await query.OrderBy(x => x.NameAr).Take(200).ToListAsync(ct);
        return Results.Ok(suppliers.Select(x => SupplierRow(x)));
    }

    private static async Task<IResult> GetSupplier(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "procurement.view")) return Forbidden();
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return supplier is null ? Results.NotFound() : Results.Ok(SupplierDetail(supplier));
    }

    private static async Task<IResult> CreateSupplier(CreateSupplierRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "procurement.suppliers.manage")) return Forbidden();
        if (!ProcurementRules.ValidName(request.NameAr) || !ProcurementRules.ValidName(request.NameEn) || !ProcurementRules.ValidContact(request.ContactPerson) || !ProcurementRules.ValidPhone(request.Phone) || !ProcurementRules.ValidEmail(request.Email) || !ProcurementRules.ValidVat(request.VatNumber) || !ProcurementRules.ValidAddress(request.Address) || !ProcurementRules.ValidNotes(request.Notes)) return Validation("supplier", "The supplier details are invalid or exceed their length limits.");
        var code = string.IsNullOrWhiteSpace(request.Code) ? await NextSupplierCode(db, ct) : request.Code.Trim();
        if (!ProcurementRules.ValidCode(code)) return Validation("code", "The supplier code is invalid.");
        var exists = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, ct);
        if (exists is not null) return Validation("code", "A supplier with this code already exists.");
        if (!string.IsNullOrWhiteSpace(request.VatNumber) && await db.Suppliers.AsNoTracking().AnyAsync(x => x.VatNumber == request.VatNumber.Trim(), ct)) return Validation("vatNumber", "A supplier with this VAT number already exists.");
        var supplier = new Supplier { Code = code, NameAr = request.NameAr!.Trim(), NameEn = request.NameEn!.Trim(), ContactPerson = request.ContactPerson?.Trim(), Phone = request.Phone?.Trim(), Email = request.Email?.Trim(), VatNumber = request.VatNumber?.Trim(), Address = request.Address?.Trim(), Notes = request.Notes?.Trim() };
        try { db.Suppliers.Add(supplier); identity.Audit(UserId(user), null, DeviceId(user), "procurement.supplier.create", "supplier", supplier.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { supplier.Code, supplier.NameAr, supplier.NameEn, supplier.VatNumber })); await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Validation("code", "A supplier with this code or VAT number already exists."); }
        return Results.Created($"/api/v1/procurement/suppliers/{supplier.Id}", SupplierDetail(supplier));
    }

    private static async Task<IResult> UpdateSupplier(Guid id, UpdateSupplierRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "procurement.suppliers.manage")) return Forbidden();
        if (!ProcurementRules.ValidName(request.NameAr) || !ProcurementRules.ValidName(request.NameEn) || !ProcurementRules.ValidContact(request.ContactPerson) || !ProcurementRules.ValidPhone(request.Phone) || !ProcurementRules.ValidEmail(request.Email) || !ProcurementRules.ValidVat(request.VatNumber) || !ProcurementRules.ValidAddress(request.Address) || !ProcurementRules.ValidNotes(request.Notes)) return Validation("supplier", "The supplier details are invalid or exceed their length limits.");
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        var previous = JsonSerializer.Serialize(supplier);
        supplier.NameAr = request.NameAr!.Trim(); supplier.NameEn = request.NameEn!.Trim(); supplier.ContactPerson = request.ContactPerson?.Trim(); supplier.Phone = request.Phone?.Trim(); supplier.Email = request.Email?.Trim(); supplier.VatNumber = request.VatNumber?.Trim(); supplier.Address = request.Address?.Trim(); supplier.Notes = request.Notes?.Trim(); supplier.IsActive = request.IsActive;
        identity.Audit(UserId(user), null, DeviceId(user), "procurement.supplier.update", "supplier", supplier.Id.ToString(), context.TraceIdentifier, oldValue: previous, newValue: JsonSerializer.Serialize(new { supplier.NameAr, supplier.NameEn, supplier.VatNumber, supplier.IsActive }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(SupplierDetail(supplier));
    }

    private static async Task<IResult> SupplierHistory(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "procurement.view")) return Forbidden();
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (supplier is null) return Results.NotFound();
        var receipts = await db.GoodsReceipts.AsNoTracking().Include(x => x.Lines).Where(x => x.SupplierId == id && x.Status == GoodsReceiptStatus.Posted).OrderByDescending(x => x.PostedAt).ToListAsync(ct);
        var itemIds = receipts.SelectMany(x => x.Lines.Select(l => l.InventoryItemId)).Distinct().ToList();
        var items = itemIds.Count == 0 ? new Dictionary<Guid, InventoryItem>() : await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id)).Include(x => x.BaseUnit).ToDictionaryAsync(x => x.Id, x => x, ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(x => receipts.SelectMany(r => r.Lines.Select(l => l.UnitId)).Distinct().Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var rows = receipts.SelectMany(r => r.Lines.Select(l => new { r.Number, r.PostedAt, r.BranchId, l.InventoryItemId, l.Quantity, l.UnitId, l.UnitCost, l.TotalAmount })).OrderBy(x => x.PostedAt).ToList();
        var costStats = rows.GroupBy(x => x.InventoryItemId).Select(g => { var latest = g.OrderByDescending(x => x.PostedAt).First(); return new { inventoryItemId = g.Key, latestUnitCost = latest.UnitCost, lastReceivedAt = latest.PostedAt, unitCode = units.TryGetValue(latest.UnitId, out var u) ? u.Code : null, receivedQuantity = ProcurementRules.RoundQuantity(g.Sum(x => x.Quantity)), totalAmount = ProcurementRules.RoundMoney(g.Sum(x => x.TotalAmount)) }; }).OrderByDescending(x => x.totalAmount);
        return Results.Ok(new { supplier = SupplierDetail(supplier), summary = new { receiptCount = receipts.Count, lineCount = rows.Count, totalAmount = ProcurementRules.RoundMoney(rows.Sum(x => x.TotalAmount)) }, history = rows.Select(x => new { receiptNumber = x.Number, x.PostedAt, x.BranchId, x.InventoryItemId, itemNameAr = items.TryGetValue(x.InventoryItemId, out var i) ? i.NameAr : null, itemNameEn = items.TryGetValue(x.InventoryItemId, out var i2) ? i2.NameEn : null, x.Quantity, x.UnitId, unitCode = units.TryGetValue(x.UnitId, out var unit) ? unit.Code : null, x.UnitCost, x.TotalAmount }), itemCostSummary = costStats });
    }

    private static async Task<IResult> ListPurchaseOrders(Guid branchId, string? status, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "procurement.view")) return Forbidden();
        IQueryable<PurchaseOrder> query = db.PurchaseOrders.AsNoTracking().Where(x => x.BranchId == branchId).Include(x => x.Lines);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PurchaseOrderStatus>(status, ignoreCase: true, out var parsed)) return Validation("status", "The purchase order status is invalid.");
            query = query.Where(x => x.Status == parsed);
        }
        var orders = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var supplierIds = orders.Select(x => x.SupplierId).Distinct().ToList();
        var suppliers = supplierIds.Count == 0 ? new Dictionary<Guid, Supplier>() : await db.Suppliers.AsNoTracking().Where(x => supplierIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(orders.Select(x => PurchaseOrderRow(x, suppliers.TryGetValue(x.SupplierId, out var s) ? s : null)));
    }

    private static async Task<IResult> GetPurchaseOrder(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "procurement.view")) return Forbidden();
        var order = await db.PurchaseOrders.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct, "procurement.view")) return Forbidden();
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.SupplierId, ct);
        return Results.Ok(PurchaseOrderDetail(order, supplier));
    }

    private static async Task<IResult> CreatePurchaseOrder(CreatePurchaseOrderRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct, "procurement.purchase-orders.manage")) return Forbidden();
        if (!ProcurementRules.ValidNotes(request.Notes) || !ProcurementRules.ValidReference(request.Reference)) return Validation("purchaseOrder", "The purchase order notes or reference are invalid.");
        if (request.Lines is not { Count: > 0 } || !ProcurementRules.ValidLineCount(request.Lines.Count)) return Validation("lines", "A purchase order requires between 1 and 500 lines.");
        if (!await db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == request.SupplierId && x.IsActive, ct)) return Validation("supplierId", "The supplier does not exist.");
        var lines = await BuildPurchaseOrderLines(request.Lines, db, ct);
        if (lines is null) return Validation("lines", "One or more purchase order lines reference an unknown inventory item or unit.");
        var order = new PurchaseOrder { Number = await NextPurchaseOrderNumber(db, request.BranchId, ct), SupplierId = request.SupplierId, BranchId = request.BranchId, Status = PurchaseOrderStatus.Draft, ExpectedDate = request.ExpectedDate, Notes = request.Notes?.Trim(), Reference = request.Reference?.Trim(), CreatedByUserId = UserId(user) };
        foreach (var line in lines) order.Lines.Add(line);
        db.PurchaseOrders.Add(order); identity.Audit(UserId(user), request.BranchId, DeviceId(user), "procurement.purchase-order.create", "purchase_order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.Number, order.SupplierId, order.BranchId, lineCount = order.Lines.Count, order.Status }));
        await db.SaveChangesAsync(ct);
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.SupplierId, ct);
        return Results.Created($"/api/v1/procurement/purchase-orders/{order.Id}", PurchaseOrderDetail(order, supplier));
    }

    private static async Task<IResult> SubmitPurchaseOrder(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await LoadPurchaseOrder(db, id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct, "procurement.purchase-orders.manage")) return Forbidden();
        if (!ProcurementRules.CanSubmit(order.Status)) return Validation("status", "Only a draft purchase order can be submitted.");
        order.Status = PurchaseOrderStatus.Submitted;
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "procurement.purchase-order.submit", "purchase_order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.Status }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(PurchaseOrderRow(order, await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.SupplierId, ct)));
    }

    private static async Task<IResult> ApprovePurchaseOrder(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await LoadPurchaseOrder(db, id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct, "procurement.approve")) return Forbidden();
        if (!ProcurementRules.CanApprove(order.Status)) return Validation("status", "Only a submitted purchase order can be approved.");
        order.Status = PurchaseOrderStatus.Approved; order.ApprovedByUserId = UserId(user); order.ApprovedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "procurement.purchase-order.approve", "purchase_order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.Status, order.ApprovedByUserId }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(PurchaseOrderRow(order, await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.SupplierId, ct)));
    }

    private static async Task<IResult> RejectPurchaseOrder(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await LoadPurchaseOrder(db, id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct, "procurement.approve")) return Forbidden();
        if (!ProcurementRules.CanApprove(order.Status)) return Validation("status", "Only a submitted purchase order can be rejected.");
        order.Status = PurchaseOrderStatus.Rejected; order.ApprovedByUserId = UserId(user); order.ApprovedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "procurement.purchase-order.reject", "purchase_order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.Status }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(PurchaseOrderRow(order, await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.SupplierId, ct)));
    }

    private static async Task<IResult> CancelPurchaseOrder(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await LoadPurchaseOrder(db, id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct, "procurement.purchase-orders.manage")) return Forbidden();
        if (!ProcurementRules.CanCancel(order.Status)) return Validation("status", "A received or rejected purchase order cannot be cancelled.");
        order.Status = PurchaseOrderStatus.Cancelled;
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "procurement.purchase-order.cancel", "purchase_order", order.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.Status }));
        await db.SaveChangesAsync(ct);
        return Results.Ok(PurchaseOrderRow(order, await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.SupplierId, ct)));
    }

    private static async Task<IResult> ReceivePurchaseOrder(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var order = await LoadPurchaseOrder(db, id, ct);
        if (order is null) return Results.NotFound();
        if (!await CanOperate(db, user, order.BranchId, ct, "procurement.goods-receipt.manage")) return Forbidden();
        if (!ProcurementRules.CanReceive(order.Status)) return Validation("status", "Only an approved purchase order can be received.");
        var existing = await db.GoodsReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.PurchaseOrderId == order.Id && x.Status == GoodsReceiptStatus.Posted, ct);
        if (existing is not null) return Validation("receipt", "This purchase order has already been received.");
        var remaining = order.Lines.Where(l => ProcurementRules.RoundQuantity(l.Quantity - l.ReceivedQuantity) > 0m).Select(l => new CreateGoodsReceiptLineRequest(l.InventoryItemId, l.UnitId, ProcurementRules.RoundQuantity(l.Quantity - l.ReceivedQuantity), l.UnitCost)).ToList();
        if (remaining.Count == 0) return Validation("receipt", "There is nothing left to receive on this purchase order.");
        var receipt = await PersistGoodsReceipt(db, identity, user, context, order.BranchId, order.SupplierId, order.Id, $"received from {order.Number}", remaining, ct);
        if (receipt is null) return Validation("receipt", "The goods receipt could not be created for this purchase order.");
        await ApplyGoodsReceipt(db, identity, user, context, receipt, ct);
        order.Status = PurchaseOrderStatus.Received; order.ReceivedAt = DateTimeOffset.UtcNow;
        foreach (var line in order.Lines) { var received = receipt.Lines.FirstOrDefault(l => l.InventoryItemId == line.InventoryItemId); if (received is not null) line.ReceivedQuantity = ProcurementRules.RoundQuantity(line.ReceivedQuantity + received.Quantity); }
        await db.SaveChangesAsync(ct);
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.SupplierId, ct);
        return Results.Created($"/api/v1/procurement/goods-receipts/{receipt.Id}", ReceiptDetail(receipt, supplier));
    }

    private static async Task<IResult> ListGoodsReceipts(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "procurement.view")) return Forbidden();
        var receipts = await db.GoodsReceipts.AsNoTracking().Include(x => x.Lines).Where(x => x.BranchId == branchId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var supplierIds = receipts.Select(x => x.SupplierId).Distinct().ToList();
        var suppliers = supplierIds.Count == 0 ? new Dictionary<Guid, Supplier>() : await db.Suppliers.AsNoTracking().Where(x => supplierIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(receipts.Select(x => ReceiptRow(x, suppliers.TryGetValue(x.SupplierId, out var s) ? s : null)));
    }

    private static async Task<IResult> GetGoodsReceipt(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "procurement.view")) return Forbidden();
        var receipt = await db.GoodsReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (receipt is null) return Results.NotFound();
        if (!await CanOperate(db, user, receipt.BranchId, ct, "procurement.view")) return Forbidden();
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.SupplierId, ct);
        return Results.Ok(ReceiptDetail(receipt, supplier));
    }

    private static async Task<IResult> CreateGoodsReceipt(CreateGoodsReceiptRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct, "procurement.goods-receipt.manage")) return Forbidden();
        if (!ProcurementRules.ValidReference(request.Reference) || !ProcurementRules.ValidNotes(request.Notes)) return Validation("receipt", "The goods receipt reference or notes are invalid.");
        if (request.Lines is not { Count: > 0 } || !ProcurementRules.ValidLineCount(request.Lines.Count)) return Validation("lines", "A goods receipt requires between 1 and 500 lines.");
        if (!await db.Suppliers.AsNoTracking().AnyAsync(x => x.Id == request.SupplierId && x.IsActive, ct)) return Validation("supplierId", "The supplier does not exist.");
        if (request.PurchaseOrderId.HasValue && !await db.PurchaseOrders.AsNoTracking().AnyAsync(x => x.Id == request.PurchaseOrderId && x.Status == PurchaseOrderStatus.Approved, ct)) return Validation("purchaseOrderId", "The referenced purchase order is not approved.");
        var existingDraft = await db.GoodsReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.ClientReceiptId == request.ClientReceiptId && x.BranchId == request.BranchId, ct);
        if (existingDraft is not null) return Results.Ok(ReceiptRow(existingDraft, await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == existingDraft.SupplierId, ct)));
        var receipt = await PersistGoodsReceipt(db, identity, user, context, request.BranchId, request.SupplierId, request.PurchaseOrderId, request.Reference, request.Lines, ct);
        if (receipt is null) return Validation("receipt", "The goods receipt could not be created.");
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.SupplierId, ct);
        return Results.Created($"/api/v1/procurement/goods-receipts/{receipt.Id}", ReceiptDetail(receipt, supplier));
    }

    private static async Task<IResult> PostGoodsReceipt(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var receipt = await LoadGoodsReceipt(db, id, ct);
        if (receipt is null) return Results.NotFound();
        if (!await CanOperate(db, user, receipt.BranchId, ct, "procurement.goods-receipt.manage")) return Forbidden();
        var existing = await db.GoodsReceipts.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.ClientReceiptId == receipt.ClientReceiptId && x.Status == GoodsReceiptStatus.Posted, ct);
        if (existing is not null) return Results.Ok(ReceiptDetail(existing, await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == existing.SupplierId, ct)));
        if (!ProcurementRules.CanPost(receipt.Status)) return Validation("status", "Only a draft goods receipt can be posted.");
        await ApplyGoodsReceipt(db, identity, user, context, receipt, ct);
        await db.SaveChangesAsync(ct);
        var supplier = await db.Suppliers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == receipt.SupplierId, ct);
        return Results.Ok(ReceiptDetail(receipt, supplier));
    }

    private static async Task<PurchaseOrder?> LoadPurchaseOrder(OFCDbContext db, Guid id, CancellationToken ct) => await db.PurchaseOrders.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);

    private static async Task<GoodsReceipt?> LoadGoodsReceipt(OFCDbContext db, Guid id, CancellationToken ct) => await db.GoodsReceipts.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct);

    private static async Task<GoodsReceipt?> PersistGoodsReceipt(OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, Guid branchId, Guid supplierId, Guid? purchaseOrderId, string? reference, List<CreateGoodsReceiptLineRequest> lines, CancellationToken ct)
    {
        var built = await BuildGoodsReceiptLines(lines, db, ct);
        if (built is null) return null;
        var receipt = new GoodsReceipt { Number = await NextReceiptNumber(db, branchId, ct), SupplierId = supplierId, BranchId = branchId, PurchaseOrderId = purchaseOrderId, Status = GoodsReceiptStatus.Draft, Reference = reference, CreatedByUserId = UserId(user) };
        foreach (var line in built) receipt.Lines.Add(line);
        db.GoodsReceipts.Add(receipt); identity.Audit(UserId(user), branchId, DeviceId(user), "procurement.goods-receipt.create", "goods_receipt", receipt.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { receipt.Number, receipt.SupplierId, receipt.BranchId, receipt.PurchaseOrderId, lineCount = receipt.Lines.Count }));
        await db.SaveChangesAsync(ct);
        return receipt;
    }

    private static async Task ApplyGoodsReceipt(OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext context, GoodsReceipt receipt, CancellationToken ct)
    {
        var conversions = await db.UnitConversions.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var items = await db.InventoryItems.Include(x => x.BaseUnit).Where(x => receipt.Lines.Select(l => l.InventoryItemId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        if (receipt.Lines.Any(l => !items.ContainsKey(l.InventoryItemId) || !items[l.InventoryItemId].IsActive)) throw new InvalidOperationException("A goods receipt line references an inactive or unknown inventory item.");
        var now = DateTimeOffset.UtcNow;
        foreach (var line in receipt.Lines)
        {
            if (!ProcurementRules.ValidQuantity(line.Quantity) || !ProcurementRules.ValidCost(line.UnitCost)) throw new InvalidOperationException("Goods receipt quantities or costs are invalid.");
            var item = items[line.InventoryItemId];
            var baseQuantity = line.Quantity;
            if (!InventoryRules.TryConvert(line.Quantity, line.UnitId, item.BaseUnitId, conversions, out var converted))
            {
                if (line.UnitId != item.BaseUnitId) throw new InvalidOperationException($"There is no conversion between the supplied unit and the base unit of {item.Sku}.");
            } else { baseQuantity = converted; }
            baseQuantity = InventoryRules.RoundQuantity(baseQuantity);
            var factor = line.Quantity == 0m ? 1m : baseQuantity / line.Quantity;
            var baseUnitCost = ProcurementRules.RoundCost(line.UnitCost * factor);
            var currentStock = InventoryRules.RoundQuantity((await db.InventoryMovements.Where(x => x.BranchId == receipt.BranchId && x.InventoryItemId == item.Id).SumAsync(x => (decimal?)x.Quantity, ct).ConfigureAwait(false) ?? 0m));
            var movement = new InventoryMovement { BranchId = receipt.BranchId, InventoryItemId = item.Id, Type = InventoryMovementType.Purchase, Quantity = baseQuantity, UnitId = item.BaseUnitId, Reference = receipt.Number, Reason = "supplier-receipt", SupplierId = receipt.SupplierId, PurchaseOrderId = receipt.PurchaseOrderId, CreatedByUserId = UserId(user), DeviceId = DeviceId(user), OccurredAt = now };
            db.InventoryMovements.Add(movement);
            item.StockOnHand = InventoryRules.RoundQuantity(currentStock + baseQuantity);
            item.UnitCost = ProcurementRules.WeightedAverageCost(currentStock, item.UnitCost, baseQuantity, baseUnitCost);
        }
        receipt.Status = GoodsReceiptStatus.Posted; receipt.PostedByUserId = UserId(user); receipt.PostedAt = now;
        identity.Audit(UserId(user), receipt.BranchId, DeviceId(user), "procurement.goods-receipt.post", "goods_receipt", receipt.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { receipt.Number, movementCount = receipt.Lines.Count, supplierId = receipt.SupplierId, purchaseOrderId = receipt.PurchaseOrderId }));
    }

    private static async Task<List<PurchaseOrderLine>?> BuildPurchaseOrderLines(List<CreatePurchaseOrderLineRequest> lines, OFCDbContext db, CancellationToken ct)
    {
        var itemIds = lines.Select(x => x.InventoryItemId).Distinct().ToList();
        var unitIds = lines.Select(x => x.UnitId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, x => x, ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(x => unitIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, x => x, ct);
        var result = new List<PurchaseOrderLine>();
        foreach (var line in lines)
        {
            if (!ProcurementRules.ValidQuantity(line.Quantity) || !ProcurementRules.ValidCost(line.UnitCost) || !items.ContainsKey(line.InventoryItemId) || !units.ContainsKey(line.UnitId)) return null;
            result.Add(new PurchaseOrderLine { InventoryItemId = line.InventoryItemId, Quantity = ProcurementRules.RoundQuantity(line.Quantity), UnitId = line.UnitId, UnitCost = ProcurementRules.RoundCost(line.UnitCost), TaxAmount = 0m, TotalAmount = ProcurementRules.LineTotal(line.Quantity, line.UnitCost) });
        }
        return result;
    }

    private static async Task<List<GoodsReceiptLine>?> BuildGoodsReceiptLines(List<CreateGoodsReceiptLineRequest> lines, OFCDbContext db, CancellationToken ct)
    {
        var itemIds = lines.Select(x => x.InventoryItemId).Distinct().ToList();
        var unitIds = lines.Select(x => x.UnitId).Distinct().ToList();
        var items = await db.InventoryItems.AsNoTracking().Where(x => itemIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, x => x, ct);
        var units = await db.UnitsOfMeasure.AsNoTracking().Where(x => unitIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, x => x, ct);
        var result = new List<GoodsReceiptLine>();
        foreach (var line in lines)
        {
            if (!ProcurementRules.ValidQuantity(line.Quantity) || !ProcurementRules.ValidCost(line.UnitCost) || !items.ContainsKey(line.InventoryItemId) || !units.ContainsKey(line.UnitId)) return null;
            result.Add(new GoodsReceiptLine { InventoryItemId = line.InventoryItemId, Quantity = ProcurementRules.RoundQuantity(line.Quantity), UnitId = line.UnitId, UnitCost = ProcurementRules.RoundCost(line.UnitCost), TotalAmount = ProcurementRules.LineTotal(line.Quantity, line.UnitCost) });
        }
        return result;
    }

    private static async Task<string> NextSupplierCode(OFCDbContext db, CancellationToken ct) => $"SUP-{await db.Suppliers.CountAsync(ct) + 1:D4}";
    private static async Task<string> NextPurchaseOrderNumber(OFCDbContext db, Guid branchId, CancellationToken ct) => $"PO-{DateTimeOffset.UtcNow:yyyyMMdd}-{await db.PurchaseOrders.CountAsync(x => x.BranchId == branchId, ct) + 1:D4}";
    private static async Task<string> NextReceiptNumber(OFCDbContext db, Guid branchId, CancellationToken ct) => $"GR-{DateTimeOffset.UtcNow:yyyyMMdd}-{await db.GoodsReceipts.CountAsync(x => x.BranchId == branchId, ct) + 1:D4}";

    private static object SupplierRow(Supplier supplier) => new { supplier.Id, supplier.Code, supplier.NameAr, supplier.NameEn, supplier.VatNumber, supplier.Phone, supplier.Email, supplier.IsActive, supplier.CreatedAt };
    private static object SupplierDetail(Supplier supplier) => new { supplier.Id, supplier.Code, supplier.NameAr, supplier.NameEn, supplier.ContactPerson, supplier.Phone, supplier.Email, supplier.VatNumber, supplier.Address, supplier.Notes, supplier.IsActive, supplier.CreatedAt };
    private static object PurchaseOrderRow(PurchaseOrder order, Supplier? supplier) => new { order.Id, order.Number, order.SupplierId, supplierCode = supplier?.Code, supplierNameAr = supplier?.NameAr, supplierNameEn = supplier?.NameEn, order.BranchId, status = order.Status.ToString(), order.ExpectedDate, order.Reference, order.CreatedByUserId, order.ApprovedByUserId, order.ApprovedAt, order.ReceivedAt, order.CreatedAt, lineCount = order.Lines.Count, totalAmount = ProcurementRules.RoundMoney(order.Lines.Sum(x => x.TotalAmount)) };
    private static object PurchaseOrderDetail(PurchaseOrder order, Supplier? supplier)
    {
        return new { order.Id, order.Number, order.SupplierId, supplierCode = supplier?.Code, supplierNameAr = supplier?.NameAr, supplierNameEn = supplier?.NameEn, order.BranchId, status = order.Status.ToString(), order.ExpectedDate, order.Reference, order.Notes, order.CreatedByUserId, order.ApprovedByUserId, order.ApprovedAt, order.ReceivedAt, order.CreatedAt, totalAmount = ProcurementRules.RoundMoney(order.Lines.Sum(x => x.TotalAmount)), lines = order.Lines.Select(x => new { x.Id, x.InventoryItemId, x.Quantity, x.UnitId, x.UnitCost, x.TaxAmount, x.TotalAmount, x.ReceivedQuantity }).ToList() };
    }
    private static object ReceiptRow(GoodsReceipt receipt, Supplier? supplier) => new { receipt.Id, receipt.Number, receipt.SupplierId, supplierCode = supplier?.Code, supplierNameAr = supplier?.NameAr, supplierNameEn = supplier?.NameEn, receipt.BranchId, receipt.PurchaseOrderId, status = receipt.Status.ToString(), receipt.Reference, receipt.CreatedByUserId, receipt.PostedByUserId, receipt.PostedAt, receipt.CreatedAt, lineCount = receipt.Lines.Count, totalAmount = ProcurementRules.RoundMoney(receipt.Lines.Sum(x => x.TotalAmount)) };
    private static object ReceiptDetail(GoodsReceipt receipt, Supplier? supplier)
    {
        return new { receipt.Id, receipt.Number, receipt.SupplierId, supplierCode = supplier?.Code, supplierNameAr = supplier?.NameAr, supplierNameEn = supplier?.NameEn, receipt.BranchId, receipt.PurchaseOrderId, status = receipt.Status.ToString(), receipt.Reference, receipt.Notes, receipt.CreatedByUserId, receipt.PostedByUserId, receipt.PostedAt, receipt.CreatedAt, totalAmount = ProcurementRules.RoundMoney(receipt.Lines.Sum(x => x.TotalAmount)), lines = receipt.Lines.Select(x => new { x.Id, x.InventoryItemId, x.Quantity, x.UnitId, x.UnitCost, x.TotalAmount }).ToList() };
    }

    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct, string permission) => user.HasClaim("permission", permission) && (user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct));
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private sealed record CreateSupplierRequest(string? Code, string? NameAr, string? NameEn, string? ContactPerson, string? Phone, string? Email, string? VatNumber, string? Address, string? Notes);
    private sealed record UpdateSupplierRequest(string? NameAr, string? NameEn, string? ContactPerson, string? Phone, string? Email, string? VatNumber, string? Address, string? Notes, bool IsActive);
    private sealed record CreatePurchaseOrderLineRequest(Guid InventoryItemId, Guid UnitId, decimal Quantity, decimal UnitCost);
    private sealed record CreatePurchaseOrderRequest(Guid SupplierId, Guid BranchId, DateTimeOffset? ExpectedDate, string? Notes, string? Reference, List<CreatePurchaseOrderLineRequest> Lines);
    private sealed record CreateGoodsReceiptLineRequest(Guid InventoryItemId, Guid UnitId, decimal Quantity, decimal UnitCost);
    private sealed record CreateGoodsReceiptRequest(Guid SupplierId, Guid BranchId, Guid? PurchaseOrderId, string? Reference, string? Notes, Guid ClientReceiptId, List<CreateGoodsReceiptLineRequest> Lines);
}
