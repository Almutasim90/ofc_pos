import type { Dexie, Table } from "dexie";
import { createId, store } from "@/lib/local-store";

export type SyncStatus = "applied" | "duplicate" | "conflict" | "failed";

export type OutboxOperation = {
  idempotencyKey: string;
  operationType: string;
  baseVersion: number | null;
  baseCatalogVersion: number | null;
  occurredAt: string;
  payload: Record<string, unknown>;
};

export type SyncResultItem = {
  idempotencyKey: string;
  operationType: string | null;
  status: SyncStatus;
  flags: string[];
  conflictReason: string | null;
  error: string | null;
  serverVersion: number | null;
  result?: unknown;
};

export type SyncBatchResponse = {
  serverVersion: number;
  currentCatalogVersion: number;
  pendingConflicts: number;
  results: SyncResultItem[];
};

export type SyncConflict = {
  idempotencyKey: string;
  operationType: string;
  conflictReason: string | null;
  error: string | null;
  result?: unknown;
  occurredAt: string;
};

const Outbox = "sync:outbox";
const Conflicts = "sync:conflicts";
const LastVersion = "sync:lastVersion";
const Device = "sync:device";
const Branch = "sync:branch";
const LastSyncedAt = "sync:lastSyncedAt";

export function getDeviceId(): string {
  const existing = store.get<string>(Device);
  if (existing) return existing;
  const id = createId();
  store.set(Device, id);
  return id;
}

export function getBranchId(): string {
  return store.get<string>(Branch) ?? "";
}

export function setBranchId(id: string): void {
  store.set(Branch, id);
}

export function lastSyncVersion(): number {
  return store.get<number>(LastVersion) ?? 0;
}

export function lastSyncedAt(): string | null {
  return store.get<string>(LastSyncedAt);
}

// ---------------------------------------------------------------------------------------------------
// Offline queue storage (SRS §4 "IndexedDB / Dexie.js"). Queued sales and their conflicts live in
// IndexedDB: far larger than localStorage and transactional, so a full store can no longer drop a sale
// silently. Every write is awaited and throws on failure, so the register only says "saved offline"
// once the sale really is stored. Small metadata (device, branch, last version) stays in localStorage
// because it is read synchronously at startup.

type OutboxRow = OutboxOperation & { seq?: number };

type OfflineDb = Dexie & {
  outbox: Table<OutboxRow, number>;
  conflicts: Table<SyncConflict, string>;
};

// Dexie is loaded on first use, so it stays out of the register's startup bundle.
async function createOfflineDb(): Promise<OfflineDb> {
  const { default: DexieClass } = await import("dexie");
  const instance = new DexieClass("ofc-offline") as OfflineDb;
  instance.version(1).stores({
    // seq keeps the original order of sales; the idempotency key is unique.
    outbox: "++seq, &idempotencyKey",
    conflicts: "&idempotencyKey",
  });
  return instance;
}

let database: Promise<OfflineDb | null> | null = null;

// Opens IndexedDB once and moves anything still queued in localStorage (from before this change) into it.
// The old copy is removed only after the move succeeded, so an interrupted migration loses nothing.
function db(): Promise<OfflineDb | null> {
  database ??= (async () => {
    if (typeof indexedDB === "undefined") return null;
    try {
      const instance = await createOfflineDb();
      await instance.open();
      const legacyOutbox = store.get<OutboxOperation[]>(Outbox);
      const legacyConflicts = store.get<SyncConflict[]>(Conflicts);
      if (legacyOutbox?.length || legacyConflicts?.length) {
        await instance.transaction(
          "rw",
          instance.outbox,
          instance.conflicts,
          async () => {
            for (const operation of legacyOutbox ?? []) {
              const exists = await instance.outbox
                .where("idempotencyKey")
                .equals(operation.idempotencyKey)
                .count();
              if (!exists) await instance.outbox.add({ ...operation });
            }
            if (legacyConflicts?.length)
              await instance.conflicts.bulkPut(legacyConflicts);
          },
        );
      }
      store.remove(Outbox);
      store.remove(Conflicts);
      return instance;
    } catch {
      // IndexedDB can be unavailable (restricted/private contexts) or its code may not have loaded yet:
      // fall back to localStorage for this write and try IndexedDB again on the next one.
      database = null;
      return null;
    }
  })();
  return database;
}

function writeLegacy<T>(name: string, value: T) {
  if (!store.set(name, value))
    throw new Error("Local storage is full or unavailable.");
}

export async function enqueue(
  operation: Omit<OutboxOperation, "idempotencyKey"> & {
    idempotencyKey?: string;
  },
): Promise<OutboxOperation> {
  const full: OutboxOperation = {
    ...operation,
    idempotencyKey: operation.idempotencyKey ?? createId(),
  };
  const instance = await db();
  if (instance) {
    await instance.transaction("rw", instance.outbox, async () => {
      const exists = await instance.outbox
        .where("idempotencyKey")
        .equals(full.idempotencyKey)
        .count();
      if (!exists) await instance.outbox.add({ ...full });
    });
    return full;
  }
  const queue = store.get<OutboxOperation[]>(Outbox) ?? [];
  if (!queue.some((item) => item.idempotencyKey === full.idempotencyKey))
    writeLegacy(Outbox, [...queue, full]);
  return full;
}

const stripSeq = ({ seq: _seq, ...operation }: OutboxRow): OutboxOperation =>
  operation;

export async function pending(): Promise<OutboxOperation[]> {
  const instance = await db();
  if (instance)
    return (await instance.outbox.orderBy("seq").toArray()).map(stripSeq);
  return store.get<OutboxOperation[]>(Outbox) ?? [];
}

export async function pendingCount(): Promise<number> {
  const instance = await db();
  return instance
    ? instance.outbox.count()
    : (store.get<OutboxOperation[]>(Outbox) ?? []).length;
}

async function removeFromOutbox(keys: string[]) {
  const instance = await db();
  if (instance) {
    await instance.outbox.where("idempotencyKey").anyOf(keys).delete();
    return;
  }
  writeLegacy(
    Outbox,
    (store.get<OutboxOperation[]>(Outbox) ?? []).filter(
      (item) => !keys.includes(item.idempotencyKey),
    ),
  );
}

export async function remove(key: string): Promise<void> {
  await removeFromOutbox([key]);
}

export async function clearPending(): Promise<void> {
  const instance = await db();
  if (instance) await instance.outbox.clear();
  else writeLegacy(Outbox, []);
}

export async function conflicts(): Promise<SyncConflict[]> {
  const instance = await db();
  if (instance) return instance.conflicts.toArray();
  return store.get<SyncConflict[]>(Conflicts) ?? [];
}

export async function conflictCount(): Promise<number> {
  return (await conflicts()).length;
}

async function replaceConflicts(items: SyncConflict[]) {
  const instance = await db();
  if (instance) {
    await instance.transaction("rw", instance.conflicts, async () => {
      await instance.conflicts.clear();
      if (items.length) await instance.conflicts.bulkPut(items);
    });
    return;
  }
  writeLegacy(Conflicts, items);
}

export async function storeConflicts(items: SyncConflict[]): Promise<void> {
  const merged = (await conflicts()).filter(
    (existing) =>
      !items.some((item) => item.idempotencyKey === existing.idempotencyKey),
  );
  await replaceConflicts([...merged, ...items]);
}

// Clears the conflict banner only. A genuine conflict/failed operation is never removed from the
// outbox by applyResults (only "applied"/"duplicate" results are — see below), so its real payload is
// still queued and will be resent on the next flush; an already-*applied*-but-flagged operation
// (stale-pricing/negative-stock) has nothing left in the outbox to touch either way.
export async function dismissConflicts(keys: string[]): Promise<void> {
  await replaceConflicts(
    (await conflicts()).filter((item) => !keys.includes(item.idempotencyKey)),
  );
}

// Abandons an operation entirely: clears the banner AND removes it from the outbox so it stops being
// resent. Use this for a conflict/failed operation the user has decided not to retry (e.g. stale data
// that can't resolve itself) — dismissConflicts alone would leave it silently retrying forever.
export async function cancelPending(keys: string[]): Promise<void> {
  await dismissConflicts(keys);
  await removeFromOutbox(keys);
}

// The original payload was never lost (see dismissConflicts) — retrying just needs to clear the
// conflict banner and let the next flush resend the real, unmodified operation still sitting in the
// outbox.
export async function retryConflict(key: string): Promise<void> {
  await dismissConflicts([key]);
}

export async function flush(token: string): Promise<SyncBatchResponse> {
  const queue = await pending();
  if (queue.length === 0)
    return {
      serverVersion: lastSyncVersion(),
      currentCatalogVersion: 0,
      pendingConflicts: await conflictCount(),
      results: [],
    };
  const batch = {
    branchId: getBranchId(),
    deviceId: getDeviceId(),
    lastSyncVersion: lastSyncVersion(),
    baseCatalogVersion: queue[0]?.baseCatalogVersion ?? null,
    operations: queue.map((operation) => ({
      idempotencyKey: operation.idempotencyKey,
      operationType: operation.operationType,
      baseVersion: operation.baseVersion,
      baseCatalogVersion: operation.baseCatalogVersion,
      occurredAt: operation.occurredAt,
      payload: operation.payload,
    })),
  };

  const response = await fetch("/api/v1/sync", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(batch),
  });
  if (!response.ok) throw new Error("Sync failed");
  const data = (await response.json()) as SyncBatchResponse;
  await applyResults(queue, data);
  return data;
}

export async function applyResults(
  queue: OutboxOperation[],
  data: SyncBatchResponse,
): Promise<void> {
  const settled: string[] = [];
  const pendingFlagged = await conflicts();
  for (const result of data.results) {
    if (result.status === "applied") {
      settled.push(result.idempotencyKey);
      if (
        result.flags.includes("stale-pricing") ||
        result.flags.includes("negative-stock")
      ) {
        pendingFlagged.push({
          idempotencyKey: result.idempotencyKey,
          operationType: result.operationType ?? "unknown",
          conflictReason: result.flags.includes("stale-pricing")
            ? "stale-pricing"
            : "negative-stock",
          error: result.error,
          result: result.result,
          occurredAt: new Date().toISOString(),
        });
      }
    } else if (result.status === "duplicate") {
      settled.push(result.idempotencyKey);
    } else if (result.status === "conflict") {
      pendingFlagged.push({
        idempotencyKey: result.idempotencyKey,
        operationType: result.operationType ?? "unknown",
        conflictReason: result.conflictReason,
        error: result.error,
        result: result.result,
        occurredAt: new Date().toISOString(),
      });
    } else if (result.status === "failed") {
      pendingFlagged.push({
        idempotencyKey: result.idempotencyKey,
        operationType: result.operationType ?? "unknown",
        conflictReason: "failed",
        error: result.error ?? "The operation failed.",
        occurredAt: new Date().toISOString(),
      });
    }
  }
  // Only operations that were part of this batch and settled leave the queue; sales queued while the
  // request was in flight stay untouched.
  const settledInBatch = queue
    .map((operation) => operation.idempotencyKey)
    .filter((key) => settled.includes(key));
  if (settledInBatch.length) await removeFromOutbox(settledInBatch);
  // A flagged key can appear more than once (earlier banner + this batch); keep the latest.
  const byKey = new Map(
    pendingFlagged.map((item) => [item.idempotencyKey, item]),
  );
  await replaceConflicts([...byKey.values()]);
  store.set(LastVersion, data.serverVersion);
  store.set(LastSyncedAt, new Date().toISOString());
}

export function backoffDelay(attempt: number): number {
  const base = Math.min(8, Math.max(0, attempt));
  return Math.min(30_000, 1000 * 2 ** base);
}

// Opens the offline store (and loads its code) ahead of time, while the register is still online, so the
// first offline sale never depends on downloading anything.
export function warmOfflineStore(): void {
  void db();
}
