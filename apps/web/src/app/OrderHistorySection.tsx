import { useEffect, useState } from "react";
import { FormDialog } from "@/app/FormDialog";
import { store } from "@/lib/local-store";

type Language = "ar" | "en";
type Branch = { id: string; nameAr: string; nameEn: string };
type Channel = { id: string; code: string; nameAr: string; nameEn: string };
type OrderRow = { id: string; status: string; source: string; salesChannelId: string; netAmount: number; taxAmount: number; grossAmount: number; note: string | null; createdAt: string; lineCount: number };
type HistoryResponse = { total: number; page: number; pageSize: number; items: OrderRow[] };
type OrderDetail = { id: string; salesChannelId: string; source: string; status: string; note: string | null; netAmount: number; taxAmount: number; grossAmount: number; createdAt: string; lines: Array<{ id: string; productNameAr: string; productNameEn: string; quantity: number; note: string | null; unitGrossAmount: number }> };
type PaymentRow = { id: string; amount: number; status: string; code: string; nameAr: string; nameEn: string };

const input = "min-h-11 min-w-0 rounded-lg border border-[#cdd7d0] bg-white px-3 text-sm";
const button = "inline-flex min-h-10 items-center justify-center gap-1.5 rounded-lg border border-[#cdd7d0] px-3 text-sm font-semibold";
const PAGE_SIZE = 20;

function todayStr() { return new Date().toISOString().slice(0, 10); }

const copy = {
  ar: {
    title: "سجل الطلبات", intro: "استعرض جميع طلبات أي فرع خلال أي فترة زمنية.", branch: "الفرع", from: "من", to: "إلى", status: "الحالة", allStatuses: "كل الحالات", channel: "قناة البيع", allChannels: "كل القنوات",
    loading: "جارٍ التحميل…", error: "تعذر تحميل الطلبات.", retry: "إعادة المحاولة", empty: "لا توجد طلبات لهذه الفترة.",
    time: "الوقت", items: "الأصناف", total: "الإجمالي", view: "عرض", close: "إغلاق", prev: "السابق", next: "التالي",
    net: "الصافي", tax: "الضريبة", gross: "الإجمالي", note: "ملاحظة", payments: "المدفوعات", noPayments: "لا توجد مدفوعات مسجلة.",
    statuses: { Draft: "معلّق", Pending: "بانتظار الدفع", Confirmed: "مؤكد", Paid: "مدفوع", SentToKitchen: "أُرسل للمطبخ", Preparing: "قيد التحضير", Ready: "جاهز", Completed: "مكتمل", Cancelled: "ملغى", Rejected: "مرفوض", PartiallyRefunded: "استرجاع جزئي", Refunded: "مسترجع" } as Record<string, string>,
    sources: { Pos: "نقطة بيع", Qr: "QR", Delivery: "توصيل", Aggregator: "منصة توصيل" } as Record<string, string>,
  },
  en: {
    title: "Order history", intro: "Browse every order for any branch and date range.", branch: "Branch", from: "From", to: "To", status: "Status", allStatuses: "All statuses", channel: "Sales channel", allChannels: "All channels",
    loading: "Loading…", error: "Unable to load orders.", retry: "Retry", empty: "No orders for this period.",
    time: "Time", items: "Items", total: "Total", view: "View", close: "Close", prev: "Previous", next: "Next",
    net: "Net", tax: "Tax", gross: "Total", note: "Note", payments: "Payments", noPayments: "No payments recorded.",
    statuses: { Draft: "Held", Pending: "Awaiting payment", Confirmed: "Confirmed", Paid: "Paid", SentToKitchen: "Sent to kitchen", Preparing: "Preparing", Ready: "Ready", Completed: "Completed", Cancelled: "Cancelled", Rejected: "Rejected", PartiallyRefunded: "Partially refunded", Refunded: "Refunded" } as Record<string, string>,
    sources: { Pos: "POS", Qr: "QR", Delivery: "Delivery", Aggregator: "Aggregator" } as Record<string, string>,
  },
} as const;

export function OrderHistorySection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) => language === "ar" ? x.nameAr : x.nameEn;
  const [branches, setBranches] = useState<Branch[]>([]);
  const [channels, setChannels] = useState<Channel[]>([]);
  const [branchId, setBranchId] = useState("");
  const [from, setFrom] = useState(todayStr);
  const [to, setTo] = useState(todayStr);
  const [status, setStatus] = useState("");
  const [channelId, setChannelId] = useState("");
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<HistoryResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [selected, setSelected] = useState<{ order: OrderDetail; payments: PaymentRow[] } | null>(null);
  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });

  useEffect(() => { void (async () => { const r = await auth("/api/v1/pos/context"); if (!r.ok) return; const value = await r.json() as { branches: Branch[]; channels: Channel[] }; setBranches(value.branches); setChannels(value.channels); setBranchId(value.branches[0]?.id ?? ""); })(); }, []);
  useEffect(() => { setPage(1); }, [branchId, from, to, status, channelId]);

  async function load() {
    if (!branchId) return;
    setLoading(true); setError(false);
    try {
      const params = new URLSearchParams({ branchId, from: new Date(`${from}T00:00:00`).toISOString(), to: new Date(`${to}T23:59:59.999`).toISOString(), page: String(page), pageSize: String(PAGE_SIZE) });
      if (status) params.set("status", status);
      if (channelId) params.set("salesChannelId", channelId);
      const response = await auth(`/api/v1/orders/history?${params.toString()}`);
      if (!response.ok) throw new Error();
      setResult(await response.json() as HistoryResponse);
    } catch { setError(true); } finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, [branchId, from, to, status, channelId, page]);

  async function open(id: string) {
    const [orderResponse, paymentResponse] = await Promise.all([auth(`/api/v1/orders/${id}`), auth(`/api/v1/orders/${id}/payments`)]);
    if (!orderResponse.ok) return;
    const order = await orderResponse.json() as OrderDetail;
    setSelected({ order, payments: paymentResponse.ok ? await paymentResponse.json() as PaymentRow[] : [] });
  }

  const rows = result?.items ?? [];
  const total = result?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const rangeFrom = total === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const rangeTo = Math.min(page * PAGE_SIZE, total);

  return <div className="space-y-5">
    <div><h1 className="text-2xl font-bold">{t.title}</h1><p className="mt-2 text-sm text-[#64716b]">{t.intro}</p></div>
    <div className="flex flex-wrap items-end gap-3 rounded-xl border border-[#dfe5df] bg-white p-4">
      <label className="text-xs font-medium text-[#53615b]">{t.branch}<select value={branchId} onChange={e => setBranchId(e.target.value)} className={`mt-1 block w-full ${input}`}>{branches.map(b => <option key={b.id} value={b.id}>{name(b)}</option>)}</select></label>
      <label className="text-xs font-medium text-[#53615b]">{t.from}<input type="date" value={from} max={to} onChange={e => setFrom(e.target.value)} className={`mt-1 block ${input}`} /></label>
      <label className="text-xs font-medium text-[#53615b]">{t.to}<input type="date" value={to} min={from} max={todayStr()} onChange={e => setTo(e.target.value)} className={`mt-1 block ${input}`} /></label>
      <label className="text-xs font-medium text-[#53615b]">{t.status}<select value={status} onChange={e => setStatus(e.target.value)} className={`mt-1 block ${input}`}><option value="">{t.allStatuses}</option>{Object.keys(t.statuses).map(s => <option key={s} value={s}>{t.statuses[s]}</option>)}</select></label>
      <label className="text-xs font-medium text-[#53615b]">{t.channel}<select value={channelId} onChange={e => setChannelId(e.target.value)} className={`mt-1 block ${input}`}><option value="">{t.allChannels}</option>{channels.map(c => <option key={c.id} value={c.id}>{name(c)}</option>)}</select></label>
    </div>
    {loading ? <p role="status" className="rounded-xl border bg-white p-8 text-center text-sm">{t.loading}</p>
      : error ? <div role="alert" className="rounded-xl border border-[#efc5c1] bg-[#fff5f4] p-5 text-sm text-[#9b2922]"><p>{t.error}</p><button onClick={() => void load()} className="mt-3 font-semibold underline">{t.retry}</button></div>
      : rows.length === 0 ? <p className="rounded-xl border bg-white p-8 text-center text-sm text-[#69766f]">{t.empty}</p>
      : <>
        <div className="overflow-x-auto rounded-xl border border-[#dfe5df] bg-white">
          <table className="w-full text-sm">
            <thead><tr className="border-b border-[#e8ece8] text-xs text-[#69766f]">
              <th className="px-4 py-3 text-start font-semibold">{t.time}</th>
              <th className="px-4 py-3 text-start font-semibold">{t.channel}</th>
              <th className="px-4 py-3 text-start font-semibold">{t.status}</th>
              <th className="px-4 py-3 text-start font-semibold">{t.items}</th>
              <th className="px-4 py-3 text-start font-semibold">{t.total}</th>
              <th className="px-4 py-3 text-end font-semibold"></th>
            </tr></thead>
            <tbody>{rows.map(o => { const ch = channels.find(c => c.id === o.salesChannelId); return <tr key={o.id} className="border-b border-[#eef1ee] last:border-0 hover:bg-[#fafbf9]">
              <td className="px-4 py-3">{new Date(o.createdAt).toLocaleString(language)}</td>
              <td className="px-4 py-3 text-[#53615b]">{ch ? name(ch) : (t.sources[o.source] ?? o.source)}</td>
              <td className="px-4 py-3"><span className="rounded-full bg-[#f4f7f4] px-2.5 py-0.5 text-xs font-semibold">{t.statuses[o.status] ?? o.status}</span></td>
              <td className="px-4 py-3 text-[#53615b]">{o.lineCount}</td>
              <td className="px-4 py-3 font-semibold">OMR {o.grossAmount.toFixed(3)}</td>
              <td className="px-4 py-3 text-end"><button onClick={() => void open(o.id)} className={button}>{t.view}</button></td>
            </tr>; })}</tbody>
          </table>
        </div>
        <div className="flex items-center justify-between gap-3 text-sm text-[#64716b]">
          <span>{rangeFrom}–{rangeTo} / {total}</span>
          <div className="flex gap-2">
            <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} className={`${button} disabled:opacity-50`}>{t.prev}</button>
            <button disabled={page >= pageCount} onClick={() => setPage(p => p + 1)} className={`${button} disabled:opacity-50`}>{t.next}</button>
          </div>
        </div>
      </>}
    {selected && <FormDialog title={t.view} closeLabel={t.close} onClose={() => setSelected(null)}>
      <div className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2"><span className="rounded-full bg-[#f4f7f4] px-2.5 py-0.5 text-xs font-semibold">{t.statuses[selected.order.status] ?? selected.order.status}</span><span className="text-sm text-[#64716b]">{new Date(selected.order.createdAt).toLocaleString(language)}</span></div>
        <ul className="space-y-2">{selected.order.lines.map(l => <li key={l.id} className="flex items-center justify-between gap-3 rounded-lg bg-[#f4f7f4] px-3 py-2 text-sm"><span>{l.quantity} × {language === "ar" ? l.productNameAr : l.productNameEn}</span><span>OMR {(l.unitGrossAmount * l.quantity).toFixed(3)}</span></li>)}</ul>
        {selected.order.note && <p className="text-sm text-[#64716b]">{t.note}: {selected.order.note}</p>}
        <div className="grid grid-cols-3 gap-2 rounded-lg bg-[#f4f7f4] p-3 text-sm">
          <div><p className="text-xs text-[#69766f]">{t.net}</p><p className="font-semibold">OMR {selected.order.netAmount.toFixed(3)}</p></div>
          <div><p className="text-xs text-[#69766f]">{t.tax}</p><p className="font-semibold">OMR {selected.order.taxAmount.toFixed(3)}</p></div>
          <div><p className="text-xs text-[#69766f]">{t.gross}</p><p className="font-semibold">OMR {selected.order.grossAmount.toFixed(3)}</p></div>
        </div>
        <div><h3 className="text-sm font-semibold">{t.payments}</h3>{selected.payments.length === 0 ? <p className="mt-1 text-sm text-[#69766f]">{t.noPayments}</p> : <ul className="mt-2 space-y-1 text-sm">{selected.payments.map(p => <li key={p.id} className="flex justify-between"><span>{name(p)}</span><span>OMR {p.amount.toFixed(3)}</span></li>)}</ul>}</div>
      </div>
    </FormDialog>}
  </div>;
}
