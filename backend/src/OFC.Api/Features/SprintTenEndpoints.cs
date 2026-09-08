using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Kitchen;
using OFC.Modules.Ordering;
using OFC.Modules.Printing;

namespace OFC.Api.Features;

public static class SprintTenEndpoints
{
    public static void MapSprintTenEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/v1");
        api.MapGet("/kitchen/stations", ListStations).RequireAuthorization();
        api.MapPost("/kitchen/tickets", Dispatch).RequireAuthorization();
        api.MapGet("/kitchen/tickets", ListTickets).RequireAuthorization();
        api.MapGet("/kitchen/tickets/{id:guid}", GetTicket).RequireAuthorization();
        api.MapPost("/kitchen/tickets/{id:guid}/send", SendToKds).RequireAuthorization();
        api.MapPost("/kitchen/tickets/{id:guid}/ack", Acknowledge).RequireAuthorization();
        api.MapPost("/kitchen/tickets/{id:guid}/fallback", Fallback).RequireAuthorization();
        api.MapPost("/kitchen/tickets/{id:guid}/printed", MarkPrintedFallback).RequireAuthorization();
        api.MapPost("/kitchen/tickets/{id:guid}/fail", Fail).RequireAuthorization();
        api.MapPut("/kitchen/tickets/{id:guid}/item/{itemId:guid}", SetItemStatus).RequireAuthorization();
        api.MapPost("/kitchen/tickets/{id:guid}/cancel", Cancel).RequireAuthorization();
    }

    private static async Task<IResult> ListStations(OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!user.HasClaim("permission", "kitchen.view")) return Forbidden();
        return Results.Ok(await db.PreparationStations.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code).Select(x => new { x.Id, x.Code, x.NameAr, x.NameEn }).ToListAsync(ct));
    }

    private static async Task<IResult> Dispatch(DispatchRequest request, OFCDbContext db, IdentityService identity, IKitchenBroadcaster broadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        if (!await CanOperate(db, user, request.BranchId, ct, "kitchen.manage") && !await CanOperate(db, user, request.BranchId, ct, "orders.manage")) return Forbidden();
        if (request.OrderId == Guid.Empty || request.ClientDispatchId == Guid.Empty) return Validation("request", "Provide an order id and a deterministic client dispatch id.");
        if (request.Note?.Trim().Length > KitchenRules.NoteMax || !KitchenRules.ValidTargetMinutes(request.TargetMinutes)) return Validation("request", "The note or target preparation time is invalid.");
        var order = await db.Orders.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == request.OrderId && x.BranchId == request.BranchId, ct);
        if (order is null) return Results.NotFound();
        if (order.Status is not (OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Paid or OrderStatus.SentToKitchen or OrderStatus.Preparing or OrderStatus.Ready)) return Validation("order", "Only an active submitted order can be dispatched to the kitchen.");

        var existing = await db.KitchenTickets.AsNoTracking().Include(x => x.Items).Where(x => x.BranchId == request.BranchId && x.OrderId == order.Id && x.DispatchStatus != KitchenDispatchStatus.Cancelled).ToListAsync(ct);
        if (existing.Count > 0) return Results.Ok(existing.Select(x => TicketResponse(x, null)));

        var productIds = order.Lines.Select(x => x.ProductId).Distinct().ToList();
        var products = productIds.Count == 0 ? new Dictionary<Guid, Product>() : await db.Products.AsNoTracking().Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var orderNumber = string.IsNullOrWhiteSpace(request.OrderNumber) ? order.Id.ToString("N")[..8].ToUpperInvariant() : request.OrderNumber.Trim();
        var at = DateTimeOffset.UtcNow;
        var created = new List<KitchenTicket>();
        foreach (var group in order.Lines.Where(x => x.VoidedQuantity < x.Quantity).GroupBy(x => products.TryGetValue(x.ProductId, out var product) ? product.PreparationStationId : null))
        {
            if (created.Count >= KitchenRules.MaxItemsPerTicket) break;
            var ticket = new KitchenTicket { BranchId = request.BranchId, OrderId = order.Id, DispatchId = request.ClientDispatchId, OrderNumber = orderNumber, StationId = group.Key, TargetMinutes = request.TargetMinutes, Note = request.Note?.Trim(), CreatedByUserId = UserId(user), DeviceId = DeviceId(user) };
            foreach (var line in group)
            {
                var product = products.TryGetValue(line.ProductId, out var found) ? found : null;
                ticket.Items.Add(new KitchenTicketItem { OrderLineId = line.Id, ProductId = line.ProductId, ProductNameAr = product is null ? line.ProductNameAr : product.NameAr, ProductNameEn = product is null ? line.ProductNameEn : product.NameEn, Quantity = line.Quantity - line.VoidedQuantity, Note = line.Note?.Trim(), SelectionsSnapshot = line.SelectionsSnapshot });
            }
            if (ticket.Items.Count == 0) continue;
            db.KitchenTickets.Add(ticket);
            created.Add(ticket);
            identity.Audit(UserId(user), request.BranchId, DeviceId(user), "kitchen.ticket.dispatch", "kitchen_ticket", ticket.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ticket.OrderId, ticket.DispatchId, ticket.StationId, itemCount = ticket.Items.Count }));
        }
        if (created.Count == 0) return Results.Ok(Array.Empty<object>());
        await db.SaveChangesAsync(ct);
        foreach (var ticket in created) await broadcaster.TicketChanged(request.BranchId, ticket.Id, "dispatched");
        return Results.Created($"/api/v1/kitchen/tickets/{created[0].Id}", created.Select(x => TicketResponse(x, null)).ToList());
    }

    private static async Task<IResult> ListTickets(Guid branchId, Guid? stationId, string? status, bool? activeOnly, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        if (!await CanOperate(db, user, branchId, ct, "kitchen.view")) return Forbidden();
        IQueryable<KitchenTicket> query = db.KitchenTickets.AsNoTracking().Where(x => x.BranchId == branchId).Include(x => x.Items);
        if (stationId.HasValue) query = query.Where(x => x.StationId == stationId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<KitchenDispatchStatus>(status, ignoreCase: true, out var parsed)) return Validation("status", "The kitchen dispatch status is invalid.");
            query = query.Where(x => x.DispatchStatus == parsed);
        }
        if (activeOnly != false) query = query.Where(x => x.DispatchStatus != KitchenDispatchStatus.Cancelled && x.DispatchStatus != KitchenDispatchStatus.Failed && x.Status != KitchenTicketStatus.Completed);
        var tickets = await query.OrderBy(x => x.CreatedAt).Take(200).ToListAsync(ct);
        var stationIds = tickets.Select(x => x.StationId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var stations = stationIds.Count == 0 ? new Dictionary<Guid, PreparationStation>() : await db.PreparationStations.AsNoTracking().Where(x => stationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x, ct);
        var now = DateTimeOffset.UtcNow;
        return Results.Ok(tickets.Select(x => TicketResponse(x, stations.TryGetValue(x.StationId ?? Guid.Empty, out var station) ? station : null, now)));
    }

    private static async Task<IResult> GetTicket(Guid id, OFCDbContext db, ClaimsPrincipal user, CancellationToken ct)
    {
        var ticket = await db.KitchenTickets.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return Results.NotFound();
        if (!await CanOperate(db, user, ticket.BranchId, ct, "kitchen.view")) return Forbidden();
        var station = ticket.StationId is Guid sid ? await db.PreparationStations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sid, ct) : null;
        return Results.Ok(TicketResponse(ticket, station, DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> SendToKds(Guid id, OFCDbContext db, IdentityService identity, IKitchenBroadcaster broadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var ticket = await db.KitchenTickets.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return Results.NotFound();
        if (!await CanOperate(db, user, ticket.BranchId, ct, "kitchen.manage")) return Forbidden();
        if (!KitchenRules.CanDispatchTransition(ticket.DispatchStatus, KitchenDispatchStatus.SentToKds)) return Validation("ticket", "This ticket cannot be sent to the KDS in its current state.");
        ticket.DispatchStatus = KitchenDispatchStatus.SentToKds; ticket.KdsAttempts += 1; ticket.UpdatedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), ticket.BranchId, DeviceId(user), "kitchen.ticket.send", "kitchen_ticket", ticket.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ticket.DispatchStatus, ticket.KdsAttempts }));
        await db.SaveChangesAsync(ct);
        await broadcaster.TicketChanged(ticket.BranchId, ticket.Id, "sent");
        return Results.Ok(TicketResponse(ticket, await Station(db, ticket, ct), DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> Acknowledge(Guid id, OFCDbContext db, IdentityService identity, IKitchenBroadcaster broadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var ticket = await db.KitchenTickets.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return Results.NotFound();
        if (!await CanOperate(db, user, ticket.BranchId, ct, "kitchen.acknowledge")) return Forbidden();
        if (ticket.DispatchStatus is not (KitchenDispatchStatus.SentToKds or KitchenDispatchStatus.PrintedFallback)) return Validation("ticket", "Only a ticket dispatched to the KDS or already printed as a fallback can be acknowledged.");
        var now = DateTimeOffset.UtcNow;
        ticket.DispatchStatus = KitchenDispatchStatus.KdsAcknowledged; ticket.AcknowledgedAt = now; ticket.Status = KitchenTicketStatus.Preparing; ticket.StartedAt ??= now; ticket.UpdatedAt = now;
        foreach (var item in ticket.Items.Where(x => x.Status == KitchenItemStatus.New))
        {
            item.Status = KitchenItemStatus.Preparing; item.StartedAt = now;
        }
        identity.Audit(UserId(user), ticket.BranchId, DeviceId(user), "kitchen.ticket.ack", "kitchen_ticket", ticket.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ticket.DispatchStatus, ticket.AcknowledgedAt, ticket.Status }));
        await db.SaveChangesAsync(ct);
        await broadcaster.TicketChanged(ticket.BranchId, ticket.Id, "acknowledged");
        return Results.Ok(TicketResponse(ticket, await Station(db, ticket, ct), now));
    }

    private static async Task<IResult> Fallback(Guid id, FallbackRequest request, OFCDbContext db, IdentityService identity, IKitchenBroadcaster broadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var ticket = await db.KitchenTickets.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return Results.NotFound();
        if (!await CanOperate(db, user, ticket.BranchId, ct, "kitchen.manage")) return Forbidden();
        if (request.Error?.Trim().Length > KitchenRules.ErrorMax) return Validation("error", "The fallback reason is too long.");
        var (ok, existingJob) = await ApplyFallback(db, ticket, request.Error?.Trim(), request.TemplateCode?.Trim(), request.Manual == true ? KitchenExecutionChannel.ManualFallback : KitchenExecutionChannel.PrintFallback, DeviceId(user), UserId(user), ct);
        if (!ok) return Validation("ticket", "This ticket cannot fall back to a kitchen printer in its current state.");
        identity.Audit(UserId(user), ticket.BranchId, DeviceId(user), "kitchen.ticket.fallback", "kitchen_ticket", ticket.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ticket.DispatchStatus, ticket.Channel, ticket.KdsAttempts, jobClientRequestId = ticket.DispatchId, duplicatePrint = existingJob }));
        await db.SaveChangesAsync(ct);
        await broadcaster.TicketChanged(ticket.BranchId, ticket.Id, "fallback");
        return Results.Ok(new { ticket = TicketResponse(ticket, await Station(db, ticket, ct), DateTimeOffset.UtcNow), duplicatePrint = existingJob });
    }

    private static async Task<IResult> MarkPrintedFallback(Guid id, OFCDbContext db, IdentityService identity, IKitchenBroadcaster broadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var ticket = await db.KitchenTickets.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return Results.NotFound();
        if (!await CanOperate(db, user, ticket.BranchId, ct, "kitchen.manage")) return Forbidden();
        if (!KitchenRules.CanDispatchTransition(ticket.DispatchStatus, KitchenDispatchStatus.PrintedFallback)) return Validation("ticket", "Only a fallback pending ticket can be marked as printed.");
        var now = DateTimeOffset.UtcNow;
        ticket.DispatchStatus = KitchenDispatchStatus.PrintedFallback; ticket.FallbackPrinted = true; ticket.FallbackPrintedAt = now; ticket.Status = KitchenTicketStatus.Preparing; ticket.StartedAt ??= now; ticket.UpdatedAt = now;
        identity.Audit(UserId(user), ticket.BranchId, DeviceId(user), "kitchen.ticket.printed-fallback", "kitchen_ticket", ticket.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ticket.DispatchStatus, ticket.FallbackPrintedAt }));
        await db.SaveChangesAsync(ct);
        await broadcaster.TicketChanged(ticket.BranchId, ticket.Id, "printed-fallback");
        return Results.Ok(TicketResponse(ticket, await Station(db, ticket, ct), now));
    }

    private static async Task<IResult> Fail(Guid id, FailRequest request, OFCDbContext db, IdentityService identity, IKitchenBroadcaster broadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var ticket = await db.KitchenTickets.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return Results.NotFound();
        if (!await CanOperate(db, user, ticket.BranchId, ct, "kitchen.manage")) return Forbidden();
        if (ticket.DispatchStatus is not (KitchenDispatchStatus.PrintFallbackPending or KitchenDispatchStatus.SentToKds)) return Validation("ticket", "Only a ticket awaiting dispatch or a pending fallback can be reported as failed.");
        if (request.Error?.Trim().Length > KitchenRules.ErrorMax) return Validation("error", "The failure reason is too long.");
        ticket.DispatchStatus = KitchenDispatchStatus.Failed; ticket.LastError = request.Error?.Trim(); ticket.UpdatedAt = DateTimeOffset.UtcNow;
        identity.Audit(UserId(user), ticket.BranchId, DeviceId(user), "kitchen.ticket.fail", "kitchen_ticket", ticket.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ticket.DispatchStatus, ticket.LastError }));
        await db.SaveChangesAsync(ct);
        await broadcaster.TicketChanged(ticket.BranchId, ticket.Id, "failed");
        return Results.Ok(TicketResponse(ticket, await Station(db, ticket, ct), DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> SetItemStatus(Guid id, Guid itemId, ItemStatusRequest request, OFCDbContext db, IdentityService identity, IKitchenBroadcaster broadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var ticket = await db.KitchenTickets.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return Results.NotFound();
        if (!await CanOperate(db, user, ticket.BranchId, ct, "kitchen.acknowledge")) return Forbidden();
        var item = ticket.Items.SingleOrDefault(x => x.Id == itemId);
        if (item is null) return Results.NotFound();
        if (ticket.Status is KitchenTicketStatus.Cancelled) return Validation("ticket", "A cancelled kitchen ticket cannot update item status.");
        if (!KitchenRules.CanItemTransition(item.Status, request.Status)) return Validation("status", "This item status transition is invalid.");
        var now = DateTimeOffset.UtcNow;
        item.Status = request.Status;
        if (request.Status == KitchenItemStatus.Preparing) item.StartedAt ??= now;
        if (request.Status == KitchenItemStatus.Ready) item.ReadyAt ??= now;
        if (request.Status == KitchenItemStatus.Completed) item.CompletedAt ??= now;
        ApplyTicketProgress(ticket, now);
        identity.Audit(UserId(user), ticket.BranchId, DeviceId(user), "kitchen.ticket.item.update", "kitchen_ticket_item", item.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ticketId = ticket.Id, itemStatus = item.Status }));
        await db.SaveChangesAsync(ct);
        await broadcaster.TicketChanged(ticket.BranchId, ticket.Id, "item-updated");
        return Results.Ok(TicketResponse(ticket, await Station(db, ticket, ct), now));
    }

    private static async Task<IResult> Cancel(Guid id, CancelRequest request, OFCDbContext db, IdentityService identity, IKitchenBroadcaster broadcaster, ClaimsPrincipal user, HttpContext context, CancellationToken ct)
    {
        var ticket = await db.KitchenTickets.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (ticket is null) return Results.NotFound();
        if (!await CanOperate(db, user, ticket.BranchId, ct, "kitchen.cancel")) return Forbidden();
        if (!KitchenRules.CanDispatchTransition(ticket.DispatchStatus, KitchenDispatchStatus.Cancelled)) return Validation("ticket", "This ticket cannot be cancelled in its current state.");
        if (request.Note?.Trim().Length > KitchenRules.NoteMax) return Validation("note", "The cancellation note is too long.");
        var now = DateTimeOffset.UtcNow;
        ticket.WasPrepStartedBeforeCancellation = ticket.Status != KitchenTicketStatus.New || ticket.StartedAt.HasValue;
        ticket.DispatchStatus = KitchenDispatchStatus.Cancelled; ticket.Status = KitchenTicketStatus.Cancelled; ticket.CancelledAt = now; ticket.Note = request.Note?.Trim(); ticket.UpdatedAt = now;
        foreach (var item in ticket.Items.Where(x => x.Status is not (KitchenItemStatus.Completed or KitchenItemStatus.Cancelled))) item.Status = KitchenItemStatus.Cancelled;
        // A cancelled ticket must alert whoever is preparing it even when nothing has printed yet: a
        // KDS-only ticket previously got no notification at all (audit finding), so it now falls back
        // to a printed alert exactly like an already-printed ticket does.
        if (!ticket.CancellationNotified)
        {
            ticket.CancellationNotified = true;
            var alert = new PrintJob { BranchId = ticket.BranchId, DeviceId = DeviceId(user), OrderId = ticket.OrderId, ClientRequestId = Guid.CreateVersion7(), Kind = PrintJobKind.Kitchen, Status = PrintJobStatus.Pending, TemplateCode = request.TemplateCode?.Trim(), Payload = JsonSerializer.Serialize(CancellationPayload(ticket, request.Note?.Trim())), CreatedByUserId = UserId(user) };
            db.PrintJobs.Add(alert);
        }
        identity.Audit(UserId(user), ticket.BranchId, DeviceId(user), "kitchen.ticket.cancel", "kitchen_ticket", ticket.Id.ToString(), context.TraceIdentifier, newValue: JsonSerializer.Serialize(new { ticket.DispatchStatus, ticket.Status, ticket.WasPrepStartedBeforeCancellation, ticket.CancellationNotified }));
        await db.SaveChangesAsync(ct);
        await broadcaster.TicketChanged(ticket.BranchId, ticket.Id, "cancelled");
        return Results.Ok(TicketResponse(ticket, await Station(db, ticket, ct), now));
    }

    private static void ApplyTicketProgress(KitchenTicket ticket, DateTimeOffset now)
    {
        var items = ticket.Items.Where(x => x.Status != KitchenItemStatus.Cancelled).ToList();
        if (items.Count == 0)
        {
            ticket.Status = KitchenTicketStatus.Cancelled; ticket.CancelledAt ??= now;
        }
        else if (items.All(x => x.Status == KitchenItemStatus.Completed))
        {
            ticket.Status = KitchenTicketStatus.Completed; ticket.CompletedAt ??= now;
        }
        else if (items.All(x => x.Status == KitchenItemStatus.Ready))
        {
            ticket.Status = KitchenTicketStatus.Ready; ticket.ReadyAt ??= now;
        }
        else if (items.Any(x => x.Status is KitchenItemStatus.Preparing or KitchenItemStatus.Ready))
        {
            ticket.Status = KitchenTicketStatus.Preparing; ticket.StartedAt ??= now;
        }
        else
        {
            ticket.Status = KitchenTicketStatus.New;
        }
        ticket.UpdatedAt = now;
    }

    // Shared by the manual "Print fallback" action and KitchenFallbackWatcher's automatic trigger, so an
    // unacknowledged ticket falls back the same way whether a human notices or the system notices first.
    internal static async Task<(bool Ok, bool DuplicatePrint)> ApplyFallback(OFCDbContext db, KitchenTicket ticket, string? error, string? templateCode, KitchenExecutionChannel channel, Guid? deviceId, Guid createdByUserId, CancellationToken ct)
    {
        if (!KitchenRules.CanDispatchTransition(ticket.DispatchStatus, KitchenDispatchStatus.PrintFallbackPending)) return (false, false);
        var route = await ResolveRoute(db, ticket, ct);
        var payload = FallbackPayload(ticket, error);
        var existingJob = await db.PrintJobs.AsNoTracking().AnyAsync(x => x.BranchId == ticket.BranchId && x.ClientRequestId == ticket.DispatchId && x.Kind == PrintJobKind.Kitchen, ct);
        if (!existingJob)
        {
            var job = new PrintJob { BranchId = ticket.BranchId, DeviceId = deviceId, OrderId = ticket.OrderId, ClientRequestId = ticket.DispatchId, Kind = PrintJobKind.Kitchen, Status = PrintJobStatus.Pending, PrinterConfigurationId = route?.PrinterConfigurationId, TemplateCode = route is null ? templateCode : null, Payload = JsonSerializer.Serialize(payload), CreatedByUserId = createdByUserId };
            db.PrintJobs.Add(job);
        }
        ticket.DispatchStatus = KitchenDispatchStatus.PrintFallbackPending; ticket.Channel = channel; ticket.KdsAttempts += 1; ticket.LastError = error; ticket.UpdatedAt = DateTimeOffset.UtcNow;
        return (true, existingJob);
    }

    private static async Task<PreparationStation?> Station(OFCDbContext db, KitchenTicket ticket, CancellationToken ct) => ticket.StationId is Guid sid ? await db.PreparationStations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sid, ct) : null;

    private static async Task<PrinterRoute?> ResolveRoute(OFCDbContext db, KitchenTicket ticket, CancellationToken ct)
    {
        var routes = await db.PrinterRoutes.AsNoTracking().Where(x => x.BranchId == ticket.BranchId).ToListAsync(ct);
        var route = PrintingRules.PickRoute(routes, ticket.StationId);
        if (route is null) return null;
        if (await db.PrintTemplates.AsNoTracking().AnyAsync(x => x.Id == route.PrintTemplateId, ct)) return route;
        return null;
    }

    private static object FallbackPayload(KitchenTicket ticket, string? reason) => new
    {
        marker = "FALLBACK",
        fallback = true,
        orderNumber = ticket.OrderNumber,
        orderId = ticket.OrderId,
        createdAt = ticket.CreatedAt,
        stationId = ticket.StationId,
        dispatchId = ticket.DispatchId,
        reason,
        items = ticket.Items.Select(x => new { x.ProductNameAr, x.ProductNameEn, x.Quantity, x.Note, selections = x.SelectionsSnapshot }),
        cancellation = ticket.Status == KitchenTicketStatus.Cancelled
    };

    private static object CancellationPayload(KitchenTicket ticket, string? note) => new
    {
        marker = "CANCELLED",
        fallback = true,
        orderNumber = ticket.OrderNumber,
        orderId = ticket.OrderId,
        stationId = ticket.StationId,
        cancelledAt = ticket.CancelledAt,
        prepStarted = ticket.WasPrepStartedBeforeCancellation,
        note,
        items = ticket.Items.Select(x => new { x.ProductNameAr, x.ProductNameEn, x.Quantity })
    };

    private static object TicketResponse(KitchenTicket ticket, PreparationStation? station, DateTimeOffset? now = null) => new
    {
        ticket.Id, ticket.BranchId, ticket.OrderId, ticket.DispatchId, ticket.OrderNumber, ticket.StationId,
        stationCode = station?.Code, stationNameAr = station?.NameAr, stationNameEn = station?.NameEn,
        dispatchStatus = ticket.DispatchStatus.ToString(), channel = ticket.Channel.ToString(), status = ticket.Status.ToString(),
        ticket.TargetMinutes, ticket.KdsAttempts, ticket.FallbackPrinted, ticket.LastError, ticket.Note,
        ticket.WasPrepStartedBeforeCancellation, ticket.CancellationNotified, ticket.CreatedByUserId, ticket.DeviceId,
        createdAt = ticket.CreatedAt, ticket.StartedAt, ticket.ReadyAt, ticket.CompletedAt, ticket.CancelledAt, ticket.AcknowledgedAt, ticket.FallbackPrintedAt, ticket.UpdatedAt,
        overdue = now is not null && KitchenRules.IsOverdue(ticket, now.Value),
        items = ticket.Items.OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.OrderLineId, x.ProductId, x.ProductNameAr, x.ProductNameEn, x.Quantity, x.VoidedQuantity, x.Note, selections = x.SelectionsSnapshot, status = x.Status.ToString(), x.StartedAt, x.ReadyAt, x.CompletedAt, x.CreatedAt })
    };

    private static async Task<bool> CanOperate(OFCDbContext db, ClaimsPrincipal user, Guid branchId, CancellationToken ct, string permission) => user.HasClaim("permission", permission) && (user.FindFirstValue("branch_id") == branchId.ToString() || await db.UserBranches.AnyAsync(x => x.UserId == UserId(user) && x.BranchId == branchId, ct));
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static Guid? DeviceId(ClaimsPrincipal user) => Guid.TryParse(user.FindFirstValue("device_id"), out var id) ? id : null;
    private static IResult Forbidden(string detail = "You do not have permission to perform this operation.") => Results.Problem(statusCode: 403, title: "Forbidden", detail: detail);
    private static IResult Validation(string field, string detail) => Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [detail] });

    private sealed record DispatchRequest(Guid BranchId, Guid OrderId, Guid ClientDispatchId, string? OrderNumber, string? Note, int? TargetMinutes);
    private sealed record FallbackRequest(string? Error, bool? Manual, string? TemplateCode);
    private sealed record FailRequest(string? Error);
    private sealed record ItemStatusRequest(KitchenItemStatus Status);
    private sealed record CancelRequest(string? Note, string? TemplateCode);
}
