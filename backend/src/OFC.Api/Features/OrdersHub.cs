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

// Customer tracking uses a separate anonymous hub. Joining requires both the public QR context code and
// the unguessable client request id, and the server verifies that they belong to the same branch.
[AllowAnonymous]
public sealed class CustomerOrdersHub(OFCDbContext db) : Hub
{
    public async Task JoinOrder(string contextCode, Guid clientRequestId)
    {
        var code = contextCode.Trim().ToUpperInvariant();
        var branchId = await db.QrContexts.AsNoTracking().Where(x => x.Code == code && x.IsActive).Select(x => (Guid?)x.BranchId).SingleOrDefaultAsync(Context.ConnectionAborted);
        if (!branchId.HasValue || !await db.Orders.AsNoTracking().AnyAsync(x => x.BranchId == branchId.Value && x.ClientRequestId == clientRequestId, Context.ConnectionAborted)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(clientRequestId));
    }

    public static string GroupName(Guid clientRequestId) => $"customer-order:{clientRequestId:N}";
}

public interface IOrdersBroadcaster
{
    Task QrOrderReceived(Guid branchId, Guid orderId, string clientRequestId, string status, string approvalStatus, decimal grossAmount, string contextCode);
    Task QrOrderReviewed(Guid branchId, Guid orderId, string clientRequestId, string status, string approvalStatus);
    Task CustomerOrderChanged(Guid clientRequestId, string status);
}

public sealed class OrdersBroadcaster(IHubContext<OrdersHub> hub, IHubContext<CustomerOrdersHub> customerHub) : IOrdersBroadcaster
{
    public Task QrOrderReceived(Guid branchId, Guid orderId, string clientRequestId, string status, string approvalStatus, decimal grossAmount, string contextCode) =>
        hub.Clients.Group(OrdersHub.GroupName(branchId)).SendAsync("qrOrderReceived", new { branchId, orderId, clientRequestId, status, approvalStatus, grossAmount, contextCode });

    public Task QrOrderReviewed(Guid branchId, Guid orderId, string clientRequestId, string status, string approvalStatus) =>
        hub.Clients.Group(OrdersHub.GroupName(branchId)).SendAsync("qrOrderReviewed", new { branchId, orderId, clientRequestId, status, approvalStatus });

    public Task CustomerOrderChanged(Guid clientRequestId, string status) =>
        customerHub.Clients.Group(CustomerOrdersHub.GroupName(clientRequestId)).SendAsync("orderChanged", new { clientRequestId, status });
}
