import { useEffect, useState } from "react";
import { RefreshCw, ShoppingBag, Trash2, Wifi, WifiOff } from "lucide-react";
import { store } from "@/lib/local-store";
import { backoffDelay, conflicts, dismissConflicts, enqueue, flush, getBranchId, lastSyncVersion, lastSyncedAt, pending, pendingCount, retryConflict, setBranchId } from "@/lib/sync-outbox";

type Language = "ar" | "en";
type Context = { branches: Array<{ id: string; nameAr: string; nameEn: string }>; channels: Array<{ id: string; code: string; nameAr: string; nameEn: string }> };
type CatalogProduct = { id: string; nameAr: string; nameEn: string; basePrice: number | null; selectionGroups: Array<{ id: string; isRequired: boolean; options: Array<{ id: string; priceAdjustment: number }> }> };
type Conflict = { idempotencyKey: string; operationType: string; conflictReason: string | null; error: string | null; result?: unknown };

const copy = {
  ar: {
    title: "المزامنة والاسترداد", intro: "تعامل مع العمليات المحلية المعلّقة، والتعارضات عند عودة الاتصال.", online: "متصل", offline: "غير متصل", syncing: "جارٍ المزامنة", syncNow: "مزامنة الآن", pending: "عمليات معلّقة", conflicts: "تعارضات", lastSynced: "آخر مزامنة", never: "لم تتم", serverVersion: "إصدار الخادم", catalogVersion: "إصدار الكتالوج", noPending: "لا توجد عمليات معلّقة", noConflicts: "لا توجد تعارضات", retry: "إعادة المحاولة", dismiss: "تجاهل", enqueue: "إرسال طلب تجريبي (غير متصل)", queued: "تم إرسال الطلب للمزامنة عند العودة للاتصال.", clear: "مسح", stalePricing: "تسعير قديم: تم تسجيل الطلب دون إعادة التسعير.", negativeStock: "المخزون أصبح سالبًا بعد العملية.", applied: "تمت المزامنة بنجاح", failed: "فشلت العملية", done: "تمت المزامنة", loading: "جارٍ تحميل بيانات المزامنة", note: "تُنشأ الطلبات غير المتصلة بأسعار مجمّدة لا يعيد الخادم تسعيرها."
  } as const,
  en: {
    title: "Sync & recovery", intro: "Manage queued local operations and conflicts when connectivity returns.", online: "Online", offline: "Offline", syncing: "Syncing", syncNow: "Sync now", pending: "Pending operations", conflicts: "Conflicts", lastSynced: "Last synced", never: "Never", serverVersion: "Server version", catalogVersion: "Catalog version", noPending: "No pending operations", noConflicts: "No conflicts", retry: "Retry", dismiss: "Dismiss", enqueue: "Send trial offline order", queued: "Order queued; it will sync when connection returns.", clear: "Clear", stalePricing: "Stale pricing: the order was recorded without repricing.", negativeStock: "Stock went negative after this operation.", applied: "Synced successfully", failed: "Operation failed", done: "Synced", loading: "Loading sync state", note: "Offline orders are created with frozen prices that the server does not re-price."
  } as const,
} as const;

const reasonLabel = { "stale-pricing": "stale", "negative-stock": "negative", "entity-conflict": "conflict", failed: "failed" } as const;

export function SyncSection({ language }: { language: Language }) {
  const t = copy[language];
  const [online, setOnline] = useState(navigator.onLine);
  const [pendingItems, setPendingItems] = useState(pending());
  const [conflictItems, setConflictItems] = useState<Conflict[]>(conflicts());
  const [syncing, setSyncing] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);
  const [version, setVersion] = useState(lastSyncVersion());
  const [catalogVersion, setCatalogVersion] = useState(0);
  const [syncedAt, setSyncedAt] = useState(lastSyncedAt());
  const [branchId, setBranchIdState] = useState(getBranchId());
  const token = store.get<string>("session-token") ?? "";

  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}`, ...(init?.headers ?? {}) } });

  const refresh = () => {
    setPendingItems(pending());
    setConflictItems(conflicts());
    setVersion(lastSyncVersion());
    setSyncedAt(lastSyncedAt());
  };

  useEffect(() => {
    const update = () => { setOnline(navigator.onLine); };
    addEventListener("online", update);
    addEventListener("offline", update);
    return () => {
      removeEventListener("online", update);
      removeEventListener("offline", update);
    };
  }, []);

  async function loadState() {
    try {
      const response = await auth(`/api/v1/sync/state?branchId=${branchId}`);
      if (response.ok) {
        const value = await response.json() as { serverVersion: number; currentCatalogVersion: number };
        setVersion(value.serverVersion);
        setCatalogVersion(value.currentCatalogVersion);
      }
    } catch {
      // The device may be offline; cached values remain.
    }
  }

  useEffect(() => { if (token && branchId) void loadState(); }, [branchId, token]);

  async function syncNow() {
    setSyncing(true);
    setMessage("");
    try {
      await flush(token);
      refresh();
      setMessage(t.done);
    } catch (e) {
      setIsError(true);
      setMessage(e instanceof Error ? e.message : t.failed);
    } finally {
      setSyncing(false);
    }
  }

  useEffect(() => {
    if (!online || pendingCount() === 0 || syncing) return;
    let cancelled = false;
    let attempt = 0;
    const run = async () => {
      if (cancelled) return;
      try {
        await flush(token);
        if (!cancelled) { refresh(); setMessage(t.done); }
      } catch {
        if (cancelled) return;
        const delay = backoffDelay(attempt);
        attempt += 1;
        window.setTimeout(() => void run(), delay);
      }
    };
    void run();
    return () => { cancelled = true; };
  }, [online, syncing, token]);

  async function enqueueTrial() {
    setMessage("");
    setSyncing(true);
    try {
      const contextResponse = await auth("/api/v1/pos/context");
      if (!contextResponse.ok) throw new Error(t.failed);
      const context = await contextResponse.json() as Context;
      const activeBranch = context.branches[0];
      const channel = context.channels[0];
      if (!activeBranch || !channel) throw new Error(t.failed);
      setBranchId(activeBranch.id);
      setBranchIdState(activeBranch.id);
      const catalogResponse = await auth(`/api/v1/pos/catalog?branchId=${activeBranch.id}&salesChannelId=${channel.id}`);
      if (!catalogResponse.ok) throw new Error(t.failed);
      const products = await catalogResponse.json() as CatalogProduct[];
      const product = products[0];
      if (!product) throw new Error(t.failed);
      let snapshot = product.basePrice ?? 0;
      let selections = "[]";
      if (product.selectionGroups.length) {
        const groups = product.selectionGroups.map((group) => ({ groupId: group.id, options: group.options.slice(0, 1).map((option) => option.id) }));
        snapshot += product.selectionGroups.flatMap((group) => group.options.slice(0, 1)).reduce((sum, option) => sum + option.priceAdjustment, 0);
        selections = JSON.stringify(groups);
      }
      const orderLine = {
        productId: product.id,
        productNameAr: product.nameAr,
        productNameEn: product.nameEn,
        quantity: 1,
        note: null,
        selectionsSnapshot: selections,
        unitListAmount: snapshot,
        unitDiscountAmount: 0,
        unitNetAmount: snapshot,
        unitTaxAmount: 0,
        unitGrossAmount: snapshot,
        taxRate: 0,
        taxCalculationMode: "Exclusive",
        priceSource: "OfflineSnapshot",
        priceRuleId: null,
        promotionId: null,
        taxRuleId: null,
        catalogVersionId: null,
        catalogVersionNumber: catalogVersion || null,
      };
      const payload = { salesChannelId: channel.id, source: "Pos", status: "Paid", note: null, lines: [orderLine] };
      enqueue({ operationType: "order.create", baseVersion: version || null, baseCatalogVersion: catalogVersion || null, occurredAt: new Date().toISOString(), payload });
      refresh();
      setMessage(t.queued);
    } catch (e) {
      setIsError(true);
      setMessage(e instanceof Error ? e.message : t.failed);
    } finally {
      setSyncing(false);
    }
  }

  const fmtDate = (value: string | null) => value ? new Intl.DateTimeFormat(language, { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)) : t.never;
  const reasonLabelFor = (reason: string | null) => reason ? (reasonLabel[reason as keyof typeof reasonLabel] ?? reason) : "";

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <p className="mt-3 max-w-3xl text-[#64716b]">{t.intro}</p>
      <p className="mt-3 max-w-3xl text-sm text-[#66736d]">{t.note}</p>

      <div className="mt-5 flex flex-wrap items-center gap-3 rounded-xl border border-[#d9dfd7] bg-[#edf5f1] p-3 text-sm text-[#08483f]">
        <span className={`inline-flex items-center gap-2 font-semibold ${online ? "text-[#137347]" : "text-[#b4322a]"}`}>{online ? <Wifi size={18} /> : <WifiOff size={18} />}{online ? t.online : t.offline}</span>
        <span className="text-[#53615b]">{t.serverVersion}: <strong>{version}</strong></span>
        <span className="text-[#53615b]">{t.catalogVersion}: <strong>{catalogVersion}</strong></span>
        <button onClick={() => void syncNow()} disabled={syncing || !online} className="ml-auto inline-flex min-h-10 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 text-xs font-semibold text-white disabled:opacity-50"><RefreshCw size={15} className={syncing ? "animate-spin" : ""} />{syncing ? t.syncing : t.syncNow}</button>
      </div>

      {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}

      <div className="mt-6 grid gap-5 lg:grid-cols-2">
        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="flex items-center justify-between font-semibold"><span>{t.pending} ({pendingItems.length})</span>{pendingItems.length > 0 && online && <button onClick={() => void syncNow()} disabled={syncing} className="min-h-8 rounded-lg border border-[#0e5a4f] px-2.5 text-xs font-semibold text-[#0e5a4f] disabled:opacity-50">{t.syncNow}</button>}</h2>
          {pendingItems.length === 0 ? <p className="mt-4 text-sm text-[#69766f]">{t.noPending}</p> : (
            <ul className="mt-4 divide-y divide-[#e8ece8]">{pendingItems.map((item) => <li key={item.idempotencyKey} className="flex items-center justify-between gap-3 py-3 text-sm"><span className="min-w-0 truncate">{item.operationType} <span className="text-[#69766f]">· {item.idempotencyKey.slice(0, 8)}</span></span><span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${online ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#f4f1e3] text-[#8a6d1f]"}`}>{online ? t.applied : t.offline}</span></li>)}</ul>
          )}
          <div className="mt-5 flex flex-wrap gap-3">
            <button onClick={() => void enqueueTrial()} disabled={syncing || !online} className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 text-sm font-semibold text-white disabled:opacity-50"><ShoppingBag size={18} />{t.enqueue}</button>
            <p className="text-xs text-[#66736d]">{t.lastSynced}: {fmtDate(syncedAt)}</p>
          </div>
        </section>

        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="flex items-center justify-between font-semibold"><span>{t.conflicts} ({conflictItems.length})</span>{conflictItems.length > 0 && <button onClick={() => { dismissConflicts(conflictItems.map((c) => c.idempotencyKey)); refresh(); }} className="min-h-8 rounded-lg border border-[#b4322a] px-2.5 text-xs font-semibold text-[#b4322a]">{t.clear}</button>}</h2>
          {conflictItems.length === 0 ? <p className="mt-4 text-sm text-[#69766f]">{t.noConflicts}</p> : (
            <ul className="mt-4 space-y-3">{conflictItems.map((item) => {
              const stale = item.conflictReason === "stale-pricing";
              return (
                <li key={item.idempotencyKey} className="rounded-xl border border-[#e8ece8] bg-[#fafbfa] p-3">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-sm font-medium">{item.operationType}</span>
                    <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${stale ? "bg-[#f4f1e3] text-[#8a6d1f]" : item.conflictReason === "negative-stock" ? "bg-[#fbe4e2] text-[#b4322a]" : "bg-[#e8ece8] text-[#53615b]"}`}>{reasonLabelFor(item.conflictReason)}</span>
                  </div>
                  {item.error && <p className="mt-2 text-sm text-[#66736d]">{item.error}</p>}
                  {stale && <p className="mt-2 text-xs text-[#8a6d1f]">{t.stalePricing}</p>}
                  {item.conflictReason === "negative-stock" && <p className="mt-2 text-xs text-[#b4322a]">{t.negativeStock}</p>}
                  <div className="mt-3 flex gap-2">
                    <button onClick={() => { retryConflict(item.idempotencyKey); refresh(); }} className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#0e5a4f] px-3 text-xs font-semibold text-[#0e5a4f]"><RefreshCw size={14} />{t.retry}</button>
                    <button onClick={() => { dismissConflicts([item.idempotencyKey]); refresh(); }} className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#b4322a] px-3 text-xs font-semibold text-[#b4322a]"><Trash2 size={14} />{t.dismiss}</button>
                  </div>
                </li>
              );
            })}</ul>
          )}
        </section>
      </div>
    </div>
  );
}
