import { useEffect, useRef, useState } from "react";
import { ChefHat, RefreshCw, Send } from "lucide-react";
import * as signalR from "@microsoft/signalr";
import { createId, store } from "@/lib/local-store";

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
    branch: "الفرع", station: "محطة التحضير", allStations: "كل المحطات", kdsOn: "KDS متصل", kdsOff: "KDS غير متصل", loading: "جارٍ التحميل", reload: "تحديث", empty: "لا توجد تذاكر مطبخ لهذه المحطة.", dispatch: "إرسال طلب للمطبخ", order: "الطلب", targetMinutes: "الوقت المستهدف (دقيقة)", dispatchAction: "إرسال", dispatchNote: "تُوجَّه الأصناف تلقائيًا إلى محطتها عبر المنتج.",
    send: "إرسال إلى KDS", ack: "استلام/بدء التحضير", fallback: "طباعة احتياطية", fail: "فشل", printed: "تمت الطباعة", cancel: "إلغاء", confirmCancel: "تأكيد الإلغاء", cancelPrompt: "هل أنت متأكد من إلغاء طلب المطبخ؟",
    start: "بدء", ready: "جاهز", complete: "اكتمل", cancelled: "ملغى", overdue: "متأخر", fallbackBadge: "احتياطي", note: "ملاحظة", quantity: "كمية", orderNo: "طلب", dispatched: "تم الإرسال", saved: "تم الحفظ.", failed: "تعذر تنفيذ العملية.",
    stPending: "قيد الإنشاء", stSentToKds: "مرسل إلى KDS", stKdsAcknowledged: "مستلم", stPrintFallbackPending: "طباعة احتياطية...", stPrintedFallback: "مطبوع احتياطيًا", stFailed: "فاشل", stCancelled: "ملغى",
    itNew: "جديد", itPreparing: "قيد التحضير", itReady: "جاهز", itCompleted: "مكتمل", itCancelled: "ملغى"
  },
  en: {
    title: "Kitchen KDS", intro: "Review kitchen tickets by preparation station, track status and prep times, and print a fallback ticket when the KDS is unavailable.", isolated: "Kitchen keeps working even when the service is offline", fallbackNote: "OFFLINE / FALLBACK TICKET",
    branch: "Branch", station: "Preparation station", allStations: "All stations", kdsOn: "KDS online", kdsOff: "KDS offline", loading: "Loading", reload: "Refresh", empty: "No kitchen tickets for this station.", dispatch: "Dispatch an order to the kitchen", order: "Order", targetMinutes: "Target time (min)", dispatchAction: "Dispatch", dispatchNote: "Items route to their station automatically via the product.",
    send: "Send to KDS", ack: "Acknowledge / start prep", fallback: "Print fallback", fail: "Fail", printed: "Mark printed", cancel: "Cancel", confirmCancel: "Confirm cancel", cancelPrompt: "Are you sure you want to cancel this kitchen order?",
    start: "Start", ready: "Ready", complete: "Complete", cancelled: "Cancelled", overdue: "Overdue", fallbackBadge: "Fallback", note: "Note", quantity: "Qty", orderNo: "Order", dispatched: "Dispatched", saved: "Saved.", failed: "Unable to complete the operation.",
    stPending: "Pending", stSentToKds: "Sent to KDS", stKdsAcknowledged: "Acknowledged", stPrintFallbackPending: "Fallback printing…", stPrintedFallback: "Printed fallback", stFailed: "Failed", stCancelled: "Cancelled",
    itNew: "New", itPreparing: "Preparing", itReady: "Ready", itCompleted: "Completed", itCancelled: "Cancelled"
  },
} as const;

export function KitchenSection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) => (language === "ar" ? x.nameAr : x.nameEn);
  const dispatchLabel = (s: DispatchStatus) => s === "Pending" ? t.stPending : s === "SentToKds" ? t.stSentToKds : s === "KdsAcknowledged" ? t.stKdsAcknowledged : s === "PrintFallbackPending" ? t.stPrintFallbackPending : s === "PrintedFallback" ? t.stPrintedFallback : s === "Failed" ? t.stFailed : t.stCancelled;
  const itemLabel = (s: ItemStatus) => s === "New" ? t.itNew : s === "Preparing" ? t.itPreparing : s === "Ready" ? t.itReady : s === "Completed" ? t.itCompleted : t.itCancelled;
  const statusPill = (s: DispatchStatus) => s === "PrintedFallback" || s === "PrintFallbackPending" ? "bg-[#f4f1e3] text-[#8a6d1f]" : s === "Failed" || s === "Cancelled" ? "bg-[#fbe4e2] text-[#b4322a]" : s === "KdsAcknowledged" ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#e8ece8] text-[#53615b]";

  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });

  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [stations, setStations] = useState<Station[]>([]);
  const [stationId, setStationId] = useState("");
  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [orders, setOrders] = useState<Order[]>([]);
  const [live, setLive] = useState(false);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);
  const [dispatchForm, setDispatchForm] = useState({ orderId: "", targetMinutes: "" });
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
  useEffect(() => {
    if (!branchId) return;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/kitchen", { accessTokenFactory: () => store.get<string>("session-token") ?? "" })
      .withAutomaticReconnect()
      .build();
    connection.on("ticketChanged", (payload: { branchId: string }) => { if (payload.branchId === branchId) void loadTicketsRef.current(); });
    connection.onreconnected(() => { setLive(true); void loadTicketsRef.current(); });
    connection.onreconnecting(() => setLive(false));
    connection.onclose(() => setLive(false));
    connection.start().then(() => { setLive(true); return connection.invoke("JoinBranch", branchId); }).catch(() => setLive(false));
    return () => { setLive(false); void connection.stop(); };
  }, [branchId]);

  async function refresh() {
    setMsg("");
    const queuePromise = loadTickets();
    if (!branchId) return;
    const ordersResponse = await auth(`/api/v1/orders?branchId=${branchId}`);
    if (ordersResponse.ok) setOrders((await ordersResponse.json() as Order[]).filter((o) => o.status !== "Cancelled" && o.status !== "Rejected"));
    await queuePromise;
  }

  async function dispatch(event: React.FormEvent) { event.preventDefault(); setMsg(""); if (!branchId || !dispatchForm.orderId) { setMsg(t.failed, true); return; } try { const body = { branchId, orderId: dispatchForm.orderId, clientDispatchId: createId(), orderNumber: null, note: null, targetMinutes: dispatchForm.targetMinutes === "" ? null : Number(dispatchForm.targetMinutes) }; const response = await auth("/api/v1/kitchen/tickets", { method: "POST", body: JSON.stringify(body) }); if (!response.ok) throw new Error(t.failed); setMsg(t.dispatched); setDispatchForm({ orderId: "", targetMinutes: "" }); void refresh(); } catch { setMsg(t.failed, true); } }

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

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <p className="mt-3 max-w-3xl text-[#64716b]">{t.intro}</p>
      <div className="mt-4 flex flex-wrap items-center gap-3 rounded-xl border border-[#d9dfd7] bg-[#edf5f1] p-3 text-sm text-[#08483f]"><ChefHat size={18} /><span>{t.isolated}</span><span className={`min-h-9 rounded-full px-3 py-1.5 text-xs font-semibold ${live ? "bg-[#0e5a4f] text-white" : "bg-[#fbe4e2] text-[#b4322a]"}`}>{live ? t.kdsOn : t.kdsOff}</span></div>

      <div className="mt-5 flex flex-col gap-3 sm:flex-row sm:items-end">
        <label className="block flex-1 text-sm font-medium">{t.branch}<select value={branchId} onChange={(e) => setBranchId(e.target.value)} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{branches.map((b) => <option key={b.id} value={b.id}>{name(b)}</option>)}</select></label>
        <label className="block flex-1 text-sm font-medium">{t.station}<select value={stationId} onChange={(e) => setStationId(e.target.value)} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3"><option value="">{t.allStations}</option>{stations.map((s) => <option key={s.id} value={s.id}>{s.code} · {name(s)}</option>)}</select></label>
        <button onClick={() => void refresh()} className="inline-flex min-h-12 items-center gap-2 rounded-lg border border-[#0e5a4f] px-4 font-semibold text-[#0e5a4f]"><RefreshCw size={18} />{t.reload}</button>
      </div>

      {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}

      <div className="mt-6 grid gap-5 xl:grid-cols-2">
        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.dispatch}</h2>
          <p className="mt-1 text-sm text-[#69766f]">{t.dispatchNote}</p>
          <form onSubmit={dispatch} className="mt-4 grid gap-4 sm:grid-cols-2">
            <label className="block text-sm font-medium sm:col-span-2">{t.order}<select value={dispatchForm.orderId} onChange={(e) => setDispatchForm({ ...dispatchForm, orderId: e.target.value })} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{orders.length === 0 && <option value="">{t.empty}</option>}{orders.map((o) => <option key={o.id} value={o.id}>{o.status} · {o.grossAmount}</option>)}</select></label>
            <label className="block text-sm font-medium">{t.targetMinutes}<input type="number" value={dispatchForm.targetMinutes} onChange={(e) => setDispatchForm({ ...dispatchForm, targetMinutes: e.target.value })} min={1} max={999} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] px-3" /></label>
            <button disabled={loading} className="mt-1 inline-flex min-h-11 items-center gap-2 justify-self-start rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Send size={18} />{t.dispatchAction}</button>
          </form>
        </section>
        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.station}</h2>
          {stations.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : <ul className="mt-3 grid gap-2 sm:grid-cols-2">{stations.map((s) => <li key={s.id} className="flex items-center justify-between rounded-lg bg-[#f4f7f4] px-3 py-2 text-sm"><span>{s.code} · {name(s)}</span><button onClick={() => setStationId(s.id)} className="text-xs font-semibold text-[#0e5a4f]">{t.reload}</button></li>)}</ul>}
        </section>
      </div>

      {loading && <div className="mt-6 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div>}

      {!loading && tickets.length === 0 && <p className="mt-6 rounded-xl border border-[#dfe5df] bg-white p-8 text-center text-sm text-[#69766f]">{t.empty}</p>}

      <div className="mt-6 grid gap-4 lg:grid-cols-2 2xl:grid-cols-3">
        {tickets.map((ticket) => (
          <article key={ticket.id} className={`rounded-xl border bg-white p-4 ${ticket.overdue ? "border-[#e8b6b0]" : ticket.status === "Completed" ? "border-[#cdd7d0] opacity-70" : "border-[#dfe5df]"}`}>
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div>
                <p className="font-semibold">{t.orderNo} {ticket.orderNumber}</p>
                <p className="mt-1 text-xs text-[#69766f]">{new Intl.DateTimeFormat(language, { timeStyle: "short" }).format(new Date(ticket.createdAt))}{ticket.targetMinutes ? ` · ${t.targetMinutes} ${language === "ar" ? "دقيقة" : "min"}` : ""}</p>
              </div>
              <div className="flex flex-wrap items-center gap-1">
                {ticket.fallbackPrinted && <span className="rounded-full bg-[#f4f1e3] px-2 py-1 text-xs font-semibold text-[#8a6d1f]">{t.fallbackBadge}</span>}
                {ticket.overdue && <span className="rounded-full bg-[#fbe4e2] px-2 py-1 text-xs font-semibold text-[#b4322a]">{t.overdue}</span>}
                <span className={`rounded-full px-2 py-1 text-xs font-semibold ${statusPill(ticket.dispatchStatus)}`}>{dispatchLabel(ticket.dispatchStatus)}</span>
              </div>
            </div>
            {ticket.stationNameEn && <p className="mt-1 text-xs text-[#0e5a4f]">{ticket.stationCode} · {language === "ar" ? ticket.stationNameAr : ticket.stationNameEn}</p>}
            {ticket.dispatchStatus === "PrintFallbackPending" && <p className="mt-2 rounded-lg bg-[#f4f1e3] px-3 py-2 text-xs font-semibold text-[#8a6d1f]">{t.fallbackNote}</p>}
            {ticket.lastError && <p className="mt-2 text-xs text-[#b4322a]">{ticket.lastError}</p>}

            <ul className="mt-3 space-y-3">
              {ticket.items.map((item) => (
                <li key={item.id} className={`rounded-lg border p-2 ${item.status === "Completed" ? "border-[#e3f4ea] bg-[#f6fbf8]" : item.status === "Cancelled" ? "border-[#fbe4e2] bg-[#fff5f4]" : "bg-[#f4f7f4]"}`}>
                  <div className="flex items-center justify-between gap-2"><span className="min-w-0 font-medium">{language === "ar" ? item.productNameAr : item.productNameEn}</span><span className="text-xs text-[#69766f]">{t.quantity} {item.quantity}</span></div>
                  {item.note && <p className="mt-1 text-xs text-[#69766f]">{t.note}: {item.note}</p>}
                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                    <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${item.status === "Completed" ? "bg-[#e3f4ea] text-[#137347]" : item.status === "Cancelled" ? "bg-[#fbe4e2] text-[#b4322a]" : "bg-[#e8ece8] text-[#53615b]"}`}>{itemLabel(item.status)}</span>
                    <div className="flex gap-1.5">
                      {itemNext(item) && ticket.dispatchStatus !== "Cancelled" && <button onClick={() => void setItem(ticket.id, item.id, itemNext(item)!)} className="min-h-8 rounded-lg bg-[#0e5a4f] px-2.5 text-xs font-semibold text-white">{itemNext(item) === "Preparing" ? t.start : itemNext(item) === "Ready" ? t.ready : t.complete}</button>}
                    </div>
                  </div>
                </li>
              ))}
            </ul>

            <div className="mt-4 flex flex-wrap gap-2">
              {ticket.dispatchStatus === "Pending" && <button onClick={() => void act(ticket, "send")} className="min-h-10 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white">{t.send}</button>}
              {ticket.dispatchStatus === "SentToKds" && <button onClick={() => void act(ticket, "fallback")} className="min-h-10 rounded-lg bg-[#8a6d1f] px-3 text-sm font-semibold text-white">{t.fallback}</button>}
              {ticket.dispatchStatus === "PrintFallbackPending" && <button onClick={() => void act(ticket, "printed")} className="min-h-10 rounded-lg bg-[#137347] px-3 text-sm font-semibold text-white">{t.printed}</button>}
              {ticket.dispatchStatus === "PrintFallbackPending" && <button onClick={() => void act(ticket, "fail")} className="min-h-10 rounded-lg border border-[#b4322a] px-3 text-sm font-semibold text-[#b4322a]">{t.fail}</button>}
              {ticket.dispatchStatus === "SentToKds" && <button onClick={() => void act(ticket, "ack")} className="min-h-10 rounded-lg bg-[#137347] px-3 text-sm font-semibold text-white">{t.ack}</button>}
              {confirmCancelId === ticket.id
                ? <><button onClick={() => void cancel(ticket.id)} className="min-h-10 rounded-lg bg-[#b4322a] px-3 text-sm font-semibold text-white">{t.confirmCancel}</button><button onClick={() => setConfirmCancelId(null)} className="min-h-10 rounded-lg border border-[#cdd7d0] px-3 text-sm font-semibold">{t.cancel}</button></>
                : <button onClick={() => setConfirmCancelId(ticket.id)} className="min-h-10 rounded-lg border border-[#b4322a] px-3 text-sm font-semibold text-[#b4322a]">{t.cancel}</button>}
            </div>
          </article>
        ))}
      </div>
    </div>
  );
}
