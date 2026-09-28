using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OFC.Api.Features;
using OFC.Infrastructure.Persistence;
using OFC.Infrastructure.Security;
using OFC.Modules.Catalog;
using OFC.Modules.Kitchen;
using OFC.Modules.Ordering;
using Xunit;

namespace OFC.Api.IntegrationTests;

// Kitchen flow for a restaurant whose only kitchen device is a tablet running the KDS page: a dispatched
// ticket lands on the screen immediately (no manual "send" step), and without a configured kitchen
// printer the unacknowledged-ticket watcher must not queue print jobs nobody can print.
public class KitchenDispatchFlowTests
{
    [Fact]
    public void Dispatched_tickets_go_straight_to_the_kitchen_screen()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityService>();
        var order = new Order { BranchId = Guid.NewGuid(), SalesChannelId = Guid.NewGuid(), Number = 7, Status = OrderStatus.Paid };
        order.Lines.Add(new OrderLine { OrderId = order.Id, ProductId = Guid.NewGuid(), ProductNameAr = "وجبة", ProductNameEn = "Meal", Quantity = 2 });

        var tickets = SprintTenEndpoints.BuildTickets(db, identity, order.BranchId, order, new Dictionary<Guid, Product>(), Guid.NewGuid(), null, null, null, null, null, "test");

        var ticket = Assert.Single(tickets);
        Assert.Equal(KitchenDispatchStatus.SentToKds, ticket.DispatchStatus);
        Assert.Equal(1, ticket.KdsAttempts);
        Assert.Equal("7", ticket.OrderNumber);
    }

    // The kitchen receives one order as one ticket, even when its items are prepared at different stations.
    [Fact]
    public void An_order_spanning_stations_is_one_ticket()
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityService>();
        var order = new Order { BranchId = Guid.NewGuid(), SalesChannelId = Guid.NewGuid(), Number = 8, Status = OrderStatus.Paid };
        var grill = new Product { Sku = "G", NameAr = "مشاوي", NameEn = "Grill", CategoryId = Guid.NewGuid(), PreparationStationId = Guid.NewGuid() };
        var drink = new Product { Sku = "D", NameAr = "عصير", NameEn = "Juice", CategoryId = Guid.NewGuid(), PreparationStationId = Guid.NewGuid() };
        order.Lines.Add(new OrderLine { OrderId = order.Id, ProductId = grill.Id, ProductNameAr = grill.NameAr, ProductNameEn = grill.NameEn, Quantity = 1 });
        order.Lines.Add(new OrderLine { OrderId = order.Id, ProductId = drink.Id, ProductNameAr = drink.NameAr, ProductNameEn = drink.NameEn, Quantity = 2 });

        var tickets = SprintTenEndpoints.BuildTickets(db, identity, order.BranchId, order, new Dictionary<Guid, Product> { [grill.Id] = grill, [drink.Id] = drink }, Guid.NewGuid(), null, null, null, null, null, "test");

        var ticket = Assert.Single(tickets);
        Assert.Null(ticket.StationId);
        Assert.Equal(2, ticket.Items.Count);
    }

    [Fact]
    public async Task Unacknowledged_ticket_without_a_kitchen_printer_is_not_turned_into_a_print_job()
    {
        using var factory = new ApiFactory();
        var branchId = Guid.NewGuid();
        Guid ticketId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
            var ticket = new KitchenTicket { BranchId = branchId, OrderId = Guid.NewGuid(), OrderNumber = "12", DispatchStatus = KitchenDispatchStatus.SentToKds, KdsAttempts = 1, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2) };
            ticket.Items.Add(new KitchenTicketItem { KitchenTicketId = ticket.Id, ProductId = Guid.NewGuid(), ProductNameAr = "وجبة", ProductNameEn = "Meal", Quantity = 1 });
            db.KitchenTickets.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
        }

        // The watcher polls every 5 seconds; give it more than one sweep.
        await Task.Delay(TimeSpan.FromSeconds(12));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
            Assert.Equal(KitchenDispatchStatus.SentToKds, db.KitchenTickets.Single(x => x.Id == ticketId).DispatchStatus);
            Assert.False(db.PrintJobs.Any(x => x.BranchId == branchId));
        }
    }

    // Regression: dispatch used to fail every time with DbUpdateConcurrencyException because the order's
    // new status-history row was taken for an existing one (POS showed "Kitchen dispatch failed").
    [Fact]
    public async Task Paid_order_dispatches_through_the_api_and_the_kitchen_can_acknowledge_it()
    {
        using var factory = new ApiFactory();
        var client = factory.AnonymousClient();
        await client.PostAsJsonAsync("/api/v1/auth/bootstrap", new { organizationNameAr = "منظمة", organizationNameEn = "Org", branchNameAr = "الفرع", branchNameEn = "Branch", username = "kds-api-admin", displayName = "Admin", password = "password1234" });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "kds-api-admin", password = "password1234", branchId = (Guid?)null, deviceId = (Guid?)null });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString());
        var branchId = (await (await client.GetAsync("/api/v1/branches")).Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetGuid();
        var channelId = (await (await client.PostAsJsonAsync("/api/v1/sales-channels", new { code = "POS", nameAr = "الصالة", nameEn = "Dine-in", isActive = true })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var categoryId = (await (await client.PostAsJsonAsync("/api/v1/categories", new { nameAr = "وجبات", nameEn = "Meals", parentId = (Guid?)null, sortOrder = 0, imageUrl = (string?)null })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var productId = (await (await client.PostAsJsonAsync("/api/v1/products", new { sku = "KDS-API-1", barcode = (string?)null, nameAr = "وجبة", nameEn = "Meal", descriptionAr = (string?)null, descriptionEn = (string?)null, categoryId, type = "Simple", taxCategoryId = (Guid?)null, preparationStationId = (Guid?)null, basePrice = 2m, isActive = true, images = Array.Empty<object>(), availability = new[] { new { branchId, isAvailable = true } } })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var orderId = (await (await client.PostAsJsonAsync("/api/v1/orders", new { branchId, salesChannelId = channelId, clientRequestId = Guid.NewGuid(), source = "Pos", note = (string?)null, lines = new[] { new { productId, quantity = 1, note = (string?)null, selections = Array.Empty<object>() } } })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/v1/orders/{orderId}/status", new { status = "Pending", note = (string?)null })).EnsureSuccessStatusCode();

        var dispatch = await client.PostAsJsonAsync("/api/v1/kitchen/tickets", new { branchId, orderId, clientDispatchId = Guid.NewGuid(), orderNumber = (string?)null, note = (string?)null, targetMinutes = (int?)null });
        Assert.True(dispatch.StatusCode == HttpStatusCode.Created, await dispatch.Content.ReadAsStringAsync());
        var ticket = (await dispatch.Content.ReadFromJsonAsync<JsonElement>())[0];
        Assert.Equal("SentToKds", ticket.GetProperty("dispatchStatus").GetString());

        var ack = await client.PostAsync($"/api/v1/kitchen/tickets/{ticket.GetProperty("id").GetGuid()}/ack", null);
        Assert.True(ack.StatusCode == HttpStatusCode.OK, await ack.Content.ReadAsStringAsync());
        var order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Preparing", order.GetProperty("status").GetString());

        // The kitchen screen shows the order type, and one tap marks the whole order ready.
        var listed = (await (await client.GetAsync($"/api/v1/kitchen/tickets?branchId={branchId}")).Content.ReadFromJsonAsync<JsonElement>())[0];
        Assert.Equal("Dine-in", listed.GetProperty("orderTypeNameEn").GetString());
        var ready = await client.PostAsync($"/api/v1/kitchen/tickets/{ticket.GetProperty("id").GetGuid()}/ready", null);
        Assert.True(ready.StatusCode == HttpStatusCode.OK, await ready.Content.ReadAsStringAsync());
        Assert.Equal("Ready", (await ready.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        order = await (await client.GetAsync($"/api/v1/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ready", order.GetProperty("status").GetString());
    }
}
