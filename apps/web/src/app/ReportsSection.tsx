import { useEffect, useState } from "react";
import { Download, RefreshCw } from "lucide-react";
import { store } from "@/lib/local-store";

type Language = "ar" | "en";
type ReportTab = "dashboard" | "sales" | "payments" | "cancellations" | "shifts" | "inventory" | "kitchen" | "audit";
type LoadState = "idle" | "loading" | "error";
type Context = { branches: Array<{ id: string; nameAr: string; nameEn: string }> };

type SummaryCard = { label: string; value: string };

const copy = {
  ar: {
    title: "التقارير والتدقيق", intro: "راجع المبيعات والمدفوعات والإلغاءات والفرق النقدي والمخزون والمطبخ وسجل التدقيق.", loading: "جارٍ تحميل التقارير", error: "تعذر تحميل التقارير. تحقق من الاتصال والصلاحيات.", retry: "إعادة المحاولة", empty: "لا توجد بيانات لهذه الفترة.", branch: "الفرع", export: "تصدير CSV", dateFrom: "من", dateTo: "إلى", refresh: "تحديث", exportDone: "تم تصدير الملف.", sales: "المبيعات", payments: "المدفوعات", cancellations: "الإلغاء والاسترجاع", shifts: "الورديات والنقد", inventory: "المخزون", kitchen: "المطبخ", audit: "سجل التدقيق", dashboard: "لوحة التحكم",
    todaySales: "مبيعات اليوم", netSales: "صافي المبيعات", tax: "الضريبة", orders: "الطلبات", averageOrder: "متوسط قيمة الطلب", openShifts: "وردية مفتوحة", cashVariance: "فرق النقدية", refunds: "الاسترجاعات", cancelled: "الطلبات الملغاة", cancelRate: "نسبة الإلغاء", lowStock: "أصناف تحت الحد", waste: "الهدر", avgPrep: "متوسط التحضير", lateOrders: "طلبات متأخرة", discount: "الخصومات",
    product: "الصنف", channel: "القناة", quantity: "الكمية", gross: "الإجمالي", count: "العدد", reason: "السبب", user: "المستخدم", hour: "الساعة", open: "الفتح", close: "الإغلاق", expectedCash: "النقد المتوقع", actualCash: "النقد الفعلي", variance: "الفرق", typeLabel: "النوع", balance: "الرصيد", station: "المحطة", status: "الحالة", action: "العملية", entity: "الكيان", when: "الوقت", amount: "المبلغ", items: "الصنف", orderCount: "الطلبات"
  },
  en: {
    title: "Reports & audit", intro: "Review sales, payments, cancellations, cash variance, inventory, kitchen throughput, and the audit trail.", loading: "Loading reports", error: "Unable to load reports. Check your connection and permissions.", retry: "Retry", empty: "No data for this period.", branch: "Branch", export: "Export CSV", dateFrom: "From", dateTo: "To", refresh: "Refresh", exportDone: "File exported.", sales: "Sales", payments: "Payments", cancellations: "Cancellations & refunds", shifts: "Shifts & cash", inventory: "Inventory", kitchen: "Kitchen", audit: "Audit log", dashboard: "Dashboard",
    todaySales: "Today's sales", netSales: "Net sales", tax: "Tax", orders: "Orders", averageOrder: "Average order", openShifts: "Open shifts", cashVariance: "Cash variance", refunds: "Refunds", cancelled: "Cancelled orders", cancelRate: "Cancellation rate", lowStock: "Low-stock items", waste: "Waste", avgPrep: "Avg prep time", lateOrders: "Late orders", discount: "Discounts",
    product: "Product", channel: "Channel", quantity: "Qty", gross: "Gross", count: "Count", reason: "Reason", user: "User", hour: "Hour", open: "Opened", close: "Closed", expectedCash: "Expected cash", actualCash: "Actual cash", variance: "Variance", typeLabel: "Type", balance: "Balance", station: "Station", status: "Status", action: "Action", entity: "Entity", when: "When", amount: "Amount", items: "Item", orderCount: "Orders"
  },
} as const;

const tabs: Array<[ReportTab, string]> = [["dashboard", "dashboard"], ["sales", "sales"], ["payments", "payments"], ["cancellations", "cancellations"], ["shifts", "shifts"], ["inventory", "inventory"], ["kitchen", "kitchen"], ["audit", "audit"]];

function dayDate(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}
function plusDays(date: string, days: number): string {
  const value = new Date(`${date}T00:00:00.000Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString();
}

export function ReportsSection({ language }: { language: Language }) {
  const t = copy[language];
  const token = store.get<string>("session-token") ?? "";
  const [tab, setTab] = useState<ReportTab>("dashboard");
  const [state, setState] = useState<LoadState>("idle");
  const [data, setData] = useState<any>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [branches, setBranches] = useState<Context["branches"]>([]);
  const [branchId, setBranchId] = useState("");
  const [from, setFrom] = useState(() => dayDate(new Date()));
  const [to, setTo] = useState(() => dayDate(new Date()));

  useEffect(() => {
    document.title = t.title;
    fetch("/api/v1/pos/context", { headers: { Authorization: `Bearer ${token}` } })
      .then((r) => (r.ok ? r.json() as Promise<Context> : Promise.reject()))
      .then((context) => { setBranches(context.branches); if (!branchId && context.branches[0]) setBranchId(context.branches[0].id); })
      .catch(() => undefined);
  }, [token]);

  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { Authorization: `Bearer ${token}`, ...(init?.headers ?? {}) } });

  function basePath(tab: ReportTab): string {
    switch (tab) {
      case "dashboard": return "/api/v1/reports/dashboard";
      case "sales": return "/api/v1/reports/sales";
      case "payments": return "/api/v1/reports/payments";
      case "cancellations": return "/api/v1/reports/cancellations";
      case "shifts": return "/api/v1/reports/shifts/cash";
      case "inventory": return "/api/v1/reports/inventory";
      case "kitchen": return "/api/v1/reports/kitchen";
      case "audit": return "/api/v1/audit-logs";
    }
  }

  async function load() {
    if (!branchId) return;
    setState("loading"); setError(""); setNotice("");
    const qs = new URLSearchParams({ branchId, from: plusDays(from, 0), to: plusDays(to, 0) });
    if (tab === "audit") { qs.set("page", "1"); qs.set("pageSize", "50"); }
    try {
      const response = await auth(`${basePath(tab)}?${qs.toString()}`);
      if (!response.ok) throw new Error();
      setData(await response.json()); setState("idle");
    } catch {
      setState("error");
    }
  }
  useEffect(() => { if (branchId) void load(); }, [branchId, tab]);

  async function exportCsv(report: string) {
    setError(""); setNotice("");
    try {
      const range = new URLSearchParams({ branchId, from: plusDays(from, 0), to: plusDays(to, 0), format: "csv", report });
      const response = await auth(`/api/v1/reports/export?${range.toString()}`);
      if (!response.ok) throw new Error();
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `${report}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
      setNotice(t.exportDone);
    } catch {
      setError(t.error);
    }
  }

  const money = (value: number | null | undefined) => value == null ? "—" : new Intl.NumberFormat(language, { style: "currency", currency: "OMR", maximumFractionDigits: 3 }).format(value);
  const num = (value: number | null | undefined) => value == null ? "—" : new Intl.NumberFormat(language, { maximumFractionDigits: 2 }).format(value);

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <p className="mt-3 max-w-3xl text-[#64716b]">{t.intro}</p>

      <div className="mt-5 flex flex-wrap items-end gap-3 rounded-xl border border-[#d9dfd7] bg-[#edf5f1] p-3">
        <label className="block text-xs font-medium text-[#53615b]">{t.branch}
          <select value={branchId} onChange={(e) => setBranchId(e.target.value)} className="mt-1 block min-h-10 rounded-lg border border-[#cdd7d0] bg-white px-3 text-sm">
            {branches.map((b) => <option key={b.id} value={b.id}>{language === "ar" ? b.nameAr : b.nameEn}</option>)}
          </select>
        </label>
        <label className="block text-xs font-medium text-[#53615b]">{t.dateFrom}
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="mt-1 block min-h-10 rounded-lg border border-[#cdd7d0] bg-white px-3 text-sm" />
        </label>
        <label className="block text-xs font-medium text-[#53615b]">{t.dateTo}
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="mt-1 block min-h-10 rounded-lg border border-[#cdd7d0] bg-white px-3 text-sm" />
        </label>
        <button onClick={() => void load()} className="inline-flex min-h-10 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 text-xs font-semibold text-white"><RefreshCw size={15} />{t.refresh}</button>
        {(tab === "sales" || tab === "payments" || tab === "audit") && <button onClick={() => void exportCsv(tab === "audit" ? "audit" : tab)} className="inline-flex min-h-10 items-center gap-2 rounded-lg border border-[#0e5a4f] px-4 text-xs font-semibold text-[#0e5a4f]"><Download size={15} />{t.export}</button>}
      </div>

      <div className="mt-5 flex gap-1 overflow-x-auto border-b border-[#dfe5df]">
        {tabs.map(([key, label]) => <button key={key} onClick={() => setTab(key)} className={`shrink-0 min-h-11 rounded-t-lg px-4 text-sm font-medium ${tab === key ? "border-b-2 border-[#0e5a4f] text-[#08483f]" : "text-[#53615b] hover:text-[#0e5a4f]"}`}>{t[label as keyof typeof t]}</button>)}
      </div>

      {notice && <p role="status" className="mt-4 text-sm text-[#137347]">{notice}</p>}
      {error && <p role="alert" className="mt-4 text-sm text-[#b4322a]">{error}</p>}
      {state === "loading" && <div className="mt-8 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div>}
      {state === "error" && <ErrorState label={t.error} onRetry={() => void load()} retry={t.retry} />}
      {state === "idle" && data && <div className="mt-6"> {tab === "dashboard" && <DashboardView t={t} data={data} money={money} num={num} />}
        {tab === "sales" && <SalesView t={t} data={data} money={money} num={num} language={language} empty={t.empty} />}
        {tab === "payments" && <PaymentsView t={t} data={data} money={money} num={num} language={language} empty={t.empty} />}
        {tab === "cancellations" && <CancellationView t={t} data={data} money={money} num={num} language={language} empty={t.empty} />}
        {tab === "shifts" && <ShiftsView t={t} data={data} money={money} language={language} empty={t.empty} />}
        {tab === "inventory" && <InventoryView t={t} data={data} num={num} language={language} empty={t.empty} />}
        {tab === "kitchen" && <KitchenView t={t} data={data} num={num} language={language} empty={t.empty} />}
        {tab === "audit" && <AuditView t={t} data={data} language={language} empty={t.empty} />}
      </div>}
    </div>
  );
}

function Cards({ cards }: { cards: SummaryCard[] }) {
  return <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">{cards.map((c) => <div key={c.label} className="rounded-xl border border-[#dfe5df] bg-white p-4"><div className="text-sm font-medium text-[#66736d]">{c.label}</div><p className="mt-2 text-2xl font-semibold">{c.value}</p></div>)}</div>;
}

function Table({ head, rows, empty }: { head: string[]; rows: React.ReactNode[][]; empty: string }) {
  if (rows.length === 0) return <p className="mt-4 text-sm text-[#69766f]">{empty}</p>;
  return <div className="mt-3 overflow-x-auto rounded-xl border border-[#dfe5df] bg-white"><table className="min-w-full text-sm"><thead className="bg-[#f6f7f4] text-start"><tr>{head.map((h) => <th key={h} className="whitespace-nowrap px-4 py-3 text-start font-semibold text-[#53615b]">{h}</th>)}</tr></thead><tbody className="divide-y divide-[#e8ece8]">{rows.map((row, i) => <tr key={i} className="hover:bg-[#fafbfa]">{row.map((cell, j) => <td key={j} className="whitespace-nowrap px-4 py-3 text-[#2b3733]">{cell}</td>)}</tr>)}</tbody></table></div>;
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (<section className="mt-5 rounded-xl border border-[#dfe5df] bg-white p-5"><h3 className="font-semibold">{title}</h3>{children}</section>);
}

function DashboardView({ t, data, money, num }: { t: any; data: any; money: (v: number | null | undefined) => string; num: (v: number | null | undefined) => string }) {
  return <Cards cards={[{ label: t.todaySales, value: money(data.todaySales) }, { label: t.orders, value: num(data.orderCount) }, { label: t.averageOrder, value: money(data.averageOrderValue) }, { label: t.openShifts, value: num(data.openShifts) }, { label: t.cashVariance, value: money(data.cashVariance) }, { label: t.refunds, value: money(data.refunds?.amount) }, { label: t.cancelled, value: num(data.cancellations?.count) }, { label: t.cancelRate, value: `${num((data.cancellations?.rate ?? 0) * 100)}%` }, { label: t.lowStock, value: num(data.lowStockCount) }, { label: t.waste, value: num(data.waste?.quantity) }, { label: t.avgPrep, value: data.kitchen?.avgPrepMinutes == null ? "—" : `${num(data.kitchen.avgPrepMinutes)} ${t.items}` }, { label: t.lateOrders, value: num(data.kitchen?.overdue) }]} />;
}

function SalesView({ t, data, money, num, language, empty }: { t: any; data: any; money: (v: number | null | undefined) => string; num: (v: number | null | undefined) => string; language: Language; empty: string }) {
  const name = (a: string | null | undefined, e: string | null | undefined) => (language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—"));
  return (
    <>
      <Cards cards={[{ label: t.netSales, value: money(data.summary?.netSales) }, { label: t.tax, value: money(data.summary?.taxAmount) }, { label: t.discount, value: money(data.summary?.discountAmount) }, { label: t.todaySales, value: money(data.summary?.grossSales) }, { label: t.orders, value: num(data.summary?.orderCount) }, { label: t.averageOrder, value: money(data.summary?.averageOrderValue) }]} />
      <Panel title={t.product}><Table head={[t.product, t.quantity, t.netSales ?? "", t.gross]} rows={(data.byProduct ?? []).map((row: any) => [name(row.nameAr, row.nameEn), num(row.quantity), money(row.netSales), money(row.grossSales)])} empty={empty} /></Panel>
      <Panel title={t.channel}><Table head={[t.channel, t.orderCount, t.gross]} rows={(data.byChannel ?? []).map((row: any) => [name(row.channelNameAr, row.channelNameEn), num(row.orderCount), money(row.grossSales)])} empty={empty} /></Panel>
      <Panel title={t.payments}><Table head={[t.payments, t.count, t.amount]} rows={(data.byPayment ?? []).map((row: any) => [name(row.nameAr, row.nameEn), num(row.count), money(row.amount)])} empty={empty} /></Panel>
      <Panel title={t.cancellations}><Table head={[t.user, t.orderCount, t.gross]} rows={(data.byCashier ?? []).map((row: any) => [row.name ?? "—", num(row.orderCount), money(row.grossSales)])} empty={empty} /></Panel>
    </>
  );
}

function PaymentsView({ t, data, money, num, language, empty }: { t: any; data: any; money: Function; num: Function; language: Language; empty: string }) {
  const name = (a: string | null | undefined, e: string | null | undefined) => (language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—"));
  return (
    <>
      <Cards cards={[{ label: t.todaySales, value: money(data.summary?.totalCaptured) }, { label: t.refunds, value: money(data.summary?.refundsAmount) }, { label: t.count, value: num(data.summary?.paymentCount) }]} />
      <Panel title={t.payments}><Table head={[t.payments, t.count, t.todaySales]} rows={(data.byMethod ?? []).map((row: any) => [name(row.nameAr, row.nameEn), num(row.count), money(row.captured)])} empty={empty} /></Panel>
      <Panel title={t.status}><Table head={[t.status, t.count, t.amount]} rows={(data.byStatus ?? []).map((row: any) => [row.status, num(row.count), money(row.amount)])} empty={empty} /></Panel>
    </>
  );
}

function CancellationView({ t, data, money, num, language, empty }: { t: any; data: any; money: Function; num: Function; language: Language; empty: string }) {
  const name = (a: string | null | undefined, e: string | null | undefined) => (language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—"));
  const c = data.summary?.cancellations ?? {};
  return (
    <>
      <Cards cards={[{ label: t.cancelled, value: num(c.count) }, { label: t.amount, value: money(c.amount) }, { label: t.cancelRate, value: `${num((c.rate ?? 0) * 100)}%` }, { label: t.refunds, value: money(data.summary?.refunds?.amount) }]} />
      <Panel title={t.reason}><Table head={[t.reason, t.count, t.amount]} rows={(data.byReason ?? []).map((row: any) => [name(row.nameAr, row.nameEn), num(row.count), money(row.amount)])} empty={empty} /></Panel>
      <Panel title={t.hour}><Table head={[t.hour, t.count, t.amount]} rows={(data.byHour ?? []).map((row: any) => [`${row.hour}:00`, num(row.count), money(row.amount)])} empty={empty} /></Panel>
    </>
  );
}

function ShiftsView({ t, data, money, language, empty }: { t: any; data: any; money: Function; language: Language; empty: string }) {
  const when = (v: string | null | undefined) => v ? new Intl.DateTimeFormat(language, { dateStyle: "short", timeStyle: "short" }).format(new Date(v)) : "—";
  return (
    <>
      <Cards cards={[{ label: t.count, value: `${data.summary?.shiftCount ?? 0}` }, { label: t.todaySales, value: money(data.summary?.totalCashSales) }, { label: t.cashVariance, value: money(data.summary?.totalCashVariance) }]} />
      <Panel title={t.shifts}><Table head={[t.user, t.open, t.close, t.expectedCash, t.actualCash, t.variance]} rows={(data.shifts ?? []).map((row: any) => [(row.openedByName ?? "—"), when(row.openedAt), when(row.closedAt), money(row.expectedCash), money(row.actualCash), money(row.cashVariance)])} empty={empty} /></Panel>
    </>
  );
}

function InventoryView({ t, data, num, language, empty }: { t: any; data: any; num: Function; language: Language; empty: string }) {
  const name = (a: string | null | undefined, e: string | null | undefined) => (language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—"));
  return (
    <>
      <Cards cards={[{ label: t.count, value: num(data.summary?.movementCount) }]} />
      <Panel title={t.typeLabel}><Table head={[t.typeLabel, t.count, t.quantity]} rows={(data.summary?.byType ?? []).map((row: any) => [row.type, num(row.movementCount), num(row.quantity)])} empty={empty} /></Panel>
      <Panel title={t.items}><Table head={[t.product, t.balance]} rows={(data.balances ?? []).map((row: any) => [name(row.nameAr, row.nameEn), num(row.balance)])} empty={empty} /></Panel>
    </>
  );
}

function KitchenView({ t, data, num, language, empty }: { t: any; data: any; num: Function; language: Language; empty: string }) {
  const name = (a: string | null | undefined, e: string | null | undefined) => (language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—"));
  return (
    <>
      <Cards cards={[{ label: t.count, value: num(data.summary?.ticketsCreated) }, { label: t.avgPrep, value: data.summary?.avgPrepMinutes == null ? "—" : `${num(data.summary.avgPrepMinutes)} ${t.items}` }, { label: t.lateOrders, value: num(data.summary?.overdue) }]} />
      <Panel title={t.station}><Table head={[t.station, t.count, t.avgPrep]} rows={(data.byStation ?? []).map((row: any) => [name(row.nameAr, row.nameEn), num(row.tickets), row.avgPrepMinutes == null || row.avgPrepMinutes === 0 ? "—" : `${num(row.avgPrepMinutes)} ${t.items}`])} empty={empty} /></Panel>
      <Panel title={t.channel}><Table head={[t.channel, t.count]} rows={(data.byChannel ?? []).map((row: any) => [row.channel, num(row.tickets)])} empty={empty} /></Panel>
    </>
  );
}

function AuditView({ t, data, language, empty }: { t: any; data: any; language: Language; empty: string }) {
  const when = (v: string | null | undefined) => v ? new Intl.DateTimeFormat(language, { dateStyle: "short", timeStyle: "short" }).format(new Date(v)) : "—";
  return (
    <Panel title={t.audit}>
      <Table head={[t.when, t.user, t.action, t.entity, t.status]} rows={(data.items ?? []).map((row: any) => [when(row.occurredAt), row.userName ?? "—", row.action, row.entityType, row.entityId])} empty={empty} />
    </Panel>
  );
}

function ErrorState({ label, onRetry, retry }: { label: string; onRetry: () => void; retry: string }) {
  return <div role="alert" className="mt-8 rounded-xl border border-[#efc5c1] bg-[#fff5f4] p-5 text-[#9b2922]"><p>{label}</p><button onClick={onRetry} className="mt-3 font-semibold underline">{retry}</button></div>;
}
