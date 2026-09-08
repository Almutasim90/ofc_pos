using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OFC.Api.Features;

// SignalR is realtime UI transport only, never the source of truth (docs/01-ARCHITECTURE-GUARDRAILS.md):
// the hub tells a connected KDS screen that something changed for its branch, and the screen re-fetches
// the authoritative ticket list over the existing REST endpoint. The hub never carries ticket state itself.
[Authorize]
public sealed class KitchenHub : Hub
{
    public Task JoinBranch(Guid branchId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(branchId));
    public Task LeaveBranch(Guid branchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(branchId));
    public static string GroupName(Guid branchId) => $"kitchen:{branchId}";
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
