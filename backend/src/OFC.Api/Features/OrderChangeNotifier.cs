using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OFC.Modules.Ordering;
using OFC.Modules.Payments;

namespace OFC.Api.Features;

// Staff screens (current orders) must refresh whenever any order in their branch changes — created on
// another till, paid, sent to the kitchen, marked ready, cancelled. Rather than every endpoint remembering
// to broadcast, this interceptor watches every successful save and sends one "orderChanged" per affected
// branch. Like the rest of SignalR here it is a refresh hint only; screens re-fetch over REST.
public sealed class OrderChangeNotifier(IHubContext<OrdersHub> hub) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, HashSet<Guid>> pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Publish(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Publish(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Discard(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        var branches = new HashSet<Guid>();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.Entity is Order order) branches.Add(order.BranchId);
            else if (entry.Entity is Payment payment) branches.Add(payment.BranchId);
        }
        if (branches.Count > 0) pending.AddOrUpdate(context, branches);
        else pending.Remove(context);
    }

    private void Publish(DbContext? context)
    {
        if (context is null || !pending.TryGetValue(context, out var branches)) return;
        pending.Remove(context);
        // Fire-and-forget: a slow or failed broadcast must never fail or delay the save that already committed.
        foreach (var branchId in branches)
            _ = hub.Clients.Group(OrdersHub.GroupName(branchId)).SendAsync("orderChanged", new { branchId });
    }

    private void Discard(DbContext? context)
    {
        if (context is not null) pending.Remove(context);
    }
}
