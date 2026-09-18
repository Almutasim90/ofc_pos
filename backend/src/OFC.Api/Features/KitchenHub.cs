using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;

namespace OFC.Api.Features;

// SignalR is realtime UI transport only, never the source of truth (docs/01-ARCHITECTURE-GUARDRAILS.md):
// the hub tells a connected KDS screen that something changed for its branch, and the screen re-fetches
// the authoritative ticket list over the existing REST endpoint. The hub never carries ticket state itself.
[Authorize]
public sealed class KitchenHub(OFCDbContext db) : Hub
{
    // Any authenticated user could otherwise join another branch's group and observe its realtime
    // ticket metadata (id, reason) despite having no assignment there (security review finding M5).
    public async Task JoinBranch(Guid branchId)
    {
        if (!await HasBranch(branchId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(branchId));
    }
    public Task LeaveBranch(Guid branchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(branchId));
    public static string GroupName(Guid branchId) => $"kitchen:{branchId}";
    private async Task<bool> HasBranch(Guid branchId)
    {
        var user = Context.User!;
        return user.FindFirst("branch_id")?.Value == branchId.ToString()
            || await db.UserBranches.AnyAsync(x => x.UserId == Guid.Parse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value) && x.BranchId == branchId, Context.ConnectionAborted);
    }
}

public interface IKitchenBroadcaster
{
    Task TicketChanged(Guid branchId, Guid ticketId, string reason);
}

public sealed class KitchenBroadcaster(IHubContext<KitchenHub> hub) : IKitchenBroadcaster
{
    public Task TicketChanged(Guid branchId, Guid ticketId, string reason) =>
        hub.Clients.Group(KitchenHub.GroupName(branchId)).SendAsync("ticketChanged", new { branchId, ticketId, reason });
}
