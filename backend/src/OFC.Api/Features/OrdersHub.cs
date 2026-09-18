using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;

namespace OFC.Api.Features;

// SignalR is realtime UI transport only, never the source of truth (docs/01-ARCHITECTURE-GUARDRAILS.md):
// the hub just tells connected staff screens (QR admin, cashier/POS) that a QR order event happened for
// their branch, and each screen re-fetches the authoritative order data over the existing REST endpoints.
[Authorize]
public sealed class OrdersHub(OFCDbContext db) : Hub
{
    // Any authenticated user could otherwise join another branch's group and observe its realtime QR
    // order metadata (status, gross amount) despite having no assignment there (security review
    // finding M5).
    public async Task JoinBranch(Guid branchId)
    {
        if (!await HasBranch(branchId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(branchId));
    }
    public Task LeaveBranch(Guid branchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(branchId));
    public static string GroupName(Guid branchId) => $"orders:{branchId}";
    private async Task<bool> HasBranch(Guid branchId)
    {
        var user = Context.User!;
        return user.FindFirst("branch_id")?.Value == branchId.ToString()
            || await db.UserBranches.AnyAsync(x => x.UserId == Guid.Parse(user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value) && x.BranchId == branchId, Context.ConnectionAborted);
    }
}

public interface IOrdersBroadcaster
{
    Task QrOrderReceived(Guid branchId, Guid orderId, string clientRequestId, string status, string approvalStatus, decimal grossAmount, string contextCode);
    Task QrOrderReviewed(Guid branchId, Guid orderId, string clientRequestId, string status, string approvalStatus);
}

public sealed class OrdersBroadcaster(IHubContext<OrdersHub> hub) : IOrdersBroadcaster
{
    public Task QrOrderReceived(Guid branchId, Guid orderId, string clientRequestId, string status, string approvalStatus, decimal grossAmount, string contextCode) =>
        hub.Clients.Group(OrdersHub.GroupName(branchId)).SendAsync("qrOrderReceived", new { branchId, orderId, clientRequestId, status, approvalStatus, grossAmount, contextCode });

    public Task QrOrderReviewed(Guid branchId, Guid orderId, string clientRequestId, string status, string approvalStatus) =>
        hub.Clients.Group(OrdersHub.GroupName(branchId)).SendAsync("qrOrderReviewed", new { branchId, orderId, clientRequestId, status, approvalStatus });
}
