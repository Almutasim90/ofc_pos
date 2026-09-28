import { useEffect, useRef, useState } from "react";
import {
  AlertTriangle,
  ChefHat,
  CheckCircle2,
  Clock3,
  Flame,
  LayoutGrid,
  RefreshCw,
  Send,
  UtensilsCrossed,
  Wifi,
  WifiOff,
} from "lucide-react";
import { FormDialog } from "@/app/FormDialog";
import { SearchableSelect } from "@/app/SearchableSelect";
import { createId, store } from "@/lib/local-store";
import { useReliableBranchHub } from "@/lib/reliable-hub";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Language = "ar" | "en";
type Branch = { id: string; nameAr: string; nameEn: string };
type Station = { id: string; code: string; nameAr: string; nameEn: string };
type Order = {
  id: string;
  status: string;
  grossAmount: number;
  createdAt: string;
};
type DispatchStatus =
  | "Pending"
  | "SentToKds"
  | "KdsAcknowledged"
  | "PrintFallbackPending"
  | "PrintedFallback"
  | "Failed"
  | "Cancelled";
type Channel = "Kds" | "PrintFallback" | "ManualFallback";
type ItemStatus = "New" | "Preparing" | "Ready" | "Completed" | "Cancelled";
type TicketStatus = "New" | "Preparing" | "Ready" | "Completed" | "Cancelled";
type TicketItem = {
  id: string;
  orderLineId: string | null;
  productId: string;
  productNameAr: string;
  productNameEn: string;
  quantity: number;
  note: string | null;
  selections: string;
  status: ItemStatus;
  startedAt: string | null;
  readyAt: string | null;
  completedAt: string | null;
};
type Ticket = {
  id: string;
  branchId: string;
  orderId: string;
  dispatchId: string;
  orderNumber: string;
  stationId: string | null;
  orderType: string | null;
  orderTypeNameAr: string | null;
  orderTypeNameEn: string | null;
  stationCode: string | null;
  stationNameAr: string | null;
  stationNameEn: string | null;
  dispatchStatus: DispatchStatus;
  channel: Channel;
  status: TicketStatus;
  targetMinutes: number | null;
  kdsAttempts: number;
  fallbackPrinted: boolean;
  lastError: string | null;
  note: string | null;
  wasPrepStartedBeforeCancellation: boolean;
  cancellationNotified: boolean;
  createdAt: string;
  startedAt: string | null;
  readyAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  acknowledgedAt: string | null;
  fallbackPrintedAt: string | null;
  updatedAt: string;
  overdue: boolean;
  items: TicketItem[];
};

const copy = {
  ar: {
    title: "مطبخ KDS",
    intro:
      "عرض طلبات المطبخ حسب محطة التحضير، مع تتبع الحالة وأوقات التحضير والطباعة الاحتياطية عند تعذر KDS.",
    isolated: "المطبخ يستمر حتى مع انقطاع الخدمة",
    fallbackNote: "OFFLINE / FALLBACK TICKET",
    branch: "الفرع",
    station: "محطة التحضير",
    allStations: "كل المحطات",
    kdsOn: "KDS متصل",
    kdsOff: "KDS غير متصل",
    loading: "جارٍ التحميل",
    reload: "تحديث",
    empty: "لا توجد تذاكر مطبخ لهذه المحطة.",
    dispatch: "إرسال طلب للمطبخ",
    order: "الطلب",
    targetMinutes: "الوقت المستهدف (دقيقة)",
    dispatchAction: "إرسال",
    dispatchNote:
      "أرسل الطلب من نقطة البيع بزر «إرسال للمطبخ». تظهر أصناف الشواية للشواية والمشروبات لقسم المشروبات، حسب مكان التحضير المحدد عند تعديل المنتج. المنتج دون مكان محدد يظهر في المطبخ العام.",
    send: "إرسال إلى KDS",
    ack: "استلام",
    newTicket: "جديد",
    fallback: "طباعة احتياطية",
    fail: "فشل",
    printed: "تمت الطباعة",
    cancel: "إلغاء",
    confirmCancel: "تأكيد الإلغاء",
    cancelPrompt: "هل أنت متأكد من إلغاء طلب المطبخ؟",
    start: "بدء",
    ready: "جاهز",
    complete: "اكتمل",
    cancelled: "ملغى",
    overdue: "متأخر",
    fallbackBadge: "احتياطي",
    note: "ملاحظة",
    quantity: "كمية",
    orderNo: "طلب",
    dineIn: "محلي",
    takeaway: "سفري",
    served: "تم التسليم",
    dispatched: "تم الإرسال",
    saved: "تم الحفظ.",
    failed: "تعذر تنفيذ العملية.",
    stPending: "قيد الإنشاء",
    stSentToKds: "مرسل إلى KDS",
    stKdsAcknowledged: "مستلم",
    stPrintFallbackPending: "طباعة احتياطية...",
    stPrintedFallback: "مطبوع احتياطيًا",
    stFailed: "فاشل",
    stCancelled: "ملغى",
    itNew: "جديد",
    itPreparing: "قيد التحضير",
    itReady: "جاهز",
    itCompleted: "مكتمل",
    itCancelled: "ملغى",
    queue: "قائمة التحضير",
    activeOrders: "الطلبات النشطة",
    preparingNow: "قيد التحضير",
    readyNow: "جاهزة للتسليم",
    lateOrders: "طلبات متأخرة",
    stationsLabel: "تصفية حسب المحطة",
    items: "أصناف",
    elapsed: "مضت",
    min: "د",
    progress: "تقدم الطلب",
    noTicketsTitle: "المطبخ هادئ الآن",
    noTicketsHint: "ستظهر الطلبات الجديدة هنا فور إرسالها من نقطة البيع.",
  },
  en: {
    title: "Kitchen KDS",
    intro:
      "Review kitchen tickets by preparation station, track status and prep times, and print a fallback ticket when the KDS is unavailable.",
    isolated: "Kitchen keeps working even when the service is offline",
    fallbackNote: "OFFLINE / FALLBACK TICKET",
    branch: "Branch",
    station: "Preparation station",
    allStations: "All stations",
    kdsOn: "KDS online",
    kdsOff: "KDS offline",
    loading: "Loading",
    reload: "Refresh",
    empty: "No kitchen tickets for this station.",
    dispatch: "Dispatch an order to the kitchen",
    order: "Order",
    targetMinutes: "Target time (min)",
    dispatchAction: "Dispatch",
    dispatchNote: "Items route to their station automatically via the product.",
    send: "Send to KDS",
    ack: "Received",
    newTicket: "New",
    fallback: "Print fallback",
    fail: "Fail",
    printed: "Mark printed",
    cancel: "Cancel",
    confirmCancel: "Confirm cancel",
    cancelPrompt: "Are you sure you want to cancel this kitchen order?",
    start: "Start",
    ready: "Ready",
    complete: "Complete",
    cancelled: "Cancelled",
    overdue: "Overdue",
    fallbackBadge: "Fallback",
    note: "Note",
    quantity: "Qty",
    orderNo: "Order",
    dineIn: "Dine-in",
    takeaway: "Takeaway",
    served: "Served",
    dispatched: "Dispatched",
    saved: "Saved.",
    failed: "Unable to complete the operation.",
    stPending: "Pending",
    stSentToKds: "Sent to KDS",
    stKdsAcknowledged: "Acknowledged",
    stPrintFallbackPending: "Fallback printing…",
    stPrintedFallback: "Printed fallback",
    stFailed: "Failed",
    stCancelled: "Cancelled",
    itNew: "New",
    itPreparing: "Preparing",
    itReady: "Ready",
    itCompleted: "Completed",
    itCancelled: "Cancelled",
    queue: "Preparation queue",
    activeOrders: "Active orders",
    preparingNow: "Preparing",
    readyNow: "Ready to serve",
    lateOrders: "Overdue orders",
    stationsLabel: "Filter by station",
    items: "Items",
    elapsed: "Elapsed",
    min: "m",
    progress: "Order progress",
    noTicketsTitle: "The kitchen is clear",
    noTicketsHint:
      "New tickets will appear here as soon as they are sent from POS.",
  },
} as const;

export function KitchenSection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) =>
    language === "ar" ? x.nameAr : x.nameEn;
  const dispatchLabel = (s: DispatchStatus) =>
    s === "Pending"
      ? t.stPending
      : s === "SentToKds"
        ? t.stSentToKds
        : s === "KdsAcknowledged"
          ? t.stKdsAcknowledged
          : s === "PrintFallbackPending"
            ? t.stPrintFallbackPending
            : s === "PrintedFallback"
              ? t.stPrintedFallback
              : s === "Failed"
                ? t.stFailed
                : t.stCancelled;
  const itemLabel = (s: ItemStatus) =>
    s === "New"
      ? t.itNew
      : s === "Preparing"
        ? t.itPreparing
        : s === "Ready"
          ? t.itReady
          : s === "Completed"
            ? t.itCompleted
            : t.itCancelled;
  const statusPill = (s: DispatchStatus) =>
    s === "PrintedFallback" || s === "PrintFallbackPending"
      ? "bg-warning/15 text-warning"
      : s === "Failed" || s === "Cancelled"
        ? "bg-destructive/15 text-destructive"
        : s === "KdsAcknowledged"
          ? "bg-success/15 text-success"
          : "bg-muted text-muted-foreground";

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
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);
  const [dispatchForm, setDispatchForm] = useState({
    orderId: "",
    targetMinutes: "",
  });
  const [showDispatch, setShowDispatch] = useState(false);
  const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);

  const setMsg = (value: string, error = false) => {
    setMessage(value);
    setIsError(error);
  };

  // Tickets already on screen; a new unacknowledged one rings so a busy kitchen notices it.
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
      const next = response.ok ? ((await response.json()) as Ticket[]) : [];
      const seen = seenTickets.current;
      if (
        seen &&
        next.some((x) => x.dispatchStatus === "SentToKds" && !seen.has(x.id))
      )
        playNewTicketChime();
      seenTickets.current = new Set(next.map((x) => x.id));
      setTickets(next);
      acknowledgeShown(next);
    } finally {
      setLoading(false);
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

  async function loadContext() {
    const [branchesResponse, contextResponse] = await Promise.all([
      auth("/api/v1/pos/context"),
      auth("/api/v1/kitchen/stations"),
    ]);
    if (branchesResponse.ok) {
      const value = (await branchesResponse.json()) as { branches: Branch[] };
      setBranches(value.branches);
      if (value.branches[0]) setBranchId(value.branches[0].id);
    }
    if (contextResponse.ok)
      setStations((await contextResponse.json()) as Station[]);
  }

  useEffect(() => {
    void loadContext();
  }, []);
  useEffect(() => {
    if (branchId) void refresh();
  }, [branchId, stationId]);

  // SignalR is realtime transport only, not the source of truth (docs/01-ARCHITECTURE-GUARDRAILS.md):
  // the hub just tells this screen something changed for the branch, and it re-fetches the ticket list
  // over the existing REST endpoint below instead of trusting ticket state carried over the socket.
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

  async function refresh() {
    setMsg("");
    const queuePromise = loadTickets();
    if (!branchId) return;
    const ordersResponse = await auth(`/api/v1/orders?branchId=${branchId}`);
    if (ordersResponse.ok)
      setOrders(
        ((await ordersResponse.json()) as Order[]).filter(
          (o) => o.status !== "Cancelled" && o.status !== "Rejected",
        ),
      );
    await queuePromise;
  }

  async function dispatch(event: React.FormEvent) {
    event.preventDefault();
    setMsg("");
    if (!branchId || !dispatchForm.orderId) {
      setMsg(t.failed, true);
      return;
    }
    try {
      const body = {
        branchId,
        orderId: dispatchForm.orderId,
        clientDispatchId: createId(),
        orderNumber: null,
        note: null,
        targetMinutes:
          dispatchForm.targetMinutes === ""
            ? null
            : Number(dispatchForm.targetMinutes),
      };
      const response = await auth("/api/v1/kitchen/tickets", {
        method: "POST",
        body: JSON.stringify(body),
      });
      if (!response.ok) throw new Error(t.failed);
      setMsg(t.dispatched);
      setDispatchForm({ orderId: "", targetMinutes: "" });
      setShowDispatch(false);
      void refresh();
    } catch {
      setMsg(t.failed, true);
    }
  }

  async function act(
    ticket: Ticket,
    action: "send" | "ack" | "ready" | "fallback" | "printed" | "fail",
  ) {
    setMsg("");
    const id = ticket.id;
    try {
      const url =
        action === "ready"
          ? `/api/v1/kitchen/tickets/${id}/ready`
          : action === "printed"
          ? `/api/v1/kitchen/tickets/${id}/printed`
          : action === "fail"
            ? `/api/v1/kitchen/tickets/${id}/fail`
            : action === "fallback"
              ? `/api/v1/kitchen/tickets/${id}/fallback`
              : action === "ack"
                ? `/api/v1/kitchen/tickets/${id}/ack`
                : `/api/v1/kitchen/tickets/${id}/send`;
      const body =
        action === "fallback"
          ? { error: "KDS unavailable", manual: false, templateCode: null }
          : action === "fail"
            ? { error: t.failed }
            : undefined;
      const response = await auth(url, {
        method: "POST",
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        setMsg(problem?.errors?.ticket?.[0] ?? t.failed, true);
        return;
      }
      void loadTickets();
    } catch {
      setMsg(t.failed, true);
    }
  }

  // One tap clears a ready order off the screen once it has been handed over.
  async function served(ticket: Ticket) {
    setMsg("");
    try {
      const results = await Promise.all(
        ticket.items
          .filter((item) => item.status === "Ready")
          .map((item) =>
            auth(`/api/v1/kitchen/tickets/${ticket.id}/item/${item.id}`, {
              method: "PUT",
              body: JSON.stringify({ status: "Completed" }),
            }),
          ),
      );
      if (results.some((response) => !response.ok)) setMsg(t.failed, true);
    } catch {
      setMsg(t.failed, true);
    }
    void loadTickets();
  }

  async function cancel(id: string) {
    setMsg("");
    try {
      const response = await auth(`/api/v1/kitchen/tickets/${id}/cancel`, {
        method: "POST",
        body: JSON.stringify({ note: null, templateCode: null }),
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        setMsg(problem?.errors?.ticket?.[0] ?? t.failed, true);
        return;
      }
      setConfirmCancelId(null);
      await loadTickets();
    } catch {
      setMsg(t.failed, true);
    }
  }

  const elapsedMinutes = (createdAt: string) =>
    Math.max(
      0,
      Math.floor((Date.now() - new Date(createdAt).getTime()) / 60_000),
    );
  // Tickets are confirmed the moment they appear, so "new" is the first minute on screen.
  const isNew = (ticket: Ticket) =>
    ticket.dispatchStatus === "SentToKds" ||
    (elapsedMinutes(ticket.createdAt) < 1 &&
      (ticket.status === "New" || ticket.status === "Preparing"));
  const preparingCount = tickets.filter(
    (ticket) =>
      ticket.status === "Preparing" ||
      ticket.items.some((item) => item.status === "Preparing"),
  ).length;
  const readyCount = tickets.filter(
    (ticket) =>
      ticket.status === "Ready" ||
      (ticket.items.length > 0 &&
        ticket.items.every(
          (item) => item.status === "Ready" || item.status === "Completed",
        )),
  ).length;
  const overdueCount = tickets.filter((ticket) => ticket.overdue).length;

  return (
    <div className="space-y-6">
      <div>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">
            {t.title}
          </h1>
          <span
            className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-bold ${live ? "bg-success/15 text-success" : "bg-destructive/15 text-destructive"}`}
          >
            {live ? <Wifi size={13} /> : <WifiOff size={13} />}
            {live ? t.kdsOn : t.kdsOff}
          </span>
        </div>
        <p className="mt-3 max-w-3xl text-muted-foreground">{t.intro}</p>
      </div>

      <div className="flex flex-wrap items-end gap-3 rounded-xl border border-border bg-accent p-3">
        <div className="min-w-56">
          <SearchableSelect
            label={t.branch}
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
        <Button
          onClick={() => void refresh()}
          aria-label={t.reload}
          className="inline-flex min-h-10 items-center gap-2 rounded-lg border border-border bg-card px-4 text-xs font-semibold text-muted-foreground"
        >
          <RefreshCw className={loading ? "animate-spin" : ""} size={15} />
          {t.reload}
        </Button>
        <Button
          onClick={() => setShowDispatch(true)}
          className="inline-flex min-h-10 items-center gap-2 rounded-lg bg-primary px-4 text-xs font-semibold text-primary-foreground hover:bg-primary"
        >
          <Send size={15} />
          {t.dispatchAction}
        </Button>
      </div>

      <section
        aria-label={t.activeOrders}
        className="grid grid-cols-2 gap-3 xl:grid-cols-4"
      >
        {[
          {
            label: t.activeOrders,
            value: tickets.length,
            icon: LayoutGrid,
            tone: "bg-accent text-primary",
          },
          {
            label: t.preparingNow,
            value: preparingCount,
            icon: Flame,
            tone: "bg-warning/15 text-warning",
          },
          {
            label: t.readyNow,
            value: readyCount,
            icon: CheckCircle2,
            tone: "bg-success/15 text-success",
          },
          {
            label: t.lateOrders,
            value: overdueCount,
            icon: AlertTriangle,
            tone: "bg-destructive/15 text-destructive",
          },
        ].map((metric) => (
          <div
            key={metric.label}
            className="flex items-center gap-3 rounded-xl border border-border bg-card p-4 sm:p-5"
          >
            <span
              className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl ${metric.tone}`}
            >
              <metric.icon size={21} />
            </span>
            <div>
              <p className="text-2xl font-semibold leading-none text-muted-foreground">
                {metric.value}
              </p>
              <p className="mt-1.5 text-xs font-medium text-muted-foreground sm:text-sm">
                {metric.label}
              </p>
            </div>
          </div>
        ))}
      </section>

      <section className="rounded-xl border border-border bg-card p-3 sm:p-4">
        <div className="mb-3 flex items-center gap-2 px-1 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          <UtensilsCrossed size={15} />
          {t.stationsLabel}
        </div>
        <div className="flex gap-2 overflow-x-auto pb-1">
          <Button
            onClick={() => setStationId("")}
            className={`min-h-10 shrink-0 rounded-lg px-4 text-sm font-medium transition ${stationId === "" ? "bg-primary text-primary-foreground" : "border border-border text-muted-foreground hover:bg-accent"}`}
          >
            {t.allStations}
            <span className="ms-2 rounded-full bg-black/10 px-2 py-0.5 text-[11px]">
              {tickets.length}
            </span>
          </Button>
          {stations.map((station) => (
            <Button
              key={station.id}
              onClick={() => setStationId(station.id)}
              className={`min-h-10 shrink-0 rounded-lg px-4 text-sm font-medium transition ${stationId === station.id ? "bg-primary text-primary-foreground" : "border border-border text-muted-foreground hover:bg-accent"}`}
            >
              <span className="me-2 text-[11px] opacity-60">
                {station.code}
              </span>
              {name(station)}
            </Button>
          ))}
        </div>
      </section>

      {message && (
        <p
          role={isError ? "alert" : "status"}
          className={`rounded-xl border px-4 py-3 text-sm font-semibold ${isError ? "border-destructive/40 bg-destructive/10 text-destructive" : "border-border bg-success/15 text-success"}`}
        >
          {message}
        </p>
      )}

      {showDispatch && (
        <FormDialog
          title={t.dispatch}
          closeLabel={t.cancel}
          onClose={() => setShowDispatch(false)}
          width="max-w-xl"
        >
          <p className="text-sm text-muted-foreground">{t.dispatchNote}</p>
          <form onSubmit={dispatch} className="mt-4 grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <SearchableSelect
                label={t.order}
                value={dispatchForm.orderId}
                onChange={(v) =>
                  setDispatchForm({ ...dispatchForm, orderId: v })
                }
              >
                {orders.length === 0 && <option value="">{t.empty}</option>}
                {orders.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.status} · {o.grossAmount}
                  </option>
                ))}
              </SearchableSelect>
            </div>
            <label className="block text-sm font-medium">
              {t.targetMinutes}
              <Input
                type="number"
                value={dispatchForm.targetMinutes}
                onChange={(e) =>
                  setDispatchForm({
                    ...dispatchForm,
                    targetMinutes: e.target.value,
                  })
                }
                min={1}
                max={999}
                className="mt-2 min-h-12 w-full rounded-lg border border-border px-3"
              />
            </label>
            <Button
              disabled={loading}
              className="mt-1 inline-flex min-h-11 items-center gap-2 justify-self-start rounded-lg bg-primary px-4 font-semibold text-primary-foreground hover:bg-primary disabled:opacity-60"
            >
              <Send size={18} />
              {t.dispatchAction}
            </Button>
          </form>
        </FormDialog>
      )}

      {loading && (
        <div className="flex items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-card p-10 font-semibold text-muted-foreground">
          <RefreshCw className="animate-spin text-primary" size={22} />
          {t.loading}
        </div>
      )}

      {!loading && tickets.length === 0 && (
        <div className="rounded-2xl border border-dashed border-border bg-card px-6 py-14 text-center">
          <span className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-accent text-primary">
            <ChefHat size={30} />
          </span>
          <h2 className="mt-5 text-xl font-semibold text-muted-foreground">
            {t.noTicketsTitle}
          </h2>
          <p className="mx-auto mt-2 max-w-md text-sm leading-6 text-muted-foreground">
            {t.noTicketsHint}
          </p>
        </div>
      )}

      {!loading && tickets.length > 0 && (
        <div className="flex items-end justify-between gap-3">
          <div>
            <p className="text-xs font-bold uppercase tracking-[0.16em] text-primary">
              {t.queue}
            </p>
            <h2 className="mt-1 text-xl font-semibold text-muted-foreground">
              {stationId
                ? name(
                    stations.find((station) => station.id === stationId) ?? {
                      nameAr: t.station,
                      nameEn: t.station,
                    },
                  )
                : t.allStations}
            </h2>
          </div>
          <span className="rounded-full bg-muted px-3 py-1.5 text-xs font-bold text-muted-foreground">
            {tickets.length} {t.activeOrders}
          </span>
        </div>
      )}

      <div className="grid items-start gap-4 lg:grid-cols-2 2xl:grid-cols-3">
        {tickets.map((ticket) => (
          <article
            key={ticket.id}
            className={`group relative overflow-hidden rounded-xl border bg-card transition hover:-translate-y-0.5 ${isNew(ticket) ? "border-primary ring-2 ring-primary/40" : "border-border"}`}
          >
            <div
              className={`h-1.5 w-full ${ticket.overdue ? "bg-destructive" : ticket.status === "Ready" ? "bg-success" : ticket.status === "Preparing" ? "bg-warning" : "bg-primary"}`}
            />
            <div className="p-4 sm:p-5">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-xs font-bold uppercase tracking-wide text-muted-foreground">
                    {t.orderNo}
                  </p>
                  <p className="mt-0.5 flex items-center gap-2 text-2xl font-black tracking-tight text-foreground">
                    #{ticket.orderNumber}
                    {ticket.orderType && (
                      <span
                        className={`rounded-full px-3 py-0.5 text-sm font-black ${ticket.orderType === "Takeaway" ? "bg-warning text-warning-foreground" : ticket.orderType === "DineIn" || ticket.orderType === "InStore" ? "bg-success text-success-foreground" : "bg-secondary text-secondary-foreground"}`}
                      >
                        {ticket.orderType === "Takeaway"
                          ? t.takeaway
                          : ticket.orderType === "DineIn"
                            ? t.dineIn
                            : language === "ar"
                              ? ticket.orderTypeNameAr
                              : ticket.orderTypeNameEn}
                      </span>
                    )}
                    {isNew(ticket) && (
                      <span className="animate-pulse rounded-full bg-primary px-2.5 py-0.5 text-xs font-bold text-primary-foreground">
                        {t.newTicket}
                      </span>
                    )}
                  </p>
                </div>
                <div className="flex flex-wrap items-center justify-end gap-1.5">
                  {ticket.fallbackPrinted && (
                    <span className="rounded-full bg-warning/15 px-2 py-1 text-xs font-semibold text-warning">
                      {t.fallbackBadge}
                    </span>
                  )}
                  {ticket.overdue && (
                    <span className="rounded-full bg-destructive/15 px-2 py-1 text-xs font-semibold text-destructive">
                      {t.overdue}
                    </span>
                  )}
                  <span
                    className={`rounded-full px-2.5 py-1 text-xs font-bold ${statusPill(ticket.dispatchStatus)}`}
                  >
                    {dispatchLabel(ticket.dispatchStatus)}
                  </span>
                </div>
              </div>
              <div className="mt-4 grid grid-cols-3 divide-x divide-border rounded-xl bg-accent px-2 py-3 text-center rtl:divide-x-reverse">
                <div>
                  <Clock3 className="mx-auto text-muted-foreground" size={16} />
                  <p
                    className={`mt-1 text-sm font-bold ${ticket.overdue ? "text-destructive" : "text-muted-foreground"}`}
                  >
                    {elapsedMinutes(ticket.createdAt)} {t.min}
                  </p>
                  <p className="text-[10px] font-semibold text-muted-foreground">
                    {t.elapsed}
                  </p>
                </div>
                <div>
                  <UtensilsCrossed
                    className="mx-auto text-muted-foreground"
                    size={16}
                  />
                  <p className="mt-1 text-sm font-bold text-muted-foreground">
                    {ticket.items.length}
                  </p>
                  <p className="text-[10px] font-semibold text-muted-foreground">
                    {t.items}
                  </p>
                </div>
                <div>
                  <ChefHat
                    className="mx-auto text-muted-foreground"
                    size={16}
                  />
                  <p className="mt-1 truncate px-1 text-sm font-bold text-muted-foreground">
                    {ticket.stationCode ?? "—"}
                  </p>
                  <p className="text-[10px] font-semibold text-muted-foreground">
                    {t.station}
                  </p>
                </div>
              </div>
              {ticket.stationNameEn && (
                <p className="mt-3 text-xs font-bold text-primary">
                  {language === "ar"
                    ? ticket.stationNameAr
                    : ticket.stationNameEn}
                </p>
              )}
              {ticket.dispatchStatus === "PrintFallbackPending" && (
                <p className="mt-2 rounded-lg bg-warning/15 px-3 py-2 text-xs font-semibold text-warning">
                  {t.fallbackNote}
                </p>
              )}
              {ticket.lastError && (
                <p className="mt-2 text-xs text-destructive">
                  {ticket.lastError}
                </p>
              )}

              <div className="mt-4 flex items-center justify-between text-[11px] font-bold text-muted-foreground">
                <span>{t.progress}</span>
                <span>
                  {
                    ticket.items.filter((item) => item.status === "Completed")
                      .length
                  }
                  /{ticket.items.length}
                </span>
              </div>
              <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-muted">
                <div
                  className="h-full rounded-full bg-success transition-all"
                  style={{
                    width: `${ticket.items.length ? (ticket.items.filter((item) => item.status === "Completed").length / ticket.items.length) * 100 : 0}%`,
                  }}
                />
              </div>

              <ul className="mt-4 space-y-2">
                {ticket.items.map((item) => (
                  <li
                    key={item.id}
                    className={`rounded-xl border border-border p-3 ${item.status === "Completed" ? "bg-success/15" : item.status === "Cancelled" ? "bg-destructive/15" : item.status === "Preparing" ? "bg-warning/15" : "bg-accent"}`}
                  >
                    <div className="flex items-center justify-between gap-3">
                      <span className="min-w-0 font-bold text-muted-foreground">
                        {language === "ar"
                          ? item.productNameAr
                          : item.productNameEn}
                      </span>
                      <span className="grid h-7 min-w-7 shrink-0 place-items-center rounded-lg bg-card px-2 text-xs font-bold text-muted-foreground">
                        ×{item.quantity}
                      </span>
                    </div>
                    {item.note && (
                      <p className="mt-2 rounded-lg bg-card px-2 py-1.5 text-xs text-warning">
                        {t.note}: {item.note}
                      </p>
                    )}
                    <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                      <span
                        className={`rounded-full px-2 py-0.5 text-[11px] font-bold ${item.status === "Completed" ? "bg-success/15 text-success" : item.status === "Preparing" ? "bg-warning/15 text-warning" : item.status === "Cancelled" ? "bg-destructive/15 text-destructive" : "bg-muted text-muted-foreground"}`}
                      >
                        {itemLabel(item.status)}
                      </span>
                    </div>
                  </li>
                ))}
              </ul>

              <div className="mt-4 flex flex-wrap gap-2 border-t border-border pt-4">
                {ticket.dispatchStatus === "Pending" && (
                  <Button
                    onClick={() => void act(ticket, "send")}
                    className="min-h-10 rounded-lg bg-primary px-3 text-sm font-semibold text-primary-foreground"
                  >
                    {t.send}
                  </Button>
                )}
                {(ticket.dispatchStatus === "SentToKds" ||
                  ticket.dispatchStatus === "KdsAcknowledged" ||
                  ticket.dispatchStatus === "PrintedFallback") &&
                  (ticket.status === "New" ||
                    ticket.status === "Preparing") && (
                    <Button
                      onClick={() => void act(ticket, "ready")}
                      className="min-h-14 flex-1 rounded-lg bg-success px-4 text-lg font-black text-success-foreground"
                    >
                      <CheckCircle2 size={20} />
                      {t.ready}
                    </Button>
                  )}
                {ticket.status === "Ready" && (
                  <Button
                    onClick={() => void served(ticket)}
                    className="min-h-14 flex-1 rounded-lg bg-primary px-4 text-lg font-black text-primary-foreground"
                  >
                    {t.served}
                  </Button>
                )}
                {ticket.dispatchStatus === "SentToKds" && (
                  <Button
                    onClick={() => void act(ticket, "fallback")}
                    className="min-h-10 rounded-lg border border-border px-3 text-sm font-semibold text-muted-foreground"
                  >
                    {t.fallback}
                  </Button>
                )}
                {ticket.dispatchStatus === "PrintFallbackPending" && (
                  <Button
                    onClick={() => void act(ticket, "printed")}
                    className="min-h-10 rounded-lg bg-success px-3 text-sm font-semibold text-primary-foreground"
                  >
                    {t.printed}
                  </Button>
                )}
                {ticket.dispatchStatus === "PrintFallbackPending" && (
                  <Button
                    onClick={() => void act(ticket, "fail")}
                    className="min-h-10 rounded-lg border border-destructive px-3 text-sm font-semibold text-destructive"
                  >
                    {t.fail}
                  </Button>
                )}
                {confirmCancelId === ticket.id ? (
                  <>
                    <Button
                      onClick={() => void cancel(ticket.id)}
                      className="min-h-10 rounded-lg bg-destructive px-3 text-sm font-semibold text-primary-foreground"
                    >
                      {t.confirmCancel}
                    </Button>
                    <Button
                      onClick={() => setConfirmCancelId(null)}
                      className="min-h-10 rounded-lg border border-border px-3 text-sm font-semibold"
                    >
                      {t.cancel}
                    </Button>
                  </>
                ) : (
                  <Button
                    onClick={() => setConfirmCancelId(ticket.id)}
                    className="min-h-10 rounded-lg border border-destructive px-3 text-sm font-semibold text-destructive"
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

// A short two-tone chime generated in the browser, so no audio file has to ship with the app.
function playNewTicketChime() {
  try {
    const context = new AudioContext();
    [880, 1320].forEach((frequency, index) => {
      const oscillator = context.createOscillator();
      const gain = context.createGain();
      oscillator.frequency.value = frequency;
      const start = context.currentTime + index * 0.18;
      gain.gain.setValueAtTime(0.0001, start);
      gain.gain.exponentialRampToValueAtTime(0.3, start + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.16);
      oscillator.connect(gain).connect(context.destination);
      oscillator.start(start);
      oscillator.stop(start + 0.18);
    });
    setTimeout(() => void context.close(), 600);
  } catch {
    /* Audio may be blocked until the screen has been tapped once; the visual badge still shows. */
  }
}
