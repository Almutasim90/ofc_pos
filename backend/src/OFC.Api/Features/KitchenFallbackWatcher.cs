using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Modules.Kitchen;

namespace OFC.Api.Features;

// KitchenRules.ShouldTriggerFallback existed and was unit-tested but nothing ever called it (Sprint 10
// audit finding): a ticket sent to the KDS that never gets acknowledged just sat there until a human
// noticed and clicked "Print fallback". This polls for exactly that condition and applies the same
// ApplyFallback path the manual button uses, so an unattended KDS still gets the order to the kitchen.
public sealed class KitchenFallbackWatcher(IServiceScopeFactory scopeFactory, IKitchenBroadcaster broadcaster, ILogger<KitchenFallbackWatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await Sweep(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Kitchen fallback sweep failed."); }
        }
    }

    private async Task Sweep(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OFCDbContext>();
        var now = DateTimeOffset.UtcNow;
        // A ticket that reached the KDS but was never acknowledged has, by definition, no live KDS
        // screen watching it right now — the same "kdsAvailable: false" input the rule was designed for.
        var candidates = await db.KitchenTickets.Include(x => x.Items)
            .Where(x => x.DispatchStatus == KitchenDispatchStatus.SentToKds)
            .ToListAsync(ct);
        foreach (var ticket in candidates)
        {
            if (!KitchenRules.ShouldTriggerFallback(kdsAvailable: false, ticket.KdsAttempts, ticket.CreatedAt, now)) continue;
            var (ok, _) = await SprintTenEndpoints.ApplyFallback(db, ticket, "KDS did not acknowledge in time", null, KitchenExecutionChannel.PrintFallback, null, Guid.Empty, ct);
            if (!ok) continue;
            await db.SaveChangesAsync(ct);
            await broadcaster.TicketChanged(ticket.BranchId, ticket.Id, "auto-fallback");
            logger.LogInformation("Kitchen ticket {TicketId} auto-fell-back to printing after {Attempts} attempt(s).", ticket.Id, ticket.KdsAttempts);
        }
    }
}
