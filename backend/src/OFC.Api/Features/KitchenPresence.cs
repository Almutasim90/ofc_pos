using System.Collections.Concurrent;

namespace OFC.Api.Features;

// Which branches have a kitchen screen (KDS) connected right now, and when the last one went away. Only the
// kitchen screen joins KitchenHub, so a live hub connection is a live kitchen screen. The register uses this
// to say "the kitchen screen is disconnected" once, instead of waiting out a timeout on every order.
// In-memory by design: it describes live connections to this API process, and after a restart screens
// simply reconnect and re-register.
public sealed class KitchenPresence(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, Guid> _connections = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastSeen = new();

    public void Joined(string connectionId, Guid branchId)
    {
        _connections[connectionId] = branchId;
        _lastSeen[branchId] = clock.GetUtcNow();
    }

    public void Left(string connectionId)
    {
        if (_connections.TryRemove(connectionId, out var branchId)) _lastSeen[branchId] = clock.GetUtcNow();
    }

    public (int Screens, DateTimeOffset? LastSeenAt) Snapshot(Guid branchId) =>
        (_connections.Values.Count(x => x == branchId), _lastSeen.TryGetValue(branchId, out var at) ? at : null);
}
