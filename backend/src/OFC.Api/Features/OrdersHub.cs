using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OFC.Api.Features;

// SignalR is realtime UI transport only, never the source of truth (docs/01-ARCHITECTURE-GUARDRAILS.md):
// the hub just tells connected staff screens (QR admin, cashier/POS) that a QR order event happened for
// their branch, and each screen re-fetches the authoritative order data over the existing REST endpoints.
[Authorize]
public sealed class OrdersHub : Hub
{
    public Task JoinBranch(Guid branchId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(branchId));
    public Task LeaveBranch(Guid branchId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(branchId));
    public static string GroupName(Guid branchId) => $"orders:{branchId}";
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
