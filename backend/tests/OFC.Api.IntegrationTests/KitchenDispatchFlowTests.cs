using System.Linq;
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
}
