import { useEffect, useRef, useState } from "react";
import {
  Bell,
  BellOff,
  CheckCircle2,
  ChefHat,
  Clock3,
  Printer,
  RefreshCw,
  Wifi,
  WifiOff,
} from "lucide-react";
import { SearchableSelect } from "@/app/SearchableSelect";
import { store } from "@/lib/local-store";
import { printKitchenSlip, snapshotChoices } from "@/lib/kitchen-slip";
import { useReliableBranchHub } from "@/lib/reliable-hub";
import { Button } from "@/components/ui/button";

type Language = "ar" | "en";
type Branch = { id: string; nameAr: string; nameEn: string };
type Station = { id: string; code: string; nameAr: string; nameEn: string };
type DispatchStatus =
  | "Pending"
  | "SentToKds"
  | "KdsAcknowledged"
  | "PrintFallbackPending"
  | "PrintedFallback"
  | "Failed"
  | "Cancelled";
type ItemStatus = "New" | "Preparing" | "Ready" | "Completed" | "Cancelled";
type TicketStatus = "New" | "Preparing" | "Ready" | "Completed" | "Cancelled";
type TicketItem = {
  id: string;
  productNameAr: string;
  productNameEn: string;
  quantity: number;
  note: string | null;
  selections: string;
  status: ItemStatus;
};
type Ticket = {
  id: string;
  orderNumber: string;
  orderType: string | null;
  orderTypeNameAr: string | null;
  orderTypeNameEn: string | null;
  dispatchStatus: DispatchStatus;
  status: TicketStatus;
  note: string | null;
  createdAt: string;
  overdue: boolean;
  items: TicketItem[];
};

const copy = {
  ar: {
    title: "المطبخ",
    branch: "الفرع",
    allStations: "كل المحطات",
    connected: "متصل",
    connecting: "جارٍ الاتصال…",
    disconnected: "غير متصل — تتحدث الطلبات كل 10 ثوانٍ",
    reload: "تحديث",
    orders: "طلب",
    late: "متأخر",
    newTicket: "جديد",
    dineIn: "محلي",
    takeaway: "سفري",
    ready: "جاهز",
    print: "طباعة",
    printed: "تمت الطباعة",
    fallbackNote: "الطلب بانتظار الطباعة الاحتياطية",
    cancel: "إلغاء الطلب",
    confirmCancel: "تأكيد الإلغاء",
    keep: "تراجع",
    kitchenSlip: "طلب مطبخ",
    soundOn: "صوت الطلبات مفعّل",
    soundOff: "اضغط لتفعيل صوت الطلبات",
    failed: "تعذر تنفيذ العملية.",
    emptyTitle: "لا توجد طلبات الآن",
    emptyHint: "الطلبات الجديدة تظهر هنا تلقائياً مع صوت تنبيه.",
  },
  en: {
    title: "Kitchen",
    branch: "Branch",
    allStations: "All stations",
    connected: "Connected",
    connecting: "Connecting…",
    disconnected: "Offline — orders refresh every 10 seconds",
    reload: "Refresh",
    orders: "orders",
    late: "late",
    newTicket: "New",
    dineIn: "Dine-in",
    takeaway: "Takeaway",
    ready: "Ready",
    print: "Print",
    printed: "Mark printed",
    fallbackNote: "Waiting for the fallback print",
    cancel: "Cancel order",
    confirmCancel: "Confirm cancel",
    keep: "Keep",
    kitchenSlip: "Kitchen order",
    soundOn: "Order sound on",
    soundOff: "Tap to turn on the order sound",
    failed: "Unable to complete the operation.",
    emptyTitle: "No orders right now",
    emptyHint: "New orders appear here automatically, with a sound alert.",
  },
} as const;

export function KitchenSection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) =>
    language === "ar" ? x.nameAr : x.nameEn;
  const auth = (path: string, init?: RequestInit) =>
    fetch(path, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`,
        ...(init?.headers ?? {}),
      },
    });

  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [stations, setStations] = useState<Station[]>([]);
  const [stationId, setStationId] = useState("");
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [loaded, setLoaded] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);
  // Orders marked ready leave the screen at once; the next one moves up without waiting for the server.
  const [doneIds, setDoneIds] = useState<Set<string>>(() => new Set());
  const [now, setNow] = useState(() => Date.now());
  const [soundReady, setSoundReady] = useState(() => audioRunning());

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(timer);
  }, []);

  // Browsers only allow sound after the screen has been touched once.
  useEffect(() => {
    const unlock = () => {
      void unlockAudio().then(() => setSoundReady(audioRunning()));
    };
    window.addEventListener("pointerdown", unlock);
    window.addEventListener("keydown", unlock);
    return () => {
      window.removeEventListener("pointerdown", unlock);
      window.removeEventListener("keydown", unlock);
    };
  }, []);

  // A kitchen tablet that goes to sleep drops its live connection; keep the screen awake.
  useEffect(() => {
    type WakeLock = { release: () => Promise<void> };
    const wakeLock = (
      navigator as Navigator & {
        wakeLock?: { request: (type: "screen") => Promise<WakeLock> };
      }
    ).wakeLock;
    if (!wakeLock) return;
    let lock: WakeLock | null = null;
    const request = () => {
      if (document.visibilityState !== "visible") return;
      wakeLock
        .request("screen")
        .then((value) => (lock = value))
        .catch(() => undefined);
    };
    request();
    document.addEventListener("visibilitychange", request);
    return () => {
      document.removeEventListener("visibilitychange", request);
      void lock?.release().catch(() => undefined);
    };
  }, []);

  // Tickets already on screen; a new one rings so a busy kitchen notices it.
  const seenTickets = useRef<Set<string> | null>(null);
  useEffect(() => {
    seenTickets.current = null;
  }, [branchId, stationId]);
  async function loadTickets() {
    if (!branchId) return;
    setLoading(true);
    try {
      const url = `/api/v1/kitchen/tickets?branchId=${branchId}${stationId ? `&stationId=${stationId}` : ""}&activeOnly=true`;
      const response = await auth(url);
      if (!response.ok) return;
      const next = (await response.json()) as Ticket[];
      const seen = seenTickets.current;
      if (seen && next.some((x) => !seen.has(x.id) && isActive(x)))
        playKitchenBell();
      seenTickets.current = new Set(next.map((x) => x.id));
      setTickets(next);
      acknowledgeShown(next);
    } catch {
      /* Keep the current tickets; the next refresh tries again. */
    } finally {
      setLoading(false);
      setLoaded(true);
    }
  }

  // A ticket on this screen has been received: confirm it automatically so the register doesn't warn
  // "kitchen hasn't received it" and the fallback printer isn't triggered while the cooks work on it.
  const acknowledging = useRef(new Set<string>());
  function acknowledgeShown(shown: Ticket[]) {
    const fresh = shown.filter(
      (x) =>
        x.dispatchStatus === "SentToKds" && !acknowledging.current.has(x.id),
    );
    if (!fresh.length) return;
    fresh.forEach((x) => acknowledging.current.add(x.id));
    void Promise.all(
      fresh.map((x) =>
        auth(`/api/v1/kitchen/tickets/${x.id}/ack`, { method: "POST" }).catch(
          () => null,
        ),
      ),
    ).then((results) => {
      // Let a failed confirmation be retried on the next refresh.
      fresh.forEach((x, i) => {
        if (!results[i]?.ok) acknowledging.current.delete(x.id);
      });
      if (results.some((r) => r?.ok)) void loadTickets();
    });
  }

  useEffect(() => {
    void (async () => {
      const [contextResponse, stationsResponse] = await Promise.all([
        auth("/api/v1/pos/context"),
        auth("/api/v1/kitchen/stations"),
      ]);
      if (contextResponse.ok) {
        const value = (await contextResponse.json()) as { branches: Branch[] };
        setBranches(value.branches);
        if (value.branches[0]) setBranchId(value.branches[0].id);
      }
      if (stationsResponse.ok)
        setStations((await stationsResponse.json()) as Station[]);
    })();
  }, []);
  useEffect(() => {
    if (branchId) void loadTickets();
  }, [branchId, stationId]);

  // SignalR is realtime transport only, not the source of truth (docs/01-ARCHITECTURE-GUARDRAILS.md):
  // the hub just tells this screen something changed for the branch, and it re-fetches the ticket list
  // over the existing REST endpoint instead of trusting ticket state carried over the socket.
  const loadTicketsRef = useRef(loadTickets);
  loadTicketsRef.current = loadTickets;
  const live = useReliableBranchHub(
    "/hubs/kitchen",
    branchId,
    (connection) =>
      connection.on("ticketChanged", (payload: { branchId: string }) => {
        if (payload.branchId === branchId) void loadTicketsRef.current();
      }),
    () => {
      void loadTicketsRef.current();
    },
  );
  useEffect(() => {
    if (!branchId) return;
    const timer = window.setInterval(
      () => {
        void loadTicketsRef.current();
      },
      live ? 60_000 : 10_000,
    );
    return () => window.clearInterval(timer);
  }, [branchId, live]);
  // Connecting (on open or after a network blip) is not "offline"; only warn if it stays down.
  const [offlineLong, setOfflineLong] = useState(false);
  useEffect(() => {
    if (live) {
      setOfflineLong(false);
      return;
    }
    const timer = window.setTimeout(() => setOfflineLong(true), 10_000);
    return () => window.clearTimeout(timer);
  }, [live]);

  async function post(ticket: Ticket, action: "ready" | "printed") {
    setError("");
    const response = await auth(
      `/api/v1/kitchen/tickets/${ticket.id}/${action}`,
      { method: "POST" },
    ).catch(() => null);
    if (!response?.ok) {
      const problem = await response?.json().catch(() => null);
      setError(problem?.errors?.ticket?.[0] ?? t.failed);
      return false;
    }
    return true;
  }

  async function ready(ticket: Ticket) {
    setDoneIds((ids) => new Set(ids).add(ticket.id));
    if (!(await post(ticket, "ready")))
      setDoneIds((ids) => {
        const next = new Set(ids);
        next.delete(ticket.id);
        return next;
      });
    void loadTickets();
  }

  async function cancel(ticket: Ticket) {
    setError("");
    const response = await auth(`/api/v1/kitchen/tickets/${ticket.id}/cancel`, {
      method: "POST",
      body: JSON.stringify({ note: null, templateCode: null }),
    }).catch(() => null);
    if (!response?.ok) {
      const problem = await response?.json().catch(() => null);
      setError(problem?.errors?.ticket?.[0] ?? t.failed);
      return;
    }
    setConfirmCancelId(null);
    void loadTickets();
  }

  const typeLabel = (ticket: Ticket) =>
    ticket.orderType === "Takeaway"
      ? t.takeaway
      : ticket.orderType === "DineIn"
        ? t.dineIn
        : (language === "ar" ? ticket.orderTypeNameAr : ticket.orderTypeNameEn) ??
          "";
  // Dine-in and takeaway never look alike: each has its own header colour.
  const headerTone = (ticket: Ticket) =>
    ticket.orderType === "Takeaway"
      ? "bg-warning text-warning-foreground"
      : ticket.orderType === "DineIn" || ticket.orderType === "InStore"
        ? "bg-success text-success-foreground"
        : "bg-foreground text-background";

  function print(ticket: Ticket) {
    printKitchenSlip({
      title: t.kitchenSlip,
      reference: `#${ticket.orderNumber}`,
      table: typeLabel(ticket) || null,
      createdAt: ticket.createdAt,
      note: ticket.note,
      language,
      lines: ticket.items
        .filter((item) => item.status !== "Cancelled")
        .map((item) => ({
          quantity: item.quantity,
          name: language === "ar" ? item.productNameAr : item.productNameEn,
          choices: snapshotChoices(item.selections, language),
          note: item.note,
        })),
    });
  }

  const visible = tickets.filter((x) => isActive(x) && !doneIds.has(x.id));
  const lateCount = visible.filter((x) => x.overdue).length;
  const elapsed = (createdAt: string) => {
    const seconds = Math.max(
      0,
      Math.floor((now - new Date(createdAt).getTime()) / 1000),
    );
    return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
  };
  const isNew = (ticket: Ticket) =>
    now - new Date(ticket.createdAt).getTime() < 60_000;

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center gap-2">
        <h1 className="me-1 text-2xl font-black tracking-tight">{t.title}</h1>
        <span
          className={`inline-flex items-center gap-1.5 rounded-full px-3 py-1.5 text-xs font-bold ${live ? "bg-success/15 text-success" : offlineLong ? "bg-destructive/15 text-destructive" : "bg-muted text-muted-foreground"}`}
        >
          {live || !offlineLong ? <Wifi size={14} /> : <WifiOff size={14} />}
          {live ? t.connected : offlineLong ? t.disconnected : t.connecting}
        </span>
        <span className="rounded-full bg-muted px-3 py-1.5 text-xs font-bold">
          {visible.length} {t.orders}
        </span>
        {lateCount > 0 && (
          <span className="rounded-full bg-destructive px-3 py-1.5 text-xs font-bold text-destructive-foreground">
            {lateCount} {t.late}
          </span>
        )}
        <div className="ms-auto flex flex-wrap items-center gap-2">
          <Button
            type="button"
            onClick={() => {
              void unlockAudio().then(() => {
                setSoundReady(audioRunning());
                playKitchenBell();
              });
            }}
            className={`inline-flex min-h-11 items-center gap-2 rounded-full px-4 text-xs font-bold ${soundReady ? "border border-border bg-card" : "animate-pulse bg-warning text-warning-foreground"}`}
          >
            {soundReady ? <Bell size={16} /> : <BellOff size={16} />}
            {soundReady ? t.soundOn : t.soundOff}
          </Button>
          {branches.length > 1 && (
            <div className="w-44">
              <SearchableSelect
                label={t.branch}
                hideLabel
                value={branchId}
                onChange={setBranchId}
              >
                {branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {name(b)}
                  </option>
                ))}
              </SearchableSelect>
            </div>
          )}
          <Button
            type="button"
            onClick={() => void loadTickets()}
            aria-label={t.reload}
            title={t.reload}
            className="grid size-11 place-items-center rounded-full border border-border bg-card"
          >
            <RefreshCw className={loading ? "animate-spin" : ""} size={17} />
          </Button>
        </div>
      </header>

      {stations.length > 0 && (
        <div className="flex gap-2 overflow-x-auto pb-1">
          {[{ id: "", label: t.allStations }, ...stations.map((s) => ({ id: s.id, label: name(s) }))].map(
            (station) => (
              <Button
                key={station.id || "all"}
                type="button"
                onClick={() => setStationId(station.id)}
                className={`min-h-10 shrink-0 rounded-full px-4 text-sm font-semibold ${stationId === station.id ? "bg-primary text-primary-foreground" : "border border-border bg-card"}`}
              >
                {station.label}
              </Button>
            ),
          )}
        </div>
      )}

      {error && (
        <p
          role="alert"
          className="rounded-xl bg-destructive/10 px-4 py-3 text-sm font-semibold text-destructive"
        >
          {error}
        </p>
      )}

      {loaded && visible.length === 0 && (
        <div className="grid place-items-center rounded-2xl border border-dashed border-border bg-card px-6 py-20 text-center">
          <span className="grid size-20 place-items-center rounded-3xl bg-accent text-primary">
            <ChefHat size={38} />
          </span>
          <h2 className="mt-5 text-2xl font-black">{t.emptyTitle}</h2>
          <p className="mt-2 max-w-md text-muted-foreground">{t.emptyHint}</p>
        </div>
      )}

      <div className="grid items-start gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
        {visible.map((ticket) => (
          <article
            key={ticket.id}
            className={`flex flex-col overflow-hidden rounded-2xl border-2 bg-card shadow-sm ${isNew(ticket) ? "border-primary ring-4 ring-primary/25" : ticket.overdue ? "border-destructive" : "border-border"}`}
          >
            <div
              className={`flex items-start justify-between gap-3 px-4 py-3 ${headerTone(ticket)}`}
            >
              <div className="min-w-0">
                <p className="text-3xl font-black leading-none">
                  #{ticket.orderNumber}
                </p>
                {typeLabel(ticket) && (
                  <p className="mt-1.5 text-base font-black">
                    {typeLabel(ticket)}
                  </p>
                )}
              </div>
              <div className="flex shrink-0 flex-col items-end gap-1.5">
                <span
                  className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-sm font-black tabular-nums ${ticket.overdue ? "bg-destructive text-destructive-foreground" : "bg-black/20"}`}
                  dir="ltr"
                >
                  <Clock3 size={14} />
                  {elapsed(ticket.createdAt)}
                </span>
                {isNew(ticket) && (
                  <span className="animate-pulse rounded-full bg-card px-2.5 py-0.5 text-xs font-black text-foreground">
                    {t.newTicket}
                  </span>
                )}
              </div>
            </div>

            <ul className="flex-1 divide-y divide-border px-4 py-2">
              {ticket.items.map((item) => {
                const choices = snapshotChoices(item.selections, language);
                const cancelled = item.status === "Cancelled";
                return (
                  <li
                    key={item.id}
                    className={`py-2.5 ${cancelled ? "opacity-50 line-through" : ""}`}
                  >
                    <div className="flex items-start gap-3">
                      <span className="grid h-9 min-w-9 shrink-0 place-items-center rounded-lg bg-foreground px-2 text-lg font-black text-background">
                        {item.quantity}
                      </span>
                      <div className="min-w-0 pt-1">
                        <p className="text-lg font-bold leading-snug">
                          {language === "ar"
                            ? item.productNameAr
                            : item.productNameEn}
                        </p>
                        {choices.map((choice) => (
                          <p key={choice} className="text-sm font-medium">
                            + {choice}
                          </p>
                        ))}
                        {item.note && (
                          <p className="mt-1 rounded-md bg-warning/15 px-2 py-1 text-sm font-bold text-warning">
                            ✎ {item.note}
                          </p>
                        )}
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>

            {ticket.note && (
              <p className="mx-4 mb-2 rounded-lg bg-warning/15 px-3 py-2 text-sm font-bold text-warning">
                ✎ {ticket.note}
              </p>
            )}

            <div className="border-t border-border p-3">
              {ticket.dispatchStatus === "PrintFallbackPending" ? (
                <div className="space-y-2">
                  <p className="text-xs font-semibold text-warning">
                    {t.fallbackNote}
                  </p>
                  <Button
                    type="button"
                    onClick={() =>
                      void post(ticket, "printed").then(() => loadTickets())
                    }
                    className="min-h-12 w-full rounded-xl bg-warning font-bold text-warning-foreground"
                  >
                    {t.printed}
                  </Button>
                </div>
              ) : (
                <div className="grid grid-cols-[auto_minmax(0,1fr)] gap-2">
                  <Button
                    type="button"
                    onClick={() => print(ticket)}
                    className="inline-flex min-h-14 items-center gap-2 rounded-xl border-2 border-border bg-card px-4 font-bold"
                  >
                    <Printer size={20} />
                    {t.print}
                  </Button>
                  <Button
                    type="button"
                    onClick={() => void ready(ticket)}
                    className="inline-flex min-h-14 items-center justify-center gap-2 rounded-xl bg-success text-xl font-black text-success-foreground"
                  >
                    <CheckCircle2 size={22} />
                    {t.ready}
                  </Button>
                </div>
              )}
              <div className="mt-2 flex justify-end gap-2">
                {confirmCancelId === ticket.id ? (
                  <>
                    <Button
                      type="button"
                      onClick={() => setConfirmCancelId(null)}
                      className="min-h-9 rounded-lg border border-border px-3 text-xs font-semibold"
                    >
                      {t.keep}
                    </Button>
                    <Button
                      type="button"
                      onClick={() => void cancel(ticket)}
                      className="min-h-9 rounded-lg bg-destructive px-3 text-xs font-semibold text-destructive-foreground"
                    >
                      {t.confirmCancel}
                    </Button>
                  </>
                ) : (
                  <Button
                    type="button"
                    onClick={() => setConfirmCancelId(ticket.id)}
                    className="min-h-9 rounded-lg px-3 text-xs font-semibold text-destructive"
                  >
                    {t.cancel}
                  </Button>
                )}
              </div>
            </div>
          </article>
        ))}
      </div>
    </div>
  );
}

// Only orders still being made stay on the kitchen screen; a ready order leaves it.
function isActive(ticket: Ticket) {
  return (
    (ticket.status === "New" || ticket.status === "Preparing") &&
    ticket.dispatchStatus !== "Cancelled" &&
    ticket.dispatchStatus !== "Failed"
  );
}

let audio: AudioContext | null = null;
function audioContext() {
  try {
    audio ??= new AudioContext();
  } catch {
    return null;
  }
  return audio;
}
function audioRunning() {
  return audio?.state === "running";
}
async function unlockAudio() {
  const context = audioContext();
  if (context && context.state !== "running")
    await context.resume().catch(() => undefined);
  void loadOrderSound();
}

// The restaurant's own order sound (public/kds.mp3); swap the file to change it. Without it the
// generated bell below plays instead.
const orderSoundUrl = "/kds.mp3";
let orderSound: AudioBuffer | null = null;
let orderSoundLoading: Promise<void> | null = null;
function loadOrderSound() {
  const context = audioContext();
  if (!context || orderSound) return Promise.resolve();
  orderSoundLoading ??= fetch(orderSoundUrl)
    .then((response) => {
      if (!response.ok) throw new Error();
      return response.arrayBuffer();
    })
    .then((data) => context.decodeAudioData(data))
    .then((buffer) => {
      orderSound = buffer;
    })
    .catch(() => {
      orderSoundLoading = null;
    });
  return orderSoundLoading;
}

function playKitchenBell() {
  const context = audioContext();
  if (!context || context.state !== "running") return;
  if (orderSound) {
    const source = context.createBufferSource();
    source.buffer = orderSound;
    source.connect(context.destination);
    source.start();
    return;
  }
  void loadOrderSound();
  playGeneratedBell(context);
}

// Fallback order bell (ding-ding-DING, twice), generated in the browser. Each strike stacks bell-like
// partials that ring out and decay.
function playGeneratedBell(context: AudioContext) {
  const strike = (at: number, frequency: number) => {
    for (const [ratio, level] of [
      [1, 0.32],
      [2.76, 0.1],
      [5.4, 0.04],
    ]) {
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      oscillator.type = "sine";
      oscillator.frequency.value = frequency * ratio;
      gain.gain.setValueAtTime(0.0001, at);
      gain.gain.exponentialRampToValueAtTime(level, at + 0.006);
      gain.gain.exponentialRampToValueAtTime(0.0001, at + 0.9);
      oscillator.connect(gain).connect(context.destination);
      oscillator.start(at);
      oscillator.stop(at + 0.95);
    }
  };
  const start = context.currentTime + 0.02;
  for (const round of [0, 1.2])
    [1319, 1319, 1760].forEach((frequency, index) =>
      strike(start + round + index * 0.17, frequency),
    );
}
