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

export function enqueue(operation: Omit<OutboxOperation, "idempotencyKey"> & { idempotencyKey?: string }): OutboxOperation {
  const full: OutboxOperation = {
    ...operation,
    idempotencyKey: operation.idempotencyKey ?? createId(),
  };
  const queue = pending();
  if (!queue.some((item) => item.idempotencyKey === full.idempotencyKey)) {
    store.set(Outbox, [...queue, full]);
  }
  return full;
}

export function pending(): OutboxOperation[] {
  return store.get<OutboxOperation[]>(Outbox) ?? [];
}

export function pendingCount(): number {
  return pending().length;
}

export function remove(key: string): void {
  store.set(Outbox, pending().filter((item) => item.idempotencyKey !== key));
}

export function clearPending(): void {
  store.set(Outbox, []);
}

export function conflicts(): SyncConflict[] {
  return store.get<SyncConflict[]>(Conflicts) ?? [];
}

export function conflictCount(): number {
  return conflicts().length;
}

export function storeConflicts(items: SyncConflict[]): void {
  const merged = conflicts().filter((existing) => !items.some((item) => item.idempotencyKey === existing.idempotencyKey));
  store.set(Conflicts, [...merged, ...items]);
}

export function dismissConflicts(keys: string[]): void {
  store.set(Conflicts, conflicts().filter((item) => !keys.includes(item.idempotencyKey)));
}

export function retryConflict(key: string): void {
  const item = conflicts().find((conflict) => conflict.idempotencyKey === key);
  dismissConflicts([key]);
  if (item) enqueue({ idempotencyKey: item.idempotencyKey, operationType: item.operationType, baseVersion: null, baseCatalogVersion: null, occurredAt: item.occurredAt, payload: {} });
}

export function flush(token: string): Promise<SyncBatchResponse> {
  const queue = pending();
  if (queue.length === 0) return Promise.resolve({ serverVersion: lastSyncVersion(), currentCatalogVersion: 0, pendingConflicts: conflictCount(), results: [] });
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

  return fetch("/api/v1/sync", {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify(batch),
  }).then(async (response) => {
    if (!response.ok) throw new Error("Sync failed");
    const data = (await response.json()) as SyncBatchResponse;
    applyResults(queue, data);
    return data;
  });
}

export function applyResults(queue: OutboxOperation[], data: SyncBatchResponse): void {
  const settled: string[] = [];
  const pendingFlagged = conflicts();
  for (const result of data.results) {
    if (result.status === "applied") {
      settled.push(result.idempotencyKey);
      if (result.flags.includes("stale-pricing") || result.flags.includes("negative-stock")) {
        pendingFlagged.push({
          idempotencyKey: result.idempotencyKey,
          operationType: result.operationType ?? "unknown",
          conflictReason: result.flags.includes("stale-pricing") ? "stale-pricing" : "negative-stock",
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
  const remaining = queue.filter((operation) => !settled.includes(operation.idempotencyKey));
  store.set(Outbox, remaining);
  store.set(Conflicts, pendingFlagged);
  store.set(LastVersion, data.serverVersion);
  store.set(LastSyncedAt, new Date().toISOString());
}

export function backoffDelay(attempt: number): number {
  const base = Math.min(8, Math.max(0, attempt));
  return Math.min(30_000, 1000 * 2 ** base);
}
