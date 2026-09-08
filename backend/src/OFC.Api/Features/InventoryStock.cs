using Microsoft.EntityFrameworkCore;
using OFC.Infrastructure.Persistence;
using OFC.Modules.Inventory;

namespace OFC.Api.Features;

// Every write to InventoryItem.StockOnHand used to read the full ledger sum into memory, add a
// delta, and write it back — a read-then-write race under concurrent movements on the same item
// (two devices syncing at once, or a POS sale racing an offline sync), which could silently lose an
// update to the cached balance. ApplyDelta instead issues a single atomic SQL UPDATE
// ("SET stock_on_hand = COALESCE(stock_on_hand,0) + @delta"), which Postgres serializes via a normal
// row lock, so concurrent deltas on the same item always accumulate correctly. The append-only
// InventoryMovements ledger remains the recoverable source of truth regardless.
internal static class InventoryStock
{
    public static Task ApplyDelta(OFCDbContext db, Guid itemId, decimal delta, CancellationToken ct)
    {
        var rounded = InventoryRules.RoundQuantity(delta);
        return db.InventoryItems.Where(x => x.Id == itemId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StockOnHand, x => (x.StockOnHand ?? 0m) + rounded), ct);
    }
}
