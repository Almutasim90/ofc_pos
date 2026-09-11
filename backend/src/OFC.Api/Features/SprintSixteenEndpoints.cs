using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Kitchen;
using OFC.Modules.Ordering;
using OFC.Modules.Organization;
using OFC.Modules.QrOrdering;

namespace OFC.Api.Features;

public static class SprintSixteenEndpoints
{
    public static void MapSprintSixteenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1/qr");
        // These four routes are the only unauthenticated, anonymous endpoints in the whole API — rate
        // limited per client IP so they can't be scraped or flooded (docs/04-Sprint-Audit P1).
        api.MapGet("/{code}", ResolveContext).RequireRateLimiting("qr-anonymous");
        api.MapGet("/{code}/menu", CustomerMenu).RequireRateLimiting("qr-anonymous");
        api.MapPost("/{code}/orders", SubmitOrder).RequireRateLimiting("qr-anonymous");
        api.MapGet("/{code}/orders/{clientRequestId:guid}", TrackOrder).RequireRateLimiting("qr-anonymous");
        api.MapGet("/contexts", ListContexts).RequireAuthorization();
        api.MapPost("/contexts", CreateContext).RequireAuthorization();
        api.MapPost("/contexts/{id:guid}/toggle", ToggleContext).RequireAuthorization();
        api.MapGet("/orders", ListOrders).RequireAuthorization();
        api.MapPost("/approvals/{id:guid}/review", ReviewApproval).RequireAuthorization();
    }

    private static async Task<IResult> ResolveContext(string code, OFCDbContext db, CancellationToken ct)
    {
        var context = await ActiveContext(db, code, ct);
        return context is null ? Results.NotFound() : Results.Ok(ContextResponse(context.Value.Branch, context.Value.Channel, context.Value.Context));
    }

    private static async Task<IResult> CustomerMenu(string code, OFCDbContext db, CancellationToken ct)
    {
        var context = await ActiveContext(db, code, ct);
        if (context is null) return Results.NotFound();
        var (ctx, branch, channel) = context.Value;
        var products = await db.Products.AsNoTracking().Where(x => x.IsActive && x.BranchAvailability.Any(a => a.BranchId == ctx.BranchId && a.IsAvailable)).Include(x => x.Category).Include(x => x.Images).Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product).OrderBy(x => x.CategoryId).ThenBy(x => x.NameAr).ToListAsync(ct);
        var productIds = products.Select(x => x.Id).ToList();
        var prices = await db.PriceRules.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToListAsync(ct);
        var promotions = await db.Promotions.AsNoTracking().Where(x => x.ProductId == null || (x.ProductId.HasValue && productIds.Contains(x.ProductId.Value))).ToListAsync(ct);
        var taxIds = products.Where(x => x.TaxCategoryId.HasValue).Select(x => x.TaxCategoryId!.Value).Distinct().ToList();
        var taxes = taxIds.Count == 0 ? [] : await db.TaxRules.AsNoTracking().Where(x => taxIds.Contains(x.TaxCategoryId)).ToListAsync(ct);
        var version = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);
        var at = DateTimeOffset.UtcNow;
        var rows = products.Select(product =>
        {
            var price = PricingRules.Resolve(product, ctx.BranchId, channel.Id, at, prices, promotions, taxes, version);
            return new
            {
                product.Id,
                product.CategoryId,
                categoryNameAr = product.Category!.NameAr,
                categoryNameEn = product.Category.NameEn,
                product.Sku,
                product.NameAr,
                product.NameEn,
                product.Type,
                product.BasePrice,
                listAmount = price.ListPrice,
                discountAmount = price.DiscountAmount,
                netAmount = price.UnitNetAmount,
                taxAmount = price.UnitTaxAmount,
                grossAmount = price.UnitGrossAmount,
                priceSource = price.PriceSource,
                imageUrl = product.Images.OrderBy(x => x.SortOrder).Select(x => x.Url).FirstOrDefault(),
                selectionGroups = product.SelectionGroups.OrderBy(x => x.SortOrder).Where(x => x.SelectionGroup!.IsActive && (!x.SelectionGroup.BranchAvailability.Any() || x.SelectionGroup.BranchAvailability.Any(a => a.BranchId == ctx.BranchId && a.IsAvailable))).Select(x => new { x.SelectionGroup!.Id, x.SelectionGroup.Kind, x.SelectionGroup.NameAr, x.SelectionGroup.NameEn, x.SelectionGroup.IsRequired, x.SelectionGroup.MinSelections, x.SelectionGroup.MaxSelections, options = x.SelectionGroup.Options.OrderBy(o => o.SortOrder).Select(o => new { o.Id, o.ProductId, nameAr = o.Product!.NameAr, nameEn = o.Product.NameEn, o.PriceAdjustment, o.IsDefault, o.MaxQuantity }) })
            };
        }).ToList();
        return Results.Ok(new { context = ContextResponse(branch, channel, ctx), products = rows });
    }

    private static async Task<IResult> SubmitOrder(string code, SubmitOrderRequest request, OFCDbContext db, IdentityService identity, IOrdersBroadcaster ordersBroadcaster, IKitchenBroadcaster kitchenBroadcaster, HttpContext httpContext, CancellationToken ct)
    {
        var context = await ActiveContext(db, code, ct);
        if (context is null) return Results.NotFound();
        var (ctx, _, channel) = context.Value;
        if (request.ClientRequestId == Guid.Empty) return Validation("clientRequestId", "A client request id is required.");
        if (request.Lines is not { Count: > 0 } || request.Lines.Count > 100 || request.Note?.Trim().Length > OrderRules.NoteMax) return Validation("lines", "Provide between 1 and 100 order lines and a valid note.");
        if (request.Customer is not null)
        {
            if (!QrRules.ValidOptionalName(request.Customer.NameAr) || !QrRules.ValidOptionalName(request.Customer.NameEn)) return Validation("customer", "The customer name is invalid.");
            if (!QrRules.ValidPhone(request.Customer.Phone)) return Validation("customer", "The customer phone is invalid.");
            if (!QrRules.ValidExternalId(request.Customer.ExternalId)) return Validation("customer", "The customer external id is invalid.");
            if (!QrRules.ValidLoyaltyReference(request.Customer.LoyaltyReference)) return Validation("customer", "The customer loyalty reference is invalid.");
        }

        var existing = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == ctx.BranchId && x.ClientRequestId == request.ClientRequestId, ct);
        if (existing is not null) return Results.Ok(OrderTracking(existing, await db.QrOrderApprovals.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == existing.Id, ct)));

        var productIds = request.Lines.Select(x => x.ProductId).Distinct().ToList();
        var products = await db.Products.Include(x => x.SelectionGroups).ThenInclude(x => x.SelectionGroup).ThenInclude(x => x!.Options).ThenInclude(x => x.Product).Where(x => productIds.Contains(x.Id) && x.IsActive && x.BranchAvailability.Any(a => a.BranchId == ctx.BranchId && a.IsAvailable)).ToDictionaryAsync(x => x.Id, x => x, ct);
        if (products.Count != productIds.Count) return Validation("lines", "One or more products are unavailable at this branch.");
        var at = DateTimeOffset.UtcNow;
        var prices = await db.PriceRules.AsNoTracking().Where(x => productIds.Contains(x.ProductId)).ToListAsync(ct);
        var promotions = await db.Promotions.AsNoTracking().Where(x => x.ProductId == null || productIds.Contains(x.ProductId.Value)).ToListAsync(ct);
        var taxIds = products.Values.Where(x => x.TaxCategoryId.HasValue).Select(x => x.TaxCategoryId!.Value).Distinct().ToList();
        var taxes = await db.TaxRules.AsNoTracking().Where(x => taxIds.Contains(x.TaxCategoryId)).ToListAsync(ct);
        var version = await db.CatalogVersions.AsNoTracking().OrderByDescending(x => x.Number).FirstOrDefaultAsync(ct);

        Guid? customerId = request.Customer is null ? null : await ResolveCustomer(db, request.Customer, ct);
        var built = OrderingEngine.Build(ctx.BranchId, channel.Id, OrderSource.Qr, null, customerId, null, request.Note, request.ClientRequestId, at, request.Lines.Select(x => new OrderLineInput(x.ProductId, x.Quantity, x.Note, x.Selections?.Select(s => new GroupSelectionInput(s.SelectionGroupId, s.Choices.Select(c => new ChoiceInput(c.OptionId, c.Quantity)).ToList())).ToList())).ToList(), products, prices, promotions, taxes, version);
        if (!built.Succeeded) return Validation(built.Field!, built.Error!);
        var order = built.Order!;
        // Table-QR orders carry their table's code so staff can look them up the same way as a
        // manually-entered dine-in table number (PosSection's current-orders table search).
        if (ctx.Kind == QrContextKind.Table) order.TableNumber = ctx.Code;

        var requiresApproval = QrRules.RequiresStaffApproval(ctx.ApprovalMode);
        order.StatusHistory.Add(new OrderStatusHistory { FromStatus = OrderStatus.Draft, ToStatus = OrderStatus.Pending, ChangedByUserId = null, Note = "Qr order submitted" });
        var approval = new QrOrderApproval { OrderId = order.Id, QrContextId = ctx.Id, CustomerId = customerId, Status = requiresApproval ? QrOrderApprovalStatus.Pending : QrOrderApprovalStatus.Approved };
        // No staff approval needed → the order is confirmed the instant it's placed, so it goes straight to
        // the kitchen here instead of sitting in Current orders until a cashier notices and dispatches it.
        List<KitchenTicket> kitchenTickets = [];
        if (!requiresApproval)
        {
            order.Status = OrderStatus.Confirmed;
            order.UpdatedAt = at;
            approval.ReviewedAt = at;
            order.StatusHistory.Add(new OrderStatusHistory { FromStatus = OrderStatus.Pending, ToStatus = OrderStatus.Confirmed, ChangedByUserId = null, Note = "Auto-approved" });
            kitchenTickets = SprintTenEndpoints.BuildTickets(db, identity, ctx.BranchId, order, products, Guid.NewGuid(), null, null, null, null, null, httpContext.TraceIdentifier);
        }
        else
        {
            order.Status = OrderStatus.Pending;
        }

        db.Orders.Add(order);
        db.QrOrderApprovals.Add(approval);
        identity.Audit(null, ctx.BranchId, null, "qr.order.submit", "order", order.Id.ToString(), httpContext.TraceIdentifier, newValue: JsonSerializer.Serialize(new { order.ClientRequestId, source = order.Source.ToString(), order.Status, order.GrossAmount, requiresApproval, ctx.Code }));
        if (!requiresApproval) identity.Audit(null, ctx.BranchId, null, "qr.order.approve", "order", order.Id.ToString(), httpContext.TraceIdentifier, newValue: JsonSerializer.Serialize(new { approval.Status, ctx.Code }));

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            var duplicate = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == ctx.BranchId && x.ClientRequestId == request.ClientRequestId, ct);
            if (duplicate is not null) return Results.Ok(OrderTracking(duplicate, await db.QrOrderApprovals.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == duplicate.Id, ct)));
            throw;
        }

        // Realtime staff notification: a fresh QR order landed for this branch (pending staff approval,
        // or already auto-approved). Each connected screen re-fetches authoritative data — no state is
        // carried over the socket (docs/01-ARCHITECTURE-GUARDRAILS.md).
        await ordersBroadcaster.QrOrderReceived(ctx.BranchId, order.Id, order.ClientRequestId.ToString(), order.Status.ToString(), approval.Status.ToString(), order.GrossAmount, ctx.Code);
        foreach (var ticket in kitchenTickets) await kitchenBroadcaster.TicketChanged(ctx.BranchId, ticket.Id, "dispatched");

        return Results.Created($"/api/v1/qr/{ctx.Code}/orders/{order.ClientRequestId}", OrderTracking(order, approval));
    }

    private static async Task<IResult> TrackOrder(string code, Guid clientRequestId, OFCDbContext db, CancellationToken ct)
    {
        var ctx = await db.QrContexts.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, ct);
        if (ctx is null) return Results.NotFound();
        var order = await db.Orders.AsNoTracking().Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.BranchId == ctx.BranchId && x.ClientRequestId == clientRequestId, ct);
        if (order is null) return Results.NotFound();
        var approval = await db.QrOrderApprovals.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == order.Id, ct);
        return Results.Ok(OrderTracking(order, approval));
    }

    private static async Task<IResult> ListContexts(Guid branchId, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, "qr.manage", ct)) return Forbidden();
        var contexts = await db.QrContexts.AsNoTracking().Where(x => x.BranchId == branchId).OrderBy(x => x.Code).ToListAsync(ct);
        var channelIds = contexts.Select(x => x.SalesChannelId).Distinct().ToList();
        var channels = channelIds.Count == 0 ? new Dictionary<Guid, SalesChannel>() : await db.SalesChannels.AsNoTracking().Where(x => channelIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(contexts.Select(x => new { x.Id, x.BranchId, x.Code, x.Kind, x.NameAr, x.NameEn, x.ApprovalMode, x.IsActive, x.SalesChannelId, salesChannelCode = channels.TryGetValue(x.SalesChannelId, out var c) ? c.Code : null, salesChannelNameAr = channels.TryGetValue(x.SalesChannelId, out var c2) ? c2.NameAr : null, salesChannelNameEn = channels.TryGetValue(x.SalesChannelId, out var c3) ? c3.NameEn : null, x.CreatedAt }));
    }

    private static async Task<IResult> CreateContext(CreateContextRequest request, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext httpContext, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, "qr.manage", ct)) return Forbidden();
        if (!QrRules.ValidCode(request.Code)) return Validation("code", "The QR code is invalid or reserved.");
        if (!QrRules.ValidName(request.NameAr) || !QrRules.ValidName(request.NameEn)) return Validation("name", "The QR context name is invalid.");
        if (!QrRules.ValidContextKind(request.Kind)) return Validation("kind", "The QR context kind is invalid.");
        if (!QrRules.ValidApprovalMode(request.ApprovalMode)) return Validation("approvalMode", "The approval mode is invalid.");
        if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.IsActive, ct)) return Validation("branchId", "The branch is invalid.");
        if (!await db.SalesChannels.AnyAsync(x => x.Id == request.SalesChannelId && x.IsActive, ct)) return Validation("salesChannelId", "The sales channel is invalid.");
        if (await db.QrContexts.AnyAsync(x => x.BranchId == request.BranchId && x.Code == request.Code.Trim().ToUpperInvariant(), ct)) return Validation("code", "This QR code already exists for the branch.");
        var ctx = new QrContext { BranchId = request.BranchId, SalesChannelId = request.SalesChannelId, Kind = request.Kind, Code = request.Code.Trim().ToUpperInvariant(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), ApprovalMode = request.ApprovalMode, IsActive = request.IsActive };
        db.QrContexts.Add(ctx);
        identity.Audit(UserId(user), request.BranchId, DeviceId(user), "qr.context.create", "qr_context", ctx.Id.ToString(), httpContext.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ctx.Code, ctx.Kind, ctx.ApprovalMode, ctx.IsActive }));
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/qr/{ctx.Code}", new { ctx.Id, ctx.BranchId, ctx.Code, ctx.Kind, ctx.NameAr, ctx.NameEn, ctx.ApprovalMode, ctx.IsActive });
    }

    private static async Task<IResult> ToggleContext(Guid id, OFCDbContext db, IdentityService identity, ClaimsPrincipal user, HttpContext httpContext, CancellationToken ct)
    {
        var ctx = await db.QrContexts.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ctx is null) return Results.NotFound();
        if (!await CanOperate(db, user, ctx.BranchId, "qr.manage", ct)) return Forbidden();
        ctx.IsActive = !ctx.IsActive;
        identity.Audit(UserId(user), ctx.BranchId, DeviceId(user), "qr.context.toggle", "qr_context", ctx.Id.ToString(), httpContext.TraceIdentifier, oldValue: JsonSerializer.Serialize(!ctx.IsActive), newValue: JsonSerializer.Serialize(ctx.IsActive));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { ctx.Id, ctx.IsActive });
    }

    private static async Task<IResult> ListOrders(Guid branchId, OrderStatus? status, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        // A cashier who can run the floor (orders.manage) needs to see and act on QR orders day to day —
        // the qr.manage/qr.approve permissions stay for QR-code setup, not for gating routine order review.
        if (!await CanOperate(db, user, branchId, "qr.manage", ct) && !await CanOperate(db, user, branchId, "orders.manage", ct)) return Forbidden();
        var query = db.Orders.AsNoTracking().Include(x => x.Lines).Where(x => x.BranchId == branchId && x.Source == OrderSource.Qr);
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        var orders = await query.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(ct);
        var approvalIds = orders.Select(x => x.Id).ToList();
        var approvals = approvalIds.Count == 0 ? new Dictionary<Guid, QrOrderApproval>() : await db.QrOrderApprovals.AsNoTracking().Where(x => approvalIds.Contains(x.OrderId)).ToDictionaryAsync(x => x.OrderId, x => x, ct);
        var customerIds = approvals.Values.Select(x => x.CustomerId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var customers = customerIds.Count == 0 ? new Dictionary<Guid, Customer>() : await db.Customers.AsNoTracking().Where(x => customerIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        return Results.Ok(orders.Select(x => OrderTracking(x, approvals.TryGetValue(x.Id, out var a) ? a : null, customers)));
    }

    private static async Task<IResult> ReviewApproval(Guid id, ReviewRequest request, OFCDbContext db, IdentityService identity, IOrdersBroadcaster ordersBroadcaster, IKitchenBroadcaster kitchenBroadcaster, ClaimsPrincipal user, HttpContext httpContext, CancellationToken ct)
    {
        var approval = await db.QrOrderApprovals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (approval is null) return Results.NotFound();
        var reviewBranchId = await db.Orders.AsNoTracking().Where(x => x.Id == approval.OrderId).Select(x => x.BranchId).FirstOrDefaultAsync(ct);
        if (!await CanOperate(db, user, reviewBranchId, "qr.approve", ct) && !await CanOperate(db, user, reviewBranchId, "orders.manage", ct)) return Forbidden();
        var decision = request.Decision?.Trim().ToLowerInvariant();
        if (decision is not ("approve" or "reject")) return Validation("decision", "The review decision must be approve or reject.");
        if (!QrRules.ValidNote(request.Note)) return Validation("note", "The review note is invalid.");
        var order = await db.Orders.Include(x => x.Lines).Include(x => x.StatusHistory).SingleOrDefaultAsync(x => x.Id == approval.OrderId, ct);
        if (order is null) return Results.NotFound();
        if (approval.Status != QrOrderApprovalStatus.Pending || order.Status != OrderStatus.Pending) return Validation("approval", "Only a pending QR order awaiting approval can be reviewed.");

        var target = decision == "approve" ? QrOrderApprovalStatus.Approved : QrOrderApprovalStatus.Rejected;
        var targetOrderStatus = QrRules.ResolutionToOrderStatus(target, order.Status);
        if (!OrderRules.CanTransition(order.Status, targetOrderStatus)) return Validation("status", "The order can no longer be resolved by this review.");

        var fromOrderStatus = order.Status;
        order.Status = targetOrderStatus;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        order.StatusHistory.Add(new OrderStatusHistory { FromStatus = fromOrderStatus, ToStatus = targetOrderStatus, ChangedByUserId = UserId(user), Note = request.Note?.Trim() ?? (decision == "approve" ? "Approved" : "Rejected") });
        var trackedApproval = await db.QrOrderApprovals.SingleOrDefaultAsync(x => x.Id == approval.Id, ct);
        trackedApproval!.Status = target;
        trackedApproval.ReviewedByUserId = UserId(user);
        trackedApproval.ReviewedAt = DateTimeOffset.UtcNow;
        trackedApproval.Note = request.Note?.Trim();
        identity.Audit(UserId(user), order.BranchId, DeviceId(user), "qr.order.review", "order", order.Id.ToString(), httpContext.TraceIdentifier, JsonSerializer.Serialize(new { fromOrderStatus }), JsonSerializer.Serialize(new { target, targetOrderStatus }));
        // Approved → straight to the kitchen, same as an auto-approved order at submission time — the
        // cashier who just approved it shouldn't also have to go dispatch it by hand. Guard against tickets
        // that already exist for this order (a near-simultaneous second review, or a manual dispatch that
        // beat this one to it) — the same check the manual Dispatch endpoint uses — so a race can't double
        // up kitchen prep for the same order.
        List<KitchenTicket> kitchenTickets = [];
        if (target == QrOrderApprovalStatus.Approved)
        {
            var alreadyDispatched = await db.KitchenTickets.AsNoTracking().AnyAsync(x => x.BranchId == order.BranchId && x.OrderId == order.Id && x.DispatchStatus != KitchenDispatchStatus.Cancelled, ct);
            if (!alreadyDispatched)
            {
                var productIds = order.Lines.Select(x => x.ProductId).Distinct().ToList();
                var products = productIds.Count == 0 ? new Dictionary<Guid, Product>() : await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
                kitchenTickets = SprintTenEndpoints.BuildTickets(db, identity, order.BranchId, order, products, Guid.NewGuid(), null, null, null, UserId(user), DeviceId(user), httpContext.TraceIdentifier);
            }
        }
        await db.SaveChangesAsync(ct);
        // Realtime staff notification: this QR order was approved/rejected. Screens refresh their lists;
        // the customer's own screen picks the outcome up on its next status poll.
        await ordersBroadcaster.QrOrderReviewed(order.BranchId, order.Id, order.ClientRequestId.ToString(), order.Status.ToString(), trackedApproval.Status.ToString());
        foreach (var ticket in kitchenTickets) await kitchenBroadcaster.TicketChanged(order.BranchId, ticket.Id, "dispatched");
        return Results.Ok(OrderTracking(order, trackedApproval));
    }

    private static async Task<Guid?> ResolveCustomer(OFCDbContext db, CustomerRequest request, CancellationToken ct)
    {
        var phone = request.Phone?.Trim();
        var externalId = request.ExternalId?.Trim();
        var loyalty = request.LoyaltyReference?.Trim();
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var match = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Phone == phone, ct);
            if (match is not null) return match.Id;
        }
        if (!string.IsNullOrWhiteSpace(externalId))
        {
            var match = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.ExternalId == externalId, ct);
            if (match is not null) return match.Id;
        }
        if (!string.IsNullOrWhiteSpace(loyalty))
        {
            var match = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.LoyaltyReference == loyalty, ct);
            if (match is not null) return match.Id;
        }
        var customer = new Customer
        {
            NameAr = string.IsNullOrWhiteSpace(request.NameAr) ? null : request.NameAr.Trim(),
            NameEn = string.IsNullOrWhiteSpace(request.NameEn) ? null : request.NameEn.Trim(),
            Phone = phone,
            ExternalId = externalId,
            LoyaltyReference = loyalty,
            IsWalkIn = string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(externalId)
        };
        db.Customers.Add(customer);
        return customer.Id;
    }

    private static async Task<(QrContext Context, Branch Branch, SalesChannel Channel)?> ActiveContext(OFCDbContext db, string code, CancellationToken ct)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var ctx = await db.QrContexts.AsNoTracking().SingleOrDefaultAsync(x => x.Code == normalized && x.IsActive, ct);
        if (ctx is null) return null;
        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ctx.BranchId && x.IsActive, ct);
        if (branch is null) return null;
        var channel = await db.SalesChannels.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ctx.SalesChannelId && x.IsActive, ct);
        if (channel is null) return null;
        return (ctx, branch, channel);
    }

    private static object ContextResponse(Branch branch, SalesChannel channel, QrContext context) => new
    {
        code = context.Code,
        kind = context.Kind.ToString(),
        nameAr = context.NameAr,
        nameEn = context.NameEn,
        branchId = branch.Id,
        branchCode = branch.Code,
        branchNameAr = branch.NameAr,
        branchNameEn = branch.NameEn,
        salesChannelId = channel.Id,
        salesChannelCode = channel.Code,
        salesChannelNameAr = channel.NameAr,
        salesChannelNameEn = channel.NameEn,
        approvalMode = context.ApprovalMode.ToString(),
        requiresApproval = QrRules.RequiresStaffApproval(context.ApprovalMode)
    };

    private static object OrderTracking(Order order, QrOrderApproval? approval, Dictionary<Guid, Customer>? customers = null)
    {
        Customer? customer = null;
        if (approval?.CustomerId is not null && customers is not null) customers.TryGetValue(approval.CustomerId.Value, out customer);
        return new
        {
            order.Id,
            order.BranchId,
            order.SalesChannelId,
            order.ClientRequestId,
            source = order.Source.ToString(),
            status = order.Status.ToString(),
            order.Note,
            order.NetAmount,
            order.TaxAmount,
            order.GrossAmount,
            order.TableNumber,
            order.CreatedAt,
            order.UpdatedAt,
            lines = order.Lines.Select(x => new { x.Id, x.ProductId, x.ProductNameAr, x.ProductNameEn, x.Quantity, x.Note, x.SelectionsSnapshot, x.UnitGrossAmount, x.UnitNetAmount, x.UnitTaxAmount, x.UnitDiscountAmount }),
            history = order.StatusHistory.OrderBy(x => x.ChangedAt).Select(x => new { x.FromStatus, x.ToStatus, x.ChangedAt, x.Note }),
            approval = approval is null ? null : new { approval.Id, status = approval.Status.ToString(), approval.Note, approval.ReviewedAt, customer = customer is null ? null : new { customer.Id, customer.NameAr, customer.NameEn, customer.Phone, customer.ExternalId, customer.LoyaltyReference, customer.IsWalkIn } }
        };
    }

    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, string permission, CancellationToken ct) => user.HasClaim("permission", permission) && (user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct));
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden() => Results.Problem(statusCode: 403, title: "Forbidden", detail: "You do not have permission to perform this operation.");
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private sealed record SubmitOrderRequest(Guid ClientRequestId, string? Note, CustomerRequest? Customer, List<QrLineRequest> Lines);
    private sealed record QrLineRequest(Guid ProductId, int Quantity, string? Note, List<QrGroupSelection>? Selections);
    private sealed record QrGroupSelection(Guid SelectionGroupId, List<QrChoiceRequest> Choices);
    private sealed record QrChoiceRequest(Guid OptionId, int Quantity);
    private sealed record CustomerRequest(string? NameAr, string? NameEn, string? Phone, string? ExternalId, string? LoyaltyReference);
    private sealed record CreateContextRequest(Guid BranchId, Guid SalesChannelId, QrContextKind Kind, string Code, string NameAr, string NameEn, QrApprovalMode ApprovalMode, bool IsActive = true);
    private sealed record ReviewRequest(string Decision, string? Note);
}
