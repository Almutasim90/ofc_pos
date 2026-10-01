import { useEffect, useState } from "react";
import { Download, RefreshCw } from "lucide-react";
import { Pagination, PAGE_SIZE } from "@/app/Pagination";
import { SearchableSelect } from "@/app/SearchableSelect";
import { store } from "@/lib/local-store";
import { paymentMethodName } from "@/lib/payment-method";
import { Button } from "@/components/ui/button";
import {
  BarList,
  ChartCard,
  ColumnChart,
  DonutChart,
  EmptyChart,
  FunnelChart,
  Gauge,
  LineChart,
  Thermometer,
} from "@/app/DashboardCharts";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

type Language = "ar" | "en";
type ReportTab =
  | "dashboard"
  | "sales"
  | "payments"
  | "cancellations"
  | "shifts"
  | "inventory"
  | "kitchen"
  | "audit"
  | "branch"
  | "profitLoss"
  | "foodCost"
  | "inventoryTrends"
  | "kitchenPerformance"
  | "cancellationAnalytics"
  | "alerts";
type LoadState = "idle" | "loading" | "error";
type Context = {
  branches: Array<{ id: string; nameAr: string; nameEn: string }>;
};

type SummaryCard = { label: string; value: string };

const copy = {
  ar: {
    title: "التقارير والتدقيق",
    intro:
      "راجع المبيعات والمدفوعات والإلغاءات والفرق النقدي والمخزون والمطبخ وسجل التدقيق.",
    loading: "جارٍ تحميل التقارير",
    error: "تعذر تحميل التقارير. تحقق من الاتصال والصلاحيات.",
    retry: "إعادة المحاولة",
    empty: "لا توجد بيانات لهذه الفترة.",
    branch: "الفرع",
    export: "تصدير CSV",
    dateFrom: "من",
    dateTo: "إلى",
    refresh: "تحديث",
    exportDone: "تم تصدير الملف.",
    sales: "المبيعات",
    payments: "المدفوعات",
    cancellations: "الإلغاء والاسترجاع",
    shifts: "الورديات والنقد",
    inventory: "المخزون",
    kitchen: "المطبخ",
    audit: "سجل التدقيق",
    dashboard: "لوحة التحكم",
    salesOverTime: "المبيعات خلال الفترة",
    salesByHour: "إجمالي المبيعات لكل ساعة",
    salesByDay: "إجمالي المبيعات لكل يوم",
    salesByChannel: "المبيعات حسب القناة",
    salesByCategory: "المبيعات حسب التصنيف",
    ordersTrend: "اتجاه عدد الطلبات",
    ordersTrendNote: "عدد الطلبات المدفوعة خلال الفترة",
    ordersByChannel: "توزيع الطلبات حسب القناة",
    ordersByChannelNote: "حصة كل قناة من عدد الطلبات",
    ordersUnit: "طلب",
    prepGauge: "سرعة المطبخ",
    prepGaugeNote: "متوسط وقت التحضير مقابل الهدف",
    prepTarget: "الهدف",
    onTarget: "ضمن الهدف",
    nearTarget: "أبطأ قليلاً",
    overTarget: "بطيء",
    noPrepData: "لا توجد طلبات مكتملة في المطبخ بعد",
    category: "التصنيف",
    salesByCategoryNote: "إجمالي المبيعات لكل تصنيف في القائمة",
    topProducts: "الأصناف الأكثر مبيعاً",
    topProductsNote: "أعلى 5 أصناف بعدد القطع",
    paymentMix: "طرق الدفع",
    paymentMixNote: "نسبة المبالغ المحصلة",
    collected: "المحصّل",
    cancelMeter: "نسبة الإلغاء",
    cancelMeterNote: "تنبيه عند 5% وخطر عند 10%",
    meterGood: "طبيعية",
    meterWarn: "مرتفعة",
    meterDanger: "مرتفعة جداً",
    showTable: "عرض البيانات كجدول",
    noChartData: "لا توجد مبيعات في هذه الفترة",
    other: "أخرى",
    period: "الفترة",
    pieces: "قطعة",
    todaySales: "مبيعات اليوم",
    netSales: "صافي المبيعات",
    tax: "الضريبة",
    orders: "الطلبات",
    averageOrder: "متوسط قيمة الطلب",
    openShifts: "وردية مفتوحة",
    cashVariance: "فرق النقدية",
    refunds: "الاسترجاعات",
    cancelled: "الطلبات الملغاة",
    cancelRate: "نسبة الإلغاء",
    lowStock: "أصناف تحت الحد",
    waste: "الهدر",
    avgPrep: "متوسط التحضير",
    lateOrders: "طلبات متأخرة",
    discount: "الخصومات",
    product: "الصنف",
    channel: "القناة",
    quantity: "الكمية",
    gross: "الإجمالي",
    count: "العدد",
    reason: "السبب",
    user: "المستخدم",
    hour: "الساعة",
    open: "الفتح",
    close: "الإغلاق",
    expectedCash: "النقد المتوقع",
    actualCash: "النقد الفعلي",
    variance: "الفرق",
    typeLabel: "النوع",
    balance: "الرصيد",
    station: "المحطة",
    status: "الحالة",
    action: "العملية",
    entity: "الكيان",
    when: "الوقت",
    amount: "المبلغ",
    items: "الصنف",
    minutes: "دقيقة",
    orderCount: "الطلبات",
    branchComparison: "مقارنة الفروع",
    profitLoss: "الأرباح والخسائر",
    foodCost: "تكلفة الأغذية",
    inventoryTrends: "اتجاهات المخزون",
    kitchenPerformance: "أداء المطبخ",
    cancellationAnalytics: "تحليلات الإلغاء",
    alerts: "تنبيهات التشغيل",
    revenue: "الإيرادات",
    grossProfit: "الربح الإجمالي",
    netProfit: "صافي الربح",
    cogs: "تكلفة البضاعة المباعة",
    margin: "الهامش",
    foodCostPercent: "نسبة تكلفة الغذاء",
    wasteCost: "تكلفة الهدر",
    purchases: "المشتريات",
    bestPerforming: "الأفضل أداءً",
    onTime: "في الوقت",
    throughput: "الإنتاجية",
    created: "منشأ",
    completed: "مكتمل",
    overcooked: "متأخر",
    consumption: "الاستهلاك",
    endingBalance: "الرصيد النهائي",
    day: "اليوم",
    saleDeduction: "خصم البيع",
    transfers: "التحويلات",
    alertLowStock: "صافي مخزون منخفض: ",
    alertNegativeStock: "مخزون سالب: ",
    alertKitchenOverdue: "طلبات مطبخ متأخرة: ",
    alertCashVariance: "فرق نقدي كبير: ",
    alertCancellationRate: "نسبة إلغاء مرتفعة: ",
    alertHighWaste: "نسبة هدر مرتفعة: ",
    alertFoodCostHigh: "تكلفة غذاء مرتفعة: ",
    alertPendingQr: "طلبات QR بانتظار الموافقة: ",
    critical: "حرج",
    warning: "تحذير",
    info: "معلومة",
    notApplicable: "—",
  },
  en: {
    title: "Reports & audit",
    intro:
      "Review sales, payments, cancellations, cash variance, inventory, kitchen throughput, and the audit trail.",
    loading: "Loading reports",
    error: "Unable to load reports. Check your connection and permissions.",
    retry: "Retry",
    empty: "No data for this period.",
    branch: "Branch",
    export: "Export CSV",
    dateFrom: "From",
    dateTo: "To",
    refresh: "Refresh",
    exportDone: "File exported.",
    sales: "Sales",
    payments: "Payments",
    cancellations: "Cancellations & refunds",
    shifts: "Shifts & cash",
    inventory: "Inventory",
    kitchen: "Kitchen",
    audit: "Audit log",
    dashboard: "Dashboard",
    salesOverTime: "Sales over the period",
    salesByHour: "Gross sales per hour",
    salesByDay: "Gross sales per day",
    salesByChannel: "Sales by channel",
    salesByCategory: "Sales by category",
    ordersTrend: "Orders trend",
    ordersTrendNote: "Paid orders over the period",
    ordersByChannel: "Orders by channel",
    ordersByChannelNote: "Each channel's share of orders",
    ordersUnit: "orders",
    prepGauge: "Kitchen speed",
    prepGaugeNote: "Average preparation time against the target",
    prepTarget: "Target",
    onTarget: "On target",
    nearTarget: "A little slow",
    overTarget: "Slow",
    noPrepData: "No completed kitchen orders yet",
    category: "Category",
    salesByCategoryNote: "Gross sales per menu category",
    topProducts: "Top-selling items",
    topProductsNote: "Top 5 items by quantity",
    paymentMix: "Payment methods",
    paymentMixNote: "Share of collected amounts",
    collected: "Collected",
    cancelMeter: "Cancellation rate",
    cancelMeterNote: "Warning at 5%, critical at 10%",
    meterGood: "Normal",
    meterWarn: "High",
    meterDanger: "Very high",
    showTable: "Show data as a table",
    noChartData: "No sales in this period",
    other: "Other",
    period: "Period",
    pieces: "pcs",
    todaySales: "Today's sales",
    netSales: "Net sales",
    tax: "Tax",
    orders: "Orders",
    averageOrder: "Average order",
    openShifts: "Open shifts",
    cashVariance: "Cash variance",
    refunds: "Refunds",
    cancelled: "Cancelled orders",
    cancelRate: "Cancellation rate",
    lowStock: "Low-stock items",
    waste: "Waste",
    avgPrep: "Avg prep time",
    lateOrders: "Late orders",
    discount: "Discounts",
    product: "Product",
    channel: "Channel",
    quantity: "Qty",
    gross: "Gross",
    count: "Count",
    reason: "Reason",
    user: "User",
    hour: "Hour",
    open: "Opened",
    close: "Closed",
    expectedCash: "Expected cash",
    actualCash: "Actual cash",
    variance: "Variance",
    typeLabel: "Type",
    balance: "Balance",
    station: "Station",
    status: "Status",
    action: "Action",
    entity: "Entity",
    when: "When",
    amount: "Amount",
    items: "Item",
    minutes: "min",
    orderCount: "Orders",
    branchComparison: "Branch comparison",
    profitLoss: "Profit & loss",
    foodCost: "Food cost",
    inventoryTrends: "Inventory trends",
    kitchenPerformance: "Kitchen performance",
    cancellationAnalytics: "Cancellation analytics",
    alerts: "Operational alerts",
    revenue: "Revenue",
    grossProfit: "Gross profit",
    netProfit: "Net profit",
    cogs: "Cost of goods sold",
    margin: "Margin",
    foodCostPercent: "Food cost %",
    wasteCost: "Waste cost",
    purchases: "Purchases",
    bestPerforming: "Best performer",
    onTime: "On time",
    throughput: "Throughput",
    created: "Created",
    completed: "Completed",
    overcooked: "Late",
    consumption: "Consumption",
    endingBalance: "Ending balance",
    day: "Day",
    saleDeduction: "Sale deduction",
    transfers: "Transfers",
    alertLowStock: "Low stock: ",
    alertNegativeStock: "Negative stock: ",
    alertKitchenOverdue: "Overdue kitchen tickets: ",
    alertCashVariance: "Large cash variance: ",
    alertCancellationRate: "High cancellation rate: ",
    alertHighWaste: "High waste rate: ",
    alertFoodCostHigh: "High food cost: ",
    alertPendingQr: "QR orders awaiting approval: ",
    critical: "Critical",
    warning: "Warning",
    info: "Info",
    notApplicable: "—",
  },
} as const;

const tabs: Array<[ReportTab, string]> = [
  ["dashboard", "dashboard"],
  ["sales", "sales"],
  ["payments", "payments"],
  ["cancellations", "cancellations"],
  ["shifts", "shifts"],
  ["inventory", "inventory"],
  ["kitchen", "kitchen"],
  ["branch", "branchComparison"],
  ["profitLoss", "profitLoss"],
  ["foodCost", "foodCost"],
  ["inventoryTrends", "inventoryTrends"],
  ["kitchenPerformance", "kitchenPerformance"],
  ["cancellationAnalytics", "cancellationAnalytics"],
  ["alerts", "alerts"],
  ["audit", "audit"],
];

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
    fetch("/api/v1/pos/context", {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then((r) => (r.ok ? (r.json() as Promise<Context>) : Promise.reject()))
      .then((context) => {
        setBranches(context.branches);
        if (!branchId && context.branches[0])
          setBranchId(context.branches[0].id);
      })
      .catch(() => undefined);
  }, [token]);

  const auth = (path: string, init?: RequestInit) =>
    fetch(path, {
      ...init,
      headers: { Authorization: `Bearer ${token}`, ...(init?.headers ?? {}) },
    });

  function basePath(tab: ReportTab): string {
    switch (tab) {
      case "dashboard":
        return "/api/v1/reports/dashboard";
      case "sales":
        return "/api/v1/reports/sales";
      case "payments":
        return "/api/v1/reports/payments";
      case "cancellations":
        return "/api/v1/reports/cancellations";
      case "shifts":
        return "/api/v1/reports/shifts/cash";
      case "inventory":
        return "/api/v1/reports/inventory";
      case "kitchen":
        return "/api/v1/reports/kitchen";
      case "audit":
        return "/api/v1/audit-logs";
      case "branch":
        return "/api/v1/reports/branch-comparison";
      case "profitLoss":
        return "/api/v1/reports/profit-loss";
      case "foodCost":
        return "/api/v1/reports/food-cost";
      case "inventoryTrends":
        return "/api/v1/reports/inventory-trends";
      case "kitchenPerformance":
        return "/api/v1/reports/kitchen-performance";
      case "cancellationAnalytics":
        return "/api/v1/reports/cancellation-analytics";
      case "alerts":
        return "/api/v1/reports/alerts";
    }
  }

  function exportCode(tab: ReportTab): string | null {
    switch (tab) {
      case "sales":
        return "sales";
      case "payments":
        return "payments";
      case "audit":
        return "audit";
      case "branch":
        return "branch-comparison";
      case "profitLoss":
        return "profit-loss";
      case "foodCost":
        return "food-cost";
      case "inventoryTrends":
        return "inventory-trends";
      case "kitchenPerformance":
        return "kitchen-performance";
      case "cancellationAnalytics":
        return "cancellation-analytics";
      case "alerts":
        return "alerts";
      default:
        return null;
    }
  }

  async function load() {
    if (!branchId) return;
    setState("loading");
    setError("");
    setNotice("");
    const qs = new URLSearchParams({
      branchId,
      from: plusDays(from, 0),
      to: plusDays(to, 0),
    });
    if (tab === "audit") {
      qs.set("page", "1");
      qs.set("pageSize", "50");
    }
    try {
      const response = await auth(`${basePath(tab)}?${qs.toString()}`);
      if (!response.ok) throw new Error();
      const body = await response.json();
      if (tab === "dashboard") {
        const sales = await auth(`/api/v1/reports/sales?${qs.toString()}`);
        body.sales = sales.ok ? await sales.json() : null;
      }
      setData(body);
      setState("idle");
    } catch {
      setState("error");
    }
  }
  useEffect(() => {
    if (branchId) void load();
  }, [branchId, tab]);

  async function exportCsv(code: string) {
    setError("");
    setNotice("");
    try {
      const range = new URLSearchParams({
        branchId,
        from: plusDays(from, 0),
        to: plusDays(to, 0),
        format: "csv",
        report: code,
      });
      const response = await auth(`/api/v1/reports/export?${range.toString()}`);
      if (!response.ok) throw new Error();
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `${code}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
      setNotice(t.exportDone);
    } catch {
      setError(t.error);
    }
  }

  const money = (value: number | null | undefined) =>
    value == null
      ? "—"
      : new Intl.NumberFormat(language, {
          style: "currency",
          currency: "OMR",
          maximumFractionDigits: 3,
        }).format(value);
  const num = (value: number | null | undefined) =>
    value == null
      ? "—"
      : new Intl.NumberFormat(language, { maximumFractionDigits: 2 }).format(
          value,
        );

  return (
    <div>
      <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">
        {t.title}
      </h1>
      <p className="mt-3 max-w-3xl text-muted-foreground">{t.intro}</p>

      <div className="mt-5 flex flex-wrap items-end gap-3 rounded-xl border border-border bg-accent p-3">
        <SearchableSelect
          label={t.branch}
          value={branchId}
          onChange={setBranchId}
        >
          {branches.map((b) => (
            <option key={b.id} value={b.id}>
              {language === "ar" ? b.nameAr : b.nameEn}
            </option>
          ))}
        </SearchableSelect>
        <label className="block text-xs font-medium text-muted-foreground">
          {t.dateFrom}
          <Input
            type="date"
            value={from}
            onChange={(e) => setFrom(e.target.value)}
            className="mt-1 block min-h-10 rounded-lg border border-border bg-card px-3 text-sm"
          />
        </label>
        <label className="block text-xs font-medium text-muted-foreground">
          {t.dateTo}
          <Input
            type="date"
            value={to}
            onChange={(e) => setTo(e.target.value)}
            className="mt-1 block min-h-10 rounded-lg border border-border bg-card px-3 text-sm"
          />
        </label>
        <Button
          onClick={() => void load()}
          className="inline-flex min-h-10 items-center gap-2 rounded-lg bg-primary px-4 text-xs font-semibold text-primary-foreground"
        >
          <RefreshCw size={15} />
          {t.refresh}
        </Button>
        {(() => {
          const code = exportCode(tab);
          return code ? (
            <Button
              onClick={() => void exportCsv(code)}
              className="inline-flex min-h-10 items-center gap-2 rounded-lg border border-primary px-4 text-xs font-semibold text-primary"
            >
              <Download size={15} />
              {t.export}
            </Button>
          ) : null;
        })()}
      </div>

      <div className="mt-5 flex gap-1 overflow-x-auto border-b border-border">
        {tabs.map(([key, label]) => (
          <Button
            key={key}
            onClick={() => setTab(key)}
            className={`shrink-0 min-h-11 rounded-t-lg px-4 text-sm font-medium ${tab === key ? "border-b-2 border-primary text-primary" : "text-muted-foreground hover:text-primary"}`}
          >
            {t[label as keyof typeof t]}
          </Button>
        ))}
      </div>

      {notice && (
        <p role="status" className="mt-4 text-sm text-success">
          {notice}
        </p>
      )}
      {error && (
        <p role="alert" className="mt-4 text-sm text-destructive">
          {error}
        </p>
      )}
      {state === "loading" && (
        <div className="mt-8 flex items-center gap-3 text-muted-foreground">
          <RefreshCw className="animate-spin" size={20} />
          {t.loading}
        </div>
      )}
      {state === "error" && (
        <ErrorState
          label={t.error}
          onRetry={() => void load()}
          retry={t.retry}
        />
      )}
      {state === "idle" && data && (
        <div className="mt-6">
          {" "}
          {tab === "dashboard" && (
            <DashboardView
              t={t}
              data={data}
              money={money}
              num={num}
              language={language}
              from={from}
              to={to}
            />
          )}
          {tab === "sales" && (
            <SalesView
              t={t}
              data={data}
              money={money}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "payments" && (
            <PaymentsView
              t={t}
              data={data}
              money={money}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "cancellations" && (
            <CancellationView
              t={t}
              data={data}
              money={money}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "shifts" && (
            <ShiftsView
              t={t}
              data={data}
              money={money}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "inventory" && (
            <InventoryView
              t={t}
              data={data}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "kitchen" && (
            <KitchenView
              t={t}
              data={data}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "branch" && (
            <BranchComparisonView
              t={t}
              data={data}
              money={money}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "profitLoss" && (
            <ProfitLossView
              t={t}
              data={data}
              money={money}
              num={num}
              empty={t.empty}
              language={language}
            />
          )}
          {tab === "foodCost" && (
            <FoodCostView
              t={t}
              data={data}
              money={money}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "inventoryTrends" && (
            <InventoryTrendsView
              t={t}
              data={data}
              money={money}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "kitchenPerformance" && (
            <KitchenPerformanceView
              t={t}
              data={data}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "cancellationAnalytics" && (
            <CancellationAnalyticsView
              t={t}
              data={data}
              money={money}
              num={num}
              language={language}
              empty={t.empty}
            />
          )}
          {tab === "alerts" && (
            <AlertsView t={t} data={data} language={language} />
          )}
          {tab === "audit" && (
            <AuditView t={t} data={data} language={language} empty={t.empty} />
          )}
        </div>
      )}
    </div>
  );
}

function Cards({ cards }: { cards: SummaryCard[] }) {
  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      {cards.map((c) => (
        <div
          key={c.label}
          className="rounded-xl border border-border bg-card p-4"
        >
          <div className="text-sm font-medium text-muted-foreground">
            {c.label}
          </div>
          <p className="mt-2 text-lg font-semibold break-words sm:text-2xl">
            {c.value}
          </p>
        </div>
      ))}
    </div>
  );
}

function ReportTable({
  head,
  rows,
  empty,
  language,
}: {
  head: string[];
  rows: React.ReactNode[][];
  empty: string;
  language: Language;
}) {
  const [page, setPage] = useState(1);
  useEffect(() => {
    setPage(1);
  }, [rows.length]);
  if (rows.length === 0)
    return <p className="mt-4 text-sm text-muted-foreground">{empty}</p>;
  const pageRows = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  return (
    <div className="mt-3 overflow-x-auto rounded-xl border border-border bg-card">
      <Table className="min-w-full text-sm">
        <TableHeader className="bg-muted text-start">
          <TableRow>
            {head.map((h) => (
              <TableHead
                key={h}
                className="whitespace-nowrap px-4 py-3 text-start font-semibold text-muted-foreground"
              >
                {h}
              </TableHead>
            ))}
          </TableRow>
        </TableHeader>
        <TableBody className="divide-y divide-border">
          {pageRows.map((row, i) => (
            <TableRow key={i} className="hover:bg-muted">
              {row.map((cell, j) => (
                <TableCell
                  key={j}
                  className="whitespace-nowrap px-4 py-3 text-foreground"
                >
                  {cell}
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {rows.length > PAGE_SIZE && (
        <div className="border-t border-border px-4 py-3">
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            total={rows.length}
            onPageChange={setPage}
            language={language}
          />
        </div>
      )}
    </div>
  );
}

function Panel({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="mt-5 rounded-xl border border-border bg-card p-5">
      <h3 className="font-semibold">{title}</h3>
      {children}
    </section>
  );
}

function DashboardView({
  t,
  data,
  money,
  num,
  language,
  from,
  to,
}: {
  t: any;
  data: any;
  money: (v: number | null | undefined) => string;
  num: (v: number | null | undefined) => string;
  language: Language;
  from: string;
  to: string;
}) {
  return (
    <>
      <DashboardCards t={t} data={data} money={money} num={num} />
      <DashboardCharts
        t={t}
        data={data}
        money={money}
        num={num}
        language={language}
        from={from}
        to={to}
      />
    </>
  );
}

function DashboardCharts({
  t,
  data,
  money,
  num,
  language,
  from,
  to,
}: {
  t: any;
  data: any;
  money: (v: number | null | undefined) => string;
  num: (v: number | null | undefined) => string;
  language: Language;
  from: string;
  to: string;
}) {
  const sales = data.sales;
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  const amount = (v: number) => money(v);
  const axis = (v: number) =>
    new Intl.NumberFormat(language, {
      notation: "compact",
      maximumFractionDigits: 1,
    }).format(v);

  // Within one day: hourly buckets (local time) with empty hours filled; longer ranges: one column per day.
  const singleDay = from === to;
  let points: Array<{ key: string; label: string; value: number }> = [];
  let orderPoints: Array<{ key: string; label: string; value: number }> = [];
  if (sales && singleDay) {
    const byHour = new Map<number, number>();
    const ordersByHour = new Map<number, number>();
    for (const row of sales.hourly ?? []) {
      byHour.set(new Date(row.hour).getTime(), row.grossSales);
      ordersByHour.set(new Date(row.hour).getTime(), row.orderCount);
    }
    const hours = [...byHour.keys()];
    if (hours.length) {
      const first = Math.min(...hours);
      const last = Math.max(...hours);
      for (let at = first; at <= last; at += 3_600_000) {
        const label = new Date(at).toLocaleTimeString(language, {
          hour: "numeric",
        });
        points.push({ key: String(at), label, value: byHour.get(at) ?? 0 });
        orderPoints.push({
          key: String(at),
          label,
          value: ordersByHour.get(at) ?? 0,
        });
      }
    }
  } else if (sales) {
    const byDay = new Map<string, number>();
    const ordersByDay = new Map<string, number>();
    for (const row of sales.daily ?? []) {
      byDay.set(String(row.date).slice(0, 10), row.grossSales);
      ordersByDay.set(String(row.date).slice(0, 10), row.orderCount);
    }
    const day = new Date(`${from}T00:00:00Z`);
    const end = new Date(`${to}T00:00:00Z`);
    for (let i = 0; day <= end && i < 400; i++) {
      const key = day.toISOString().slice(0, 10);
      const label = day.toLocaleDateString(language, {
        day: "numeric",
        month: "numeric",
        timeZone: "UTC",
      });
      points.push({ key, label, value: byDay.get(key) ?? 0 });
      orderPoints.push({ key, label, value: ordersByDay.get(key) ?? 0 });
      day.setUTCDate(day.getUTCDate() + 1);
    }
  }
  const hasSales = points.some((p) => p.value > 0);

  // Donut: up to five channels, the rest folded into "Other" (part-to-whole reads at a glance only <= 6).
  const channelOrderRows = [...(sales?.byChannel ?? [])]
    .sort((a: any, b: any) => b.orderCount - a.orderCount)
    .map((row: any) => ({
      key: String(row.channelId),
      label: name(row.channelNameAr, row.channelNameEn),
      value: row.orderCount,
    }));
  const channelOrders =
    channelOrderRows.length > 6
      ? [
          ...channelOrderRows.slice(0, 5),
          {
            key: "other",
            label: t.other,
            value: channelOrderRows
              .slice(5)
              .reduce((sum: number, x: { value: number }) => sum + x.value, 0),
          },
        ]
      : channelOrderRows;
  const prepMinutes: number | null = data.kitchen?.avgPrepMinutes ?? null;
  const prepTarget = 15;
  const channels = [...(sales?.byChannel ?? [])]
    .sort((a: any, b: any) => b.grossSales - a.grossSales)
    .map((row: any) => ({
      key: String(row.channelId),
      label: name(row.channelNameAr, row.channelNameEn),
      value: row.grossSales,
    }));
  // Up to seven categories get their own colour; the rest fold into "Other" (never a generated hue).
  const categoryRows = [...(sales?.byCategory ?? [])]
    .sort((a: any, b: any) => b.grossSales - a.grossSales)
    .map((row: any) => ({
      key: String(row.categoryId),
      label: name(row.categoryNameAr, row.categoryNameEn),
      value: row.grossSales,
    }));
  const categories =
    categoryRows.length > 8
      ? [
          ...categoryRows.slice(0, 7),
          {
            key: "other",
            label: t.other,
            value: categoryRows
              .slice(7)
              .reduce((sum: number, x: { value: number }) => sum + x.value, 0),
          },
        ]
      : categoryRows;
  const products = [...(sales?.byProduct ?? [])]
    .sort((a: any, b: any) => b.quantity - a.quantity)
    .slice(0, 5)
    .map((row: any) => ({
      key: String(row.productId),
      label: name(row.nameAr, row.nameEn),
      value: row.quantity,
    }));
  // Three validated categorical slots; anything beyond folds into "Other".
  const methods = [...(sales?.byPayment ?? [])]
    .sort((a: any, b: any) => b.amount - a.amount)
    .map((row: any) => ({
      key: String(row.paymentMethodId),
      label: name(row.nameAr, row.nameEn),
      value: row.amount,
    }));
  const payments =
    methods.length > 3
      ? [
          ...methods.slice(0, 3),
          {
            key: "other",
            label: t.other,
            value: methods
              .slice(3)
              .reduce((sum: number, x: { value: number }) => sum + x.value, 0),
          },
        ]
      : methods;

  return (
    <div className="viz-grid-layout">
      <ChartCard
        wide
        title={t.salesOverTime}
        subtitle={singleDay ? t.salesByHour : t.salesByDay}
        tableLabel={t.showTable}
        table={{
          head: [t.period, t.todaySales],
          rows: points.map((p) => [p.label, amount(p.value)]),
        }}
      >
        {hasSales ? (
          <ColumnChart
            points={points}
            format={amount}
            formatAxis={axis}
            label={t.salesOverTime}
          />
        ) : (
          <EmptyChart text={t.noChartData} />
        )}
      </ChartCard>
      <ChartCard
        wide
        title={t.ordersTrend}
        subtitle={t.ordersTrendNote}
        tableLabel={t.showTable}
        table={{
          head: [t.period, t.orders],
          rows: orderPoints.map((p) => [p.label, num(p.value)]),
        }}
      >
        {orderPoints.some((p) => p.value > 0) ? (
          <LineChart
            points={orderPoints}
            format={(v) => `${num(v)} ${t.ordersUnit}`}
            formatAxis={axis}
            label={t.ordersTrend}
          />
        ) : (
          <EmptyChart text={t.noChartData} />
        )}
      </ChartCard>
      <ChartCard
        wide
        title={t.salesByCategory}
        subtitle={t.salesByCategoryNote}
        tableLabel={t.showTable}
        table={{
          head: [t.category, t.todaySales],
          rows: categories.map((c: any) => [c.label, amount(c.value)]),
        }}
      >
        {categories.length ? (
          <ColumnChart
            points={categories}
            format={amount}
            formatAxis={axis}
            label={t.salesByCategory}
            categorical
          />
        ) : (
          <EmptyChart text={t.noChartData} />
        )}
      </ChartCard>
      <ChartCard
        title={t.salesByChannel}
        tableLabel={t.showTable}
        table={{
          head: [t.channel, t.todaySales],
          rows: channels.map((c: any) => [c.label, amount(c.value)]),
        }}
      >
        {channels.length ? (
          <BarList items={channels} format={amount} categorical />
        ) : (
          <EmptyChart text={t.noChartData} />
        )}
      </ChartCard>
      <ChartCard
        title={t.ordersByChannel}
        subtitle={t.ordersByChannelNote}
        tableLabel={t.showTable}
        table={{
          head: [t.channel, t.orders],
          rows: channelOrders.map((c: any) => [c.label, num(c.value)]),
        }}
      >
        {channelOrders.length ? (
          <DonutChart
            items={channelOrders}
            format={(v) => num(v)}
            centerLabel={t.ordersUnit}
          />
        ) : (
          <EmptyChart text={t.noChartData} />
        )}
      </ChartCard>
      <ChartCard
        title={t.prepGauge}
        subtitle={t.prepGaugeNote}
        tableLabel={t.showTable}
      >
        {prepMinutes == null ? (
          <EmptyChart text={t.noPrepData} />
        ) : (
          <Gauge
            value={prepMinutes}
            max={prepTarget * 2}
            target={prepTarget}
            warnAt={prepTarget * 1.33}
            display={`${num(prepMinutes)} ${t.minutes}`}
            caption={`${t.prepTarget}: ${prepTarget} ${t.minutes}`}
            states={{
              good: t.onTarget,
              warn: t.nearTarget,
              danger: t.overTarget,
            }}
          />
        )}
      </ChartCard>
      <ChartCard
        title={t.cancelMeter}
        subtitle={t.cancelMeterNote}
        tableLabel={t.showTable}
      >
        <Thermometer
          value={data.cancellations?.rate ?? 0}
          warnAt={0.05}
          dangerAt={0.1}
          label={t.cancelMeter}
          states={{
            good: t.meterGood,
            warn: t.meterWarn,
            danger: t.meterDanger,
          }}
        >
          <p className="viz-muted">
            {num(data.cancellations?.count)} {t.cancelled}
          </p>
          <p className="viz-muted">{money(data.cancellations?.amount)}</p>
        </Thermometer>
      </ChartCard>
      <ChartCard
        span={3}
        title={t.topProducts}
        subtitle={t.topProductsNote}
        tableLabel={t.showTable}
        table={{
          head: [t.product, t.quantity],
          rows: products.map((p: any) => [p.label, num(p.value)]),
        }}
      >
        {products.length ? (
          <FunnelChart
            items={products}
            format={(v) => `${num(v)} ${t.pieces}`}
          />
        ) : (
          <EmptyChart text={t.noChartData} />
        )}
      </ChartCard>
      <ChartCard
        span={1}
        title={t.paymentMix}
        subtitle={t.paymentMixNote}
        tableLabel={t.showTable}
        table={{
          head: [t.payments, t.amount],
          rows: payments.map((p: any) => [p.label, amount(p.value)]),
        }}
      >
        {payments.length ? (
          <DonutChart
            items={payments}
            format={amount}
            centerLabel={t.collected}
            stacked
          />
        ) : (
          <EmptyChart text={t.noChartData} />
        )}
      </ChartCard>
    </div>
  );
}

function DashboardCards({
  t,
  data,
  money,
  num,
}: {
  t: any;
  data: any;
  money: (v: number | null | undefined) => string;
  num: (v: number | null | undefined) => string;
}) {
  return (
    <Cards
      cards={[
        { label: t.todaySales, value: money(data.todaySales) },
        { label: t.orders, value: num(data.orderCount) },
        { label: t.averageOrder, value: money(data.averageOrderValue) },
        { label: t.openShifts, value: num(data.openShifts) },
        { label: t.cashVariance, value: money(data.cashVariance) },
        { label: t.refunds, value: money(data.refunds?.amount) },
        { label: t.cancelled, value: num(data.cancellations?.count) },
        {
          label: t.cancelRate,
          value: `${num((data.cancellations?.rate ?? 0) * 100)}%`,
        },
        { label: t.lowStock, value: num(data.lowStockCount) },
        { label: t.waste, value: num(data.waste?.quantity) },
        {
          label: t.avgPrep,
          value:
            data.kitchen?.avgPrepMinutes == null
              ? "—"
              : `${num(data.kitchen.avgPrepMinutes)} ${t.minutes}`,
        },
        { label: t.lateOrders, value: num(data.kitchen?.overdue) },
      ]}
    />
  );
}

function SalesView({
  t,
  data,
  money,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  money: (v: number | null | undefined) => string;
  num: (v: number | null | undefined) => string;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  return (
    <>
      <Cards
        cards={[
          { label: t.netSales, value: money(data.summary?.netSales) },
          { label: t.tax, value: money(data.summary?.taxAmount) },
          { label: t.discount, value: money(data.summary?.discountAmount) },
          { label: t.todaySales, value: money(data.summary?.grossSales) },
          { label: t.orders, value: num(data.summary?.orderCount) },
          {
            label: t.averageOrder,
            value: money(data.summary?.averageOrderValue),
          },
        ]}
      />
      <Panel title={t.product}>
        <ReportTable
          language={language}
          head={[t.product, t.quantity, t.netSales ?? "", t.gross]}
          rows={(data.byProduct ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.quantity),
            money(row.netSales),
            money(row.grossSales),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.channel}>
        <ReportTable
          language={language}
          head={[t.channel, t.orderCount, t.gross]}
          rows={(data.byChannel ?? []).map((row: any) => [
            name(row.channelNameAr, row.channelNameEn),
            num(row.orderCount),
            money(row.grossSales),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.payments}>
        <ReportTable
          language={language}
          head={[t.payments, t.count, t.amount]}
          rows={(data.byPayment ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.count),
            money(row.amount),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.cancellations}>
        <ReportTable
          language={language}
          head={[t.user, t.orderCount, t.gross]}
          rows={(data.byCashier ?? []).map((row: any) => [
            row.name ?? "—",
            num(row.orderCount),
            money(row.grossSales),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function PaymentsView({
  t,
  data,
  money,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  money: Function;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    paymentMethodName(language, { nameAr: a, nameEn: e });
  return (
    <>
      <Cards
        cards={[
          { label: t.todaySales, value: money(data.summary?.totalCaptured) },
          { label: t.refunds, value: money(data.summary?.refundsAmount) },
          { label: t.count, value: num(data.summary?.paymentCount) },
        ]}
      />
      <Panel title={t.payments}>
        <ReportTable
          language={language}
          head={[t.payments, t.count, t.todaySales]}
          rows={(data.byMethod ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.count),
            money(row.captured),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.status}>
        <ReportTable
          language={language}
          head={[t.status, t.count, t.amount]}
          rows={(data.byStatus ?? []).map((row: any) => [
            row.status,
            num(row.count),
            money(row.amount),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function CancellationView({
  t,
  data,
  money,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  money: Function;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  const c = data.summary?.cancellations ?? {};
  return (
    <>
      <Cards
        cards={[
          { label: t.cancelled, value: num(c.count) },
          { label: t.amount, value: money(c.amount) },
          { label: t.cancelRate, value: `${num((c.rate ?? 0) * 100)}%` },
          { label: t.refunds, value: money(data.summary?.refunds?.amount) },
        ]}
      />
      <Panel title={t.reason}>
        <ReportTable
          language={language}
          head={[t.reason, t.count, t.amount]}
          rows={(data.byReason ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.count),
            money(row.amount),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.hour}>
        <ReportTable
          language={language}
          head={[t.hour, t.count, t.amount]}
          rows={(data.byHour ?? []).map((row: any) => [
            `${row.hour}:00`,
            num(row.count),
            money(row.amount),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function ShiftsView({
  t,
  data,
  money,
  language,
  empty,
}: {
  t: any;
  data: any;
  money: Function;
  language: Language;
  empty: string;
}) {
  const when = (v: string | null | undefined) =>
    v
      ? new Intl.DateTimeFormat(language, {
          dateStyle: "short",
          timeStyle: "short",
        }).format(new Date(v))
      : "—";
  return (
    <>
      <Cards
        cards={[
          { label: t.count, value: `${data.summary?.shiftCount ?? 0}` },
          { label: t.todaySales, value: money(data.summary?.totalCashSales) },
          {
            label: t.cashVariance,
            value: money(data.summary?.totalCashVariance),
          },
        ]}
      />
      <Panel title={t.shifts}>
        <ReportTable
          language={language}
          head={[
            t.user,
            t.open,
            t.close,
            t.expectedCash,
            t.actualCash,
            t.variance,
          ]}
          rows={(data.shifts ?? []).map((row: any) => [
            row.openedByName ?? "—",
            when(row.openedAt),
            when(row.closedAt),
            money(row.expectedCash),
            money(row.actualCash),
            money(row.cashVariance),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function InventoryView({
  t,
  data,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  return (
    <>
      <Cards
        cards={[{ label: t.count, value: num(data.summary?.movementCount) }]}
      />
      <Panel title={t.typeLabel}>
        <ReportTable
          language={language}
          head={[t.typeLabel, t.count, t.quantity]}
          rows={(data.summary?.byType ?? []).map((row: any) => [
            row.type,
            num(row.movementCount),
            num(row.quantity),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.items}>
        <ReportTable
          language={language}
          head={[t.product, t.balance]}
          rows={(data.balances ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.balance),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function KitchenView({
  t,
  data,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  return (
    <>
      <Cards
        cards={[
          { label: t.count, value: num(data.summary?.ticketsCreated) },
          {
            label: t.avgPrep,
            value:
              data.summary?.avgPrepMinutes == null
                ? "—"
                : `${num(data.summary.avgPrepMinutes)} ${t.items}`,
          },
          { label: t.lateOrders, value: num(data.summary?.overdue) },
        ]}
      />
      <Panel title={t.station}>
        <ReportTable
          language={language}
          head={[t.station, t.count, t.avgPrep]}
          rows={(data.byStation ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.tickets),
            row.avgPrepMinutes == null || row.avgPrepMinutes === 0
              ? "—"
              : `${num(row.avgPrepMinutes)} ${t.items}`,
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.channel}>
        <ReportTable
          language={language}
          head={[t.channel, t.count]}
          rows={(data.byChannel ?? []).map((row: any) => [
            row.channel,
            num(row.tickets),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function AuditView({
  t,
  data,
  language,
  empty,
}: {
  t: any;
  data: any;
  language: Language;
  empty: string;
}) {
  const when = (v: string | null | undefined) =>
    v
      ? new Intl.DateTimeFormat(language, {
          dateStyle: "short",
          timeStyle: "short",
        }).format(new Date(v))
      : "—";
  return (
    <Panel title={t.audit}>
      <ReportTable
        language={language}
        head={[t.when, t.user, t.action, t.entity, t.status]}
        rows={(data.items ?? []).map((row: any) => [
          when(row.occurredAt),
          row.userName ?? "—",
          row.action,
          row.entityType,
          row.entityId,
        ])}
        empty={empty}
      />
    </Panel>
  );
}

function BranchComparisonView({
  t,
  data,
  money,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  money: Function;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  const totals = data.totals ?? {};
  const bestRow = data.bestPerforming?.branchId
    ? (data.rows ?? []).find(
        (r: any) => r.branchId === data.bestPerforming.branchId,
      )
    : null;
  return (
    <>
      <Cards
        cards={[
          { label: t.netSales, value: money(totals.netSales) },
          { label: t.gross, value: money(totals.grossSales) },
          { label: t.orders, value: num(totals.orderCount) },
          {
            label: t.bestPerforming,
            value: bestRow
              ? name(bestRow.branchNameAr, bestRow.branchNameEn)
              : "—",
          },
          {
            label: t.cancelRate,
            value: `${num((totals.cancellationRate ?? 0) * 100)}%`,
          },
          { label: t.lowStock, value: num(totals.lowStockCount) },
        ]}
      />
      <Panel title={t.branchComparison}>
        <ReportTable
          language={language}
          head={[
            t.branch,
            t.orderCount,
            t.netSales,
            t.gross,
            t.averageOrder,
            t.cancelRate,
            t.waste,
            t.lowStock,
            t.lateOrders,
          ]}
          rows={(data.rows ?? []).map((row: any) => [
            name(row.branchNameAr, row.branchNameEn),
            num(row.orderCount),
            money(row.netSales),
            money(row.grossSales),
            money(row.averageOrderValue),
            `${num((row.cancellations?.rate ?? 0) * 100)}%`,
            num(row.waste?.quantity),
            num(row.lowStockCount),
            num(row.kitchen?.overdue),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function ProfitLossView({
  t,
  data,
  money,
  num,
  empty,
  language,
}: {
  t: any;
  data: any;
  money: Function;
  num: Function;
  empty: string;
  language: Language;
}) {
  const s = data.summary ?? {};
  return (
    <>
      <Cards
        cards={[
          { label: t.revenue, value: money(s.grossRevenue) },
          { label: t.netSales, value: money(s.netRevenue) },
          { label: t.cogs, value: money(s.cogs) },
          { label: t.grossProfit, value: money(s.grossProfit) },
          { label: t.netProfit, value: money(s.netProfit) },
          { label: t.foodCostPercent, value: `${num(s.foodCostPercent)}%` },
          { label: t.wasteCost, value: money(s.wasteCost) },
          { label: t.refunds, value: money(s.refunds) },
          { label: t.cancelled, value: money(s.cancellations) },
          { label: t.purchases, value: money(s.purchases) },
        ]}
      />
      <Panel title={t.cogs}>
        <ReportTable
          language={language}
          head={[t.cogs, t.amount]}
          rows={[
            [t.cogs, money(s.cogs)],
            [t.wasteCost, money(s.wasteCost)],
            [t.refunds, money(s.refunds)],
            [t.cancelled, money(s.cancellations)],
          ]}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function FoodCostView({
  t,
  data,
  money,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  money: Function;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  const s = data.summary ?? {};
  return (
    <>
      <Cards
        cards={[
          { label: t.gross, value: money(s.grossSales) },
          { label: t.cogs, value: money(s.cogs) },
          { label: t.grossProfit, value: money(s.grossMargin) },
          { label: t.margin, value: `${num(s.grossMarginPercent)}%` },
          { label: t.foodCostPercent, value: `${num(s.foodCostPercent)}%` },
        ]}
      />
      <Panel title={t.product}>
        <ReportTable
          language={language}
          head={[
            t.product,
            t.quantity,
            t.gross,
            t.cogs,
            t.foodCostPercent,
            t.margin,
          ]}
          rows={(data.rows ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.quantity),
            money(row.grossSales),
            money(row.cogs),
            `${num(row.foodCostPercent)}%`,
            `${num(row.grossMarginPercent)}%`,
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.foodCostPercent}>
        <ReportTable
          language={language}
          head={[t.product, t.foodCostPercent, t.margin]}
          rows={(data.topMargin ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            `${num(row.foodCostPercent)}%`,
            `${num(row.grossMarginPercent)}%`,
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function InventoryTrendsView({
  t,
  data,
  money,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  money: Function;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  const s = data.summary ?? {};
  return (
    <>
      <Cards
        cards={[
          { label: t.balance, value: money(s.totalValuation) },
          { label: t.consumption, value: num(s.consumption) },
          { label: t.wasteCost, value: money(s.wasteCost) },
          { label: t.waste, value: num(s.wasteQuantity) },
        ]}
      />
      <Panel title={t.items}>
        <ReportTable
          language={language}
          head={[t.product, t.balance, t.endingBalance]}
          rows={(data.byItem ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.net),
            num(row.endingBalance),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.typeLabel}>
        <ReportTable
          language={language}
          head={[t.typeLabel, t.count, t.quantity]}
          rows={(data.byType ?? []).map((row: any) => [
            row.type,
            num(row.count),
            num(row.quantity),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function KitchenPerformanceView({
  t,
  data,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  const s = data.summary ?? {};
  return (
    <>
      <Cards
        cards={[
          { label: t.created, value: num(s.created) },
          { label: t.completed, value: num(s.completed) },
          {
            label: t.avgPrep,
            value:
              s.avgPrepMinutes == null
                ? "—"
                : `${num(s.avgPrepMinutes)} ${t.items}`,
          },
          { label: t.onTime, value: `${num(s.onTimePercent)}%` },
          { label: t.lateOrders, value: num(s.overdue) },
        ]}
      />
      <Panel title={t.station}>
        <ReportTable
          language={language}
          head={[t.station, t.created, t.completed, t.avgPrep, t.onTime]}
          rows={(data.byStation ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.created),
            num(row.completed),
            row.avgPrepMinutes == null || row.avgPrepMinutes === 0
              ? "—"
              : `${num(row.avgPrepMinutes)} ${t.items}`,
            num(row.onTime),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.day}>
        <ReportTable
          language={language}
          head={[t.day, t.created, t.completed, t.lateOrders]}
          rows={(data.byDay ?? []).map((row: any) => [
            row.day,
            num(row.created),
            num(row.completed),
            num(row.overdue),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function CancellationAnalyticsView({
  t,
  data,
  money,
  num,
  language,
  empty,
}: {
  t: any;
  data: any;
  money: Function;
  num: Function;
  language: Language;
  empty: string;
}) {
  const name = (a: string | null | undefined, e: string | null | undefined) =>
    language === "ar" ? (a ?? e ?? "—") : (e ?? a ?? "—");
  const s = data.summary ?? {};
  return (
    <>
      <Cards
        cards={[
          { label: t.cancelled, value: num(s.count) },
          { label: t.amount, value: money(s.amount) },
          { label: t.cancelRate, value: `${num((s.rate ?? 0) * 100)}%` },
          { label: t.avgPrep, value: num(s.beforeKitchen) },
          { label: t.lateOrders, value: num(s.afterKitchen) },
          { label: t.refunds, value: money(s.refundAmount) },
        ]}
      />
      <Panel title={t.day}>
        <ReportTable
          language={language}
          head={[t.day, t.count, t.amount]}
          rows={(data.byDay ?? []).map((row: any) => [
            row.day,
            num(row.count),
            money(row.amount),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.reason}>
        <ReportTable
          language={language}
          head={[t.reason, t.count, t.amount]}
          rows={(data.byReason ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.count),
            money(row.amount),
          ])}
          empty={empty}
        />
      </Panel>
      <Panel title={t.product}>
        <ReportTable
          language={language}
          head={[t.product, t.quantity, t.amount]}
          rows={(data.byProduct ?? []).map((row: any) => [
            name(row.nameAr, row.nameEn),
            num(row.quantity),
            money(row.amount),
          ])}
          empty={empty}
        />
      </Panel>
    </>
  );
}

function AlertsView({
  t,
  data,
  language,
}: {
  t: any;
  data: any;
  language: Language;
}) {
  const levelLabel = (level: string) =>
    level === "critical"
      ? t.critical
      : level === "warning"
        ? t.warning
        : t.info;
  const levelColor = (level: string) =>
    level === "critical"
      ? "border-destructive/40 bg-destructive/10 text-destructive"
      : level === "warning"
        ? "border-warning/40 bg-warning/10 text-warning"
        : "border-border bg-muted text-success";
  const message = (alert: any) => {
    const p = alert.params ?? {};
    switch (alert.code) {
      case "low-stock":
        return `${t.alertLowStock}${p.itemName} (${p.sku}): #${p.balance}`;
      case "negative-stock":
        return `${t.alertNegativeStock}${p.itemName} (${p.sku}): #${p.balance}`;
      case "kitchen-overdue":
        return `${t.alertKitchenOverdue}${p.count}`;
      case "cash-variance":
        return `${t.alertCashVariance}${p.variance}`;
      case "cancellation-rate":
        return `${t.alertCancellationRate}${num((p.rate ?? 0) * 100)}%`;
      case "high-waste":
        return `${t.alertHighWaste}${num(p.rate)}%`;
      case "food-cost-high":
        return `${t.alertFoodCostHigh}${p.productName} (${num(p.foodCostPercent)}%)`;
      case "pending-qr-approval":
        return `${t.alertPendingQr}${p.count}`;
      default:
        return alert.code;
    }
  };
  const num = (v: number | null | undefined) =>
    v == null
      ? "—"
      : new Intl.NumberFormat(language, { maximumFractionDigits: 2 }).format(v);
  if (!data.alerts?.length)
    return <p className="mt-4 text-sm text-muted-foreground">{t.empty}</p>;
  return (
    <div className="mt-3 grid gap-3">
      {data.alerts.map((alert: any, i: number) => (
        <div
          key={i}
          className={`flex items-center justify-between rounded-xl border p-4 ${levelColor(alert.level)}`}
        >
          <span>{message(alert)}</span>
          <span className="rounded-full bg-card/70 px-3 py-1 text-xs font-semibold">
            {levelLabel(alert.level)}
          </span>
        </div>
      ))}
    </div>
  );
}

function ErrorState({
  label,
  onRetry,
  retry,
}: {
  label: string;
  onRetry: () => void;
  retry: string;
}) {
  return (
    <div
      role="alert"
      className="mt-8 rounded-xl border border-destructive/40 bg-destructive/10 p-5 text-destructive"
    >
      <p>{label}</p>
      <Button onClick={onRetry} className="mt-3 font-semibold underline">
        {retry}
      </Button>
    </div>
  );
}
