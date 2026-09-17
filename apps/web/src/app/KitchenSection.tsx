import { useEffect, useRef, useState } from "react";
import { AlertTriangle, ChefHat, CheckCircle2, Clock3, Flame, LayoutGrid, RefreshCw, Send, UtensilsCrossed, Wifi, WifiOff } from "lucide-react";
import { FormDialog } from "@/app/FormDialog";
import { SearchableSelect } from "@/app/SearchableSelect";
import { createId, store } from "@/lib/local-store";
import { useReliableBranchHub } from "@/lib/reliable-hub";

type Language = "ar" | "en";
type Branch = { id: string; nameAr: string; nameEn: string };
type Station = { id: string; code: string; nameAr: string; nameEn: string };
type Order = { id: string; status: string; grossAmount: number; createdAt: string };
type DispatchStatus = "Pending" | "SentToKds" | "KdsAcknowledged" | "PrintFallbackPending" | "PrintedFallback" | "Failed" | "Cancelled";
type Channel = "Kds" | "PrintFallback" | "ManualFallback";
type ItemStatus = "New" | "Preparing" | "Ready" | "Completed" | "Cancelled";
type TicketStatus = "New" | "Preparing" | "Ready" | "Completed" | "Cancelled";
type TicketItem = { id: string; orderLineId: string | null; productId: string; productNameAr: string; productNameEn: string; quantity: number; note: string | null; selections: string; status: ItemStatus; startedAt: string | null; readyAt: string | null; completedAt: string | null };
type Ticket = { id: string; branchId: string; orderId: string; dispatchId: string; orderNumber: string; stationId: string | null; stationCode: string | null; stationNameAr: string | null; stationNameEn: string | null; dispatchStatus: DispatchStatus; channel: Channel; status: TicketStatus; targetMinutes: number | null; kdsAttempts: number; fallbackPrinted: boolean; lastError: string | null; note: string | null; wasPrepStartedBeforeCancellation: boolean; cancellationNotified: boolean; createdAt: string; startedAt: string | null; readyAt: string | null; completedAt: string | null; cancelledAt: string | null; acknowledgedAt: string | null; fallbackPrintedAt: string | null; updatedAt: string; overdue: boolean; items: TicketItem[] };

const copy = {
  ar: {
    title: "مطبخ KDS", intro: "عرض طلبات المطبخ حسب محطة التحضير، مع تتبع الحالة وأوقات التحضير والطباعة الاحتياطية عند تعذر KDS.", isolated: "المطبخ يستمر حتى مع انقطاع الخدمة", fallbackNote: "OFFLINE / FALLBACK TICKET",
    branch: "الفرع", station: "محطة التحضير", allStations: "كل المحطات", kdsOn: "KDS متصل", kdsOff: "KDS غير متصل", loading: "جارٍ التحميل", reload: "تحديث", empty: "لا توجد تذاكر مطبخ لهذه المحطة.", dispatch: "إرسال طلب للمطبخ", order: "الطلب", targetMinutes: "الوقت المستهدف (دقيقة)", dispatchAction: "إرسال", dispatchNote: "أرسل الطلب من نقطة البيع بزر «إرسال للمطبخ». تظهر أصناف الشواية للشواية والمشروبات لقسم المشروبات، حسب مكان التحضير المحدد عند تعديل المنتج. المنتج دون مكان محدد يظهر في المطبخ العام.",
    send: "إرسال إلى KDS", ack: "استلام/بدء التحضير", fallback: "طباعة احتياطية", fail: "فشل", printed: "تمت الطباعة", cancel: "إلغاء", confirmCancel: "تأكيد الإلغاء", cancelPrompt: "هل أنت متأكد من إلغاء طلب المطبخ؟",
    start: "بدء", ready: "جاهز", complete: "اكتمل", cancelled: "ملغى", overdue: "متأخر", fallbackBadge: "احتياطي", note: "ملاحظة", quantity: "كمية", orderNo: "طلب", dispatched: "تم الإرسال", saved: "تم الحفظ.", failed: "تعذر تنفيذ العملية.",
    stPending: "قيد الإنشاء", stSentToKds: "مرسل إلى KDS", stKdsAcknowledged: "مستلم", stPrintFallbackPending: "طباعة احتياطية...", stPrintedFallback: "مطبوع احتياطيًا", stFailed: "فاشل", stCancelled: "ملغى",
    itNew: "جديد", itPreparing: "قيد التحضير", itReady: "جاهز", itCompleted: "مكتمل", itCancelled: "ملغى",
    queue: "قائمة التحضير", activeOrders: "الطلبات النشطة", preparingNow: "قيد التحضير", readyNow: "جاهزة للتسليم", lateOrders: "طلبات متأخرة", stationsLabel: "تصفية حسب المحطة", items: "أصناف", elapsed: "مضت", min: "د", progress: "تقدم الطلب", noTicketsTitle: "المطبخ هادئ الآن", noTicketsHint: "ستظهر الطلبات الجديدة هنا فور إرسالها من نقطة البيع."
  },
  en: {
    title: "Kitchen KDS", intro: "Review kitchen tickets by preparation station, track status and prep times, and print a fallback ticket when the KDS is unavailable.", isolated: "Kitchen keeps working even when the service is offline", fallbackNote: "OFFLINE / FALLBACK TICKET",
    branch: "Branch", station: "Preparation station", allStations: "All stations", kdsOn: "KDS online", kdsOff: "KDS offline", loading: "Loading", reload: "Refresh", empty: "No kitchen tickets for this station.", dispatch: "Dispatch an order to the kitchen", order: "Order", targetMinutes: "Target time (min)", dispatchAction: "Dispatch", dispatchNote: "Items route to their station automatically via the product.",
    send: "Send to KDS", ack: "Acknowledge / start prep", fallback: "Print fallback", fail: "Fail", printed: "Mark printed", cancel: "Cancel", confirmCancel: "Confirm cancel", cancelPrompt: "Are you sure you want to cancel this kitchen order?",
    start: "Start", ready: "Ready", complete: "Complete", cancelled: "Cancelled", overdue: "Overdue", fallbackBadge: "Fallback", note: "Note", quantity: "Qty", orderNo: "Order", dispatched: "Dispatched", saved: "Saved.", failed: "Unable to complete the operation.",
    stPending: "Pending", stSentToKds: "Sent to KDS", stKdsAcknowledged: "Acknowledged", stPrintFallbackPending: "Fallback printing…", stPrintedFallback: "Printed fallback", stFailed: "Failed", stCancelled: "Cancelled",
    itNew: "New", itPreparing: "Preparing", itReady: "Ready", itCompleted: "Completed", itCancelled: "Cancelled",
    queue: "Preparation queue", activeOrders: "Active orders", preparingNow: "Preparing", readyNow: "Ready to serve", lateOrders: "Overdue orders", stationsLabel: "Filter by station", items: "Items", elapsed: "Elapsed", min: "m", progress: "Order progress", noTicketsTitle: "The kitchen is clear", noTicketsHint: "New tickets will appear here as soon as they are sent from POS."
  },
} as const;

export function KitchenSection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) => (language === "ar" ? x.nameAr : x.nameEn);
  const dispatchLabel = (s: DispatchStatus) => s === "Pending" ? t.stPending : s === "SentToKds" ? t.stSentToKds : s === "KdsAcknowledged" ? t.stKdsAcknowledged : s === "PrintFallbackPending" ? t.stPrintFallbackPending : s === "PrintedFallback" ? t.stPrintedFallback : s === "Failed" ? t.stFailed : t.stCancelled;
  const itemLabel = (s: ItemStatus) => s === "New" ? t.itNew : s === "Preparing" ? t.itPreparing : s === "Ready" ? t.itReady : s === "Completed" ? t.itCompleted : t.itCancelled;
  const statusPill = (s: DispatchStatus) => s === "PrintedFallback" || s === "PrintFallbackPending" ? "bg-[#f4f1e3] text-[#8a6d1f]" : s === "Failed" || s === "Cancelled" ? "bg-[#fbe4e2] text-[#b4322a]" : s === "KdsAcknowledged" ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#e8ece8] text-[#000000]";

  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });

  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [stations, setStations] = useState<Station[]>([]);
  const [stationId, setStationId] = useState("");
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);
  const [dispatchForm, setDispatchForm] = useState({ orderId: "", targetMinutes: "" });
  const [showDispatch, setShowDispatch] = useState(false);
  const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);

  const setMsg = (value: string, error = false) => { setMessage(value); setIsError(error); };

  async function loadTickets() {
    if (!branchId) return;
    setLoading(true);
    try {
      const url = `/api/v1/kitchen/tickets?branchId=${branchId}${stationId ? `&stationId=${stationId}` : ""}&activeOnly=true`;
      const response = await auth(url);
      setTickets(response.ok ? await response.json() as Ticket[] : []);
    } finally { setLoading(false); }
  }

  async function loadContext() {
    const [branchesResponse, contextResponse] = await Promise.all([auth("/api/v1/pos/context"), auth("/api/v1/kitchen/stations")]);
    if (branchesResponse.ok) { const value = await branchesResponse.json() as { branches: Branch[] }; setBranches(value.branches); if (value.branches[0]) setBranchId(value.branches[0].id); }
    if (contextResponse.ok) setStations(await contextResponse.json() as Station[]);
  }

  useEffect(() => { void loadContext(); }, []);
  useEffect(() => { if (branchId) void refresh(); }, [branchId, stationId]);

  // SignalR is realtime transport only, not the source of truth (docs/01-ARCHITECTURE-GUARDRAILS.md):
  // the hub just tells this screen something changed for the branch, and it re-fetches the ticket list
  // over the existing REST endpoint below instead of trusting ticket state carried over the socket.
  const loadTicketsRef = useRef(loadTickets);
  loadTicketsRef.current = loadTickets;
  const live = useReliableBranchHub(
    "/hubs/kitchen",
    branchId,
    (connection) => connection.on("ticketChanged", (payload: { branchId: string }) => {
      if (payload.branchId === branchId) void loadTicketsRef.current();
    }),
    () => { void loadTicketsRef.current(); },
  );

  useEffect(() => {
    if (!branchId) return;
    const timer = window.setInterval(() => { void loadTicketsRef.current(); }, live ? 60_000 : 10_000);
    return () => window.clearInterval(timer);
  }, [branchId, live]);

  async function refresh() {
    setMsg("");
    const queuePromise = loadTickets();
    if (!branchId) return;
    const ordersResponse = await auth(`/api/v1/orders?branchId=${branchId}`);
    if (ordersResponse.ok) setOrders((await ordersResponse.json() as Order[]).filter((o) => o.status !== "Cancelled" && o.status !== "Rejected"));
    await queuePromise;
  }

  async function dispatch(event: React.FormEvent) { event.preventDefault(); setMsg(""); if (!branchId || !dispatchForm.orderId) { setMsg(t.failed, true); return; } try { const body = { branchId, orderId: dispatchForm.orderId, clientDispatchId: createId(), orderNumber: null, note: null, targetMinutes: dispatchForm.targetMinutes === "" ? null : Number(dispatchForm.targetMinutes) }; const response = await auth("/api/v1/kitchen/tickets", { method: "POST", body: JSON.stringify(body) }); if (!response.ok) throw new Error(t.failed); setMsg(t.dispatched); setDispatchForm({ orderId: "", targetMinutes: "" }); setShowDispatch(false); void refresh(); } catch { setMsg(t.failed, true); } }

  async function act(ticket: Ticket, action: "send" | "ack" | "fallback" | "printed" | "fail") {
    setMsg(""); const id = ticket.id;
    try {
      const url = action === "printed" ? `/api/v1/kitchen/tickets/${id}/printed` : action === "fail" ? `/api/v1/kitchen/tickets/${id}/fail` : action === "fallback" ? `/api/v1/kitchen/tickets/${id}/fallback` : action === "ack" ? `/api/v1/kitchen/tickets/${id}/ack` : `/api/v1/kitchen/tickets/${id}/send`;
      const body = action === "fallback" ? { error: "KDS unavailable", manual: false, templateCode: null } : action === "fail" ? { error: t.failed } : undefined;
      const response = await auth(url, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });
      if (!response.ok) { const problem = await response.json().catch(() => null); setMsg(problem?.errors?.ticket?.[0] ?? t.failed, true); return; }
      void loadTickets();
    } catch { setMsg(t.failed, true); }
  }

  async function setItem(id: string, itemId: string, status: ItemStatus) {
    setMsg(""); try { const response = await auth(`/api/v1/kitchen/tickets/${id}/item/${itemId}`, { method: "PUT", body: JSON.stringify({ status }) }); if (!response.ok) throw new Error(t.failed); await loadTickets(); } catch { setMsg(t.failed, true); } }

  async function cancel(id: string) { setMsg(""); try { const response = await auth(`/api/v1/kitchen/tickets/${id}/cancel`, { method: "POST", body: JSON.stringify({ note: null, templateCode: null }) }); if (!response.ok) { const problem = await response.json().catch(() => null); setMsg(problem?.errors?.ticket?.[0] ?? t.failed, true); return; } setConfirmCancelId(null); await loadTickets(); } catch { setMsg(t.failed, true); } }

  const itemNext = (item: TicketItem): ItemStatus | null => item.status === "New" ? "Preparing" : item.status === "Preparing" ? "Ready" : item.status === "Ready" ? "Completed" : null;
  const elapsedMinutes = (createdAt: string) => Math.max(0, Math.floor((Date.now() - new Date(createdAt).getTime()) / 60_000));
  const preparingCount = tickets.filter((ticket) => ticket.status === "Preparing" || ticket.items.some((item) => item.status === "Preparing")).length;
  const readyCount = tickets.filter((ticket) => ticket.status === "Ready" || (ticket.items.length > 0 && ticket.items.every((item) => item.status === "Ready" || item.status === "Completed"))).length;
  const overdueCount = tickets.filter((ticket) => ticket.overdue).length;

  return (
    <div className="space-y-6">
      <header className="relative overflow-hidden rounded-[28px] bg-[#092f2a] px-5 py-6 text-white shadow-[0_18px_60px_rgba(9,47,42,0.18)] sm:px-7 sm:py-7">
        <div className="pointer-events-none absolute -end-16 -top-20 h-56 w-56 rounded-full bg-[#18a77e]/20 blur-2xl" />
        <div className="pointer-events-none absolute -bottom-24 start-1/3 h-48 w-48 rounded-full bg-[#e4a63a]/10 blur-3xl" />
        <div className="relative flex flex-col gap-6 xl:flex-row xl:items-end xl:justify-between">
          <div className="max-w-2xl">
            <div className="mb-4 flex flex-wrap items-center gap-2">
              <span className="inline-flex items-center gap-2 rounded-full border border-white/10 bg-white/10 px-3 py-1.5 text-xs font-bold tracking-wide"><ChefHat size={15} />KITCHEN DISPLAY</span>
              <span className={`inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-xs font-bold ${live ? "bg-[#29c58f]/20 text-[#78e3bc]" : "bg-[#e45f54]/20 text-[#ffaaa3]"}`}>
                {live ? <Wifi size={14} /> : <WifiOff size={14} />}<span className={`h-1.5 w-1.5 rounded-full ${live ? "bg-[#53d6a7] animate-pulse" : "bg-[#ff8278]"}`} />{live ? t.kdsOn : t.kdsOff}
              </span>
            </div>
            <h1 className="text-3xl font-black tracking-tight sm:text-4xl">{t.title}</h1>
            <p className="mt-3 max-w-xl text-sm leading-7 text-white/65 sm:text-base">{t.intro}</p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="min-w-56 rounded-xl bg-white p-1 text-[#102e2a]"><SearchableSelect label={t.branch} value={branchId} onChange={setBranchId}>{branches.map((b) => <option key={b.id} value={b.id}>{name(b)}</option>)}</SearchableSelect></div>
            <button onClick={() => void refresh()} aria-label={t.reload} className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border border-white/15 bg-white/10 px-4 font-bold text-white transition hover:bg-white/20"><RefreshCw className={loading ? "animate-spin" : ""} size={18} />{t.reload}</button>
            <button onClick={() => setShowDispatch(true)} className="inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-[#f3b33f] px-5 font-black text-[#18362f] shadow-lg shadow-black/15 transition hover:-translate-y-0.5 hover:bg-[#ffc45b]"><Send size={18} />{t.dispatchAction}</button>
          </div>
        </div>
      </header>

      <section aria-label={t.activeOrders} className="grid grid-cols-2 gap-3 xl:grid-cols-4">
        {[
          { label: t.activeOrders, value: tickets.length, icon: LayoutGrid, tone: "bg-[#e8f4f0] text-[#0e6c5b]" },
          { label: t.preparingNow, value: preparingCount, icon: Flame, tone: "bg-[#fff1df] text-[#b66316]" },
          { label: t.readyNow, value: readyCount, icon: CheckCircle2, tone: "bg-[#e7f6eb] text-[#18824a]" },
          { label: t.lateOrders, value: overdueCount, icon: AlertTriangle, tone: "bg-[#fdebea] text-[#bd3f38]" },
        ].map((metric) => <div key={metric.label} className="flex items-center gap-3 rounded-2xl border border-[#dfe7e2] bg-white p-4 shadow-[0_8px_30px_rgba(20,55,45,0.05)] sm:p-5"><span className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl ${metric.tone}`}><metric.icon size={21} /></span><div><p className="text-2xl font-black leading-none text-[#17332d]">{metric.value}</p><p className="mt-1.5 text-xs font-semibold text-[#64756f] sm:text-sm">{metric.label}</p></div></div>)}
      </section>

      <section className="rounded-2xl border border-[#dfe7e2] bg-white p-3 shadow-[0_8px_30px_rgba(20,55,45,0.04)] sm:p-4">
        <div className="mb-3 flex items-center gap-2 px-1 text-xs font-bold uppercase tracking-wider text-[#71817b]"><UtensilsCrossed size={15} />{t.stationsLabel}</div>
        <div className="flex gap-2 overflow-x-auto pb-1">
          <button onClick={() => setStationId("")} className={`min-h-10 shrink-0 rounded-xl px-4 text-sm font-bold transition ${stationId === "" ? "bg-[#0e5a4f] text-white shadow-md shadow-[#0e5a4f]/15" : "bg-[#f1f5f3] text-[#53645e] hover:bg-[#e5eeea]"}`}>{t.allStations}<span className="ms-2 rounded-full bg-black/10 px-2 py-0.5 text-[11px]">{tickets.length}</span></button>
          {stations.map((station) => <button key={station.id} onClick={() => setStationId(station.id)} className={`min-h-10 shrink-0 rounded-xl px-4 text-sm font-bold transition ${stationId === station.id ? "bg-[#0e5a4f] text-white shadow-md shadow-[#0e5a4f]/15" : "bg-[#f1f5f3] text-[#53645e] hover:bg-[#e5eeea]"}`}><span className="me-2 text-[11px] opacity-60">{station.code}</span>{name(station)}</button>)}
        </div>
      </section>

      {message && <p role={isError ? "alert" : "status"} className={`rounded-xl border px-4 py-3 text-sm font-semibold ${isError ? "border-[#efc6c3] bg-[#fff1f0] text-[#b4322a]" : "border-[#bfe2cc] bg-[#eefaf2] text-[#137347]"}`}>{message}</p>}

      {showDispatch && <FormDialog title={t.dispatch} closeLabel={t.cancel} onClose={() => setShowDispatch(false)} width="max-w-xl">
        <p className="text-sm text-[#000000]">{t.dispatchNote}</p>
        <form onSubmit={dispatch} className="mt-4 grid gap-4 sm:grid-cols-2">
          <div className="sm:col-span-2"><SearchableSelect label={t.order} value={dispatchForm.orderId} onChange={(v) => setDispatchForm({ ...dispatchForm, orderId: v })}>{orders.length === 0 && <option value="">{t.empty}</option>}{orders.map((o) => <option key={o.id} value={o.id}>{o.status} · {o.grossAmount}</option>)}</SearchableSelect></div>
          <label className="block text-sm font-medium">{t.targetMinutes}<input type="number" value={dispatchForm.targetMinutes} onChange={(e) => setDispatchForm({ ...dispatchForm, targetMinutes: e.target.value })} min={1} max={999} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] px-3" /></label>
          <button disabled={loading} className="mt-1 inline-flex min-h-11 items-center gap-2 justify-self-start rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Send size={18} />{t.dispatchAction}</button>
        </form>
      </FormDialog>}

      {loading && <div className="flex items-center justify-center gap-3 rounded-2xl border border-dashed border-[#cbd8d1] bg-white/60 p-10 font-semibold text-[#5a6c65]"><RefreshCw className="animate-spin text-[#0e5a4f]" size={22} />{t.loading}</div>}

      {!loading && tickets.length === 0 && <div className="rounded-3xl border border-dashed border-[#cbd8d1] bg-white px-6 py-14 text-center"><span className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-[#edf5f1] text-[#0e5a4f]"><ChefHat size={30} /></span><h2 className="mt-5 text-xl font-black text-[#17332d]">{t.noTicketsTitle}</h2><p className="mx-auto mt-2 max-w-md text-sm leading-6 text-[#6a7a74]">{t.noTicketsHint}</p></div>}

      {!loading && tickets.length > 0 && <div className="flex items-end justify-between gap-3"><div><p className="text-xs font-bold uppercase tracking-[0.16em] text-[#0e7a68]">{t.queue}</p><h2 className="mt-1 text-xl font-black text-[#17332d]">{stationId ? name(stations.find((station) => station.id === stationId) ?? { nameAr: t.station, nameEn: t.station }) : t.allStations}</h2></div><span className="rounded-full bg-[#e8f1ed] px-3 py-1.5 text-xs font-bold text-[#476159]">{tickets.length} {t.activeOrders}</span></div>}

      <div className="grid items-start gap-4 lg:grid-cols-2 2xl:grid-cols-3">
        {tickets.map((ticket) => (
          <article key={ticket.id} className={`group relative overflow-hidden rounded-2xl border bg-white shadow-[0_10px_35px_rgba(24,56,47,0.07)] transition hover:-translate-y-0.5 hover:shadow-[0_16px_45px_rgba(24,56,47,0.11)] ${ticket.overdue ? "border-[#e5aaa5]" : ticket.status === "Ready" ? "border-[#9bd5b2]" : "border-[#dce5e0]"}`}>
            <div className={`h-1.5 w-full ${ticket.overdue ? "bg-[#d84f45]" : ticket.status === "Ready" ? "bg-[#25a75f]" : ticket.status === "Preparing" ? "bg-[#e8a12d]" : "bg-[#0e7665]"}`} />
            <div className="p-4 sm:p-5">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div>
                <p className="text-xs font-bold uppercase tracking-wide text-[#73837d]">{t.orderNo}</p>
                <p className="mt-0.5 text-2xl font-black tracking-tight text-[#17332d]">#{ticket.orderNumber}</p>
              </div>
              <div className="flex flex-wrap items-center justify-end gap-1.5">
                {ticket.fallbackPrinted && <span className="rounded-full bg-[#f4f1e3] px-2 py-1 text-xs font-semibold text-[#8a6d1f]">{t.fallbackBadge}</span>}
                {ticket.overdue && <span className="rounded-full bg-[#fbe4e2] px-2 py-1 text-xs font-semibold text-[#b4322a]">{t.overdue}</span>}
                <span className={`rounded-full px-2.5 py-1 text-xs font-bold ${statusPill(ticket.dispatchStatus)}`}>{dispatchLabel(ticket.dispatchStatus)}</span>
              </div>
            </div>
            <div className="mt-4 grid grid-cols-3 divide-x divide-[#dfe7e2] rounded-xl bg-[#f4f7f5] px-2 py-3 text-center rtl:divide-x-reverse">
              <div><Clock3 className="mx-auto text-[#60736c]" size={16} /><p className={`mt-1 text-sm font-black ${ticket.overdue ? "text-[#c7433b]" : "text-[#243d36]"}`}>{elapsedMinutes(ticket.createdAt)} {t.min}</p><p className="text-[10px] font-semibold text-[#7b8984]">{t.elapsed}</p></div>
              <div><UtensilsCrossed className="mx-auto text-[#60736c]" size={16} /><p className="mt-1 text-sm font-black text-[#243d36]">{ticket.items.length}</p><p className="text-[10px] font-semibold text-[#7b8984]">{t.items}</p></div>
              <div><ChefHat className="mx-auto text-[#60736c]" size={16} /><p className="mt-1 truncate px-1 text-sm font-black text-[#243d36]">{ticket.stationCode ?? "—"}</p><p className="text-[10px] font-semibold text-[#7b8984]">{t.station}</p></div>
            </div>
            {ticket.stationNameEn && <p className="mt-3 text-xs font-bold text-[#0e6c5b]">{language === "ar" ? ticket.stationNameAr : ticket.stationNameEn}</p>}
            {ticket.dispatchStatus === "PrintFallbackPending" && <p className="mt-2 rounded-lg bg-[#f4f1e3] px-3 py-2 text-xs font-semibold text-[#8a6d1f]">{t.fallbackNote}</p>}
            {ticket.lastError && <p className="mt-2 text-xs text-[#b4322a]">{ticket.lastError}</p>}

            <div className="mt-4 flex items-center justify-between text-[11px] font-bold text-[#71817b]"><span>{t.progress}</span><span>{ticket.items.filter((item) => item.status === "Completed").length}/{ticket.items.length}</span></div>
            <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-[#e8eeeb]"><div className="h-full rounded-full bg-[#20a66d] transition-all" style={{ width: `${ticket.items.length ? (ticket.items.filter((item) => item.status === "Completed").length / ticket.items.length) * 100 : 0}%` }} /></div>

            <ul className="mt-4 space-y-2">
              {ticket.items.map((item) => (
                <li key={item.id} className={`rounded-xl border p-3 ${item.status === "Completed" ? "border-[#cbe8d6] bg-[#f2faf5]" : item.status === "Cancelled" ? "border-[#f1d1ce] bg-[#fff5f4]" : item.status === "Preparing" ? "border-[#f0d5aa] bg-[#fff9ee]" : "border-[#e1e8e4] bg-[#f8faf9]"}`}>
                  <div className="flex items-center justify-between gap-3"><span className="min-w-0 font-bold text-[#213a33]">{language === "ar" ? item.productNameAr : item.productNameEn}</span><span className="grid h-7 min-w-7 shrink-0 place-items-center rounded-lg bg-white px-2 text-xs font-black text-[#17332d] shadow-sm">×{item.quantity}</span></div>
                  {item.note && <p className="mt-2 rounded-lg bg-white/80 px-2 py-1.5 text-xs text-[#6b4f1f]">{t.note}: {item.note}</p>}
                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                    <span className={`rounded-full px-2 py-0.5 text-[11px] font-bold ${item.status === "Completed" ? "bg-[#d9f1e2] text-[#137347]" : item.status === "Preparing" ? "bg-[#f8e7ca] text-[#9b5915]" : item.status === "Cancelled" ? "bg-[#fbe4e2] text-[#b4322a]" : "bg-[#e8ece8] text-[#53645e]"}`}>{itemLabel(item.status)}</span>
                    <div className="flex gap-1.5">
                      {itemNext(item) && ticket.dispatchStatus !== "Cancelled" && <button onClick={() => void setItem(ticket.id, item.id, itemNext(item)!)} className="min-h-8 rounded-lg bg-[#123d35] px-3 text-xs font-bold text-white transition hover:bg-[#0e5a4f]">{itemNext(item) === "Preparing" ? t.start : itemNext(item) === "Ready" ? t.ready : t.complete}</button>}
                    </div>
                  </div>
                </li>
              ))}
            </ul>

            <div className="mt-4 flex flex-wrap gap-2 border-t border-[#e8eeeb] pt-4">
              {ticket.dispatchStatus === "Pending" && <button onClick={() => void act(ticket, "send")} className="min-h-10 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white">{t.send}</button>}
              {ticket.dispatchStatus === "SentToKds" && <button onClick={() => void act(ticket, "fallback")} className="min-h-10 rounded-lg bg-[#8a6d1f] px-3 text-sm font-semibold text-white">{t.fallback}</button>}
              {ticket.dispatchStatus === "PrintFallbackPending" && <button onClick={() => void act(ticket, "printed")} className="min-h-10 rounded-lg bg-[#137347] px-3 text-sm font-semibold text-white">{t.printed}</button>}
              {ticket.dispatchStatus === "PrintFallbackPending" && <button onClick={() => void act(ticket, "fail")} className="min-h-10 rounded-lg border border-[#b4322a] px-3 text-sm font-semibold text-[#b4322a]">{t.fail}</button>}
              {ticket.dispatchStatus === "SentToKds" && <button onClick={() => void act(ticket, "ack")} className="min-h-10 rounded-lg bg-[#137347] px-3 text-sm font-semibold text-white">{t.ack}</button>}
              {confirmCancelId === ticket.id
                ? <><button onClick={() => void cancel(ticket.id)} className="min-h-10 rounded-lg bg-[#b4322a] px-3 text-sm font-semibold text-white">{t.confirmCancel}</button><button onClick={() => setConfirmCancelId(null)} className="min-h-10 rounded-lg border border-[#cdd7d0] px-3 text-sm font-semibold">{t.cancel}</button></>
                : <button onClick={() => setConfirmCancelId(ticket.id)} className="min-h-10 rounded-lg border border-[#b4322a] px-3 text-sm font-semibold text-[#b4322a]">{t.cancel}</button>}
            </div>
            </div>
          </article>
        ))}
      </div>
    </div>
  );
}
