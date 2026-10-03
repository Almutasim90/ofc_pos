import { useEffect, useState } from "react";
import { FormDialog } from "@/app/FormDialog";
import { SearchableSelect } from "@/app/SearchableSelect";
import { store } from "@/lib/local-store";
import { paymentMethodName } from "@/lib/payment-method";
import { printOrderReceipt } from "@/lib/receipt";
import { Button } from "@/components/ui/button";
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
type Branch = { id: string; nameAr: string; nameEn: string };
type Channel = { id: string; code: string; nameAr: string; nameEn: string };
type OrderRow = {
  id: string;
  number?: number | null;
  status: string;
  source: string;
  salesChannelId: string;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
  note: string | null;
  createdAt: string;
  lineCount: number;
};
type HistoryResponse = {
  total: number;
  page: number;
  pageSize: number;
  items: OrderRow[];
};
type OrderDetail = {
  id: string;
  salesChannelId: string;
  source: string;
  status: string;
  note: string | null;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
  createdAt: string;
  lines: Array<{
    id: string;
    productNameAr: string;
    productNameEn: string;
    quantity: number;
    note: string | null;
    unitGrossAmount: number;
  }>;
};
type PaymentRow = {
  id: string;
  amount: number;
  status: string;
  code: string;
  nameAr: string;
  nameEn: string;
};

const input =
  "min-h-11 min-w-0 rounded-lg border border-border bg-card px-3 text-sm";
const button =
  "inline-flex min-h-10 items-center justify-center gap-1.5 rounded-lg border border-border px-3 text-sm font-semibold";
const PAGE_SIZE = 10;

function todayStr() {
  return new Date().toISOString().slice(0, 10);
}

const copy = {
  ar: {
    title: "سجل الطلبات",
    intro: "استعرض جميع طلبات أي فرع خلال أي فترة زمنية.",
    branch: "الفرع",
    from: "من",
    to: "إلى",
    status: "الحالة",
    allStatuses: "كل الحالات",
    channel: "قناة البيع",
    allChannels: "كل القنوات",
    loading: "جارٍ التحميل…",
    error: "تعذر تحميل الطلبات.",
    retry: "إعادة المحاولة",
    empty: "لا توجد طلبات لهذه الفترة.",
    time: "الوقت",
    orderNo: "رقم الطلب",
    items: "الأصناف",
    total: "الإجمالي",
    view: "عرض",
    close: "إغلاق",
    prev: "السابق",
    next: "التالي",
    net: "الصافي",
    tax: "الضريبة",
    gross: "الإجمالي",
    note: "ملاحظة",
    payments: "المدفوعات",
    noPayments: "لا توجد مدفوعات مسجلة.",
    printReceipt: "طباعة الفاتورة",
    printFailed: "تعذر طباعة الفاتورة.",
    statuses: {
      Draft: "معلّق",
      Pending: "بانتظار الدفع",
      Confirmed: "مؤكد",
      Paid: "مدفوع",
      SentToKitchen: "أُرسل للمطبخ",
      Preparing: "قيد التحضير",
      Ready: "جاهز",
      Completed: "مكتمل",
      Cancelled: "ملغى",
      Rejected: "مرفوض",
      PartiallyRefunded: "استرجاع جزئي",
      Refunded: "مسترجع",
    } as Record<string, string>,
    sources: {
      Pos: "نقطة بيع",
      Qr: "QR",
      Delivery: "توصيل",
      Aggregator: "منصة توصيل",
    } as Record<string, string>,
  },
  en: {
    title: "Order history",
    intro: "Browse every order for any branch and date range.",
    branch: "Branch",
    from: "From",
    to: "To",
    status: "Status",
    allStatuses: "All statuses",
    channel: "Sales channel",
    allChannels: "All channels",
    loading: "Loading…",
    error: "Unable to load orders.",
    retry: "Retry",
    empty: "No orders for this period.",
    time: "Time",
    orderNo: "Order no.",
    items: "Items",
    total: "Total",
    view: "View",
    close: "Close",
    prev: "Previous",
    next: "Next",
    net: "Net",
    tax: "Tax",
    gross: "Total",
    note: "Note",
    payments: "Payments",
    noPayments: "No payments recorded.",
    printReceipt: "Print receipt",
    printFailed: "Could not print the receipt.",
    statuses: {
      Draft: "Held",
      Pending: "Awaiting payment",
      Confirmed: "Confirmed",
      Paid: "Paid",
      SentToKitchen: "Sent to kitchen",
      Preparing: "Preparing",
      Ready: "Ready",
      Completed: "Completed",
      Cancelled: "Cancelled",
      Rejected: "Rejected",
      PartiallyRefunded: "Partially refunded",
      Refunded: "Refunded",
    } as Record<string, string>,
    sources: {
      Pos: "POS",
      Qr: "QR",
      Delivery: "Delivery",
      Aggregator: "Aggregator",
    } as Record<string, string>,
  },
} as const;

export function OrderHistorySection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: {
    nameAr: string;
    nameEn: string;
    code?: string;
    kind?: string;
  }) => paymentMethodName(language, x);
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
  const [printFailed, setPrintFailed] = useState(false);
  const [selected, setSelected] = useState<{
    order: OrderDetail;
    payments: PaymentRow[];
  } | null>(null);
  const auth = (path: string, init?: RequestInit) =>
    fetch(path, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`,
        ...(init?.headers ?? {}),
      },
    });

  useEffect(() => {
    void (async () => {
      const r = await auth("/api/v1/pos/context");
      if (!r.ok) return;
      const value = (await r.json()) as {
        branches: Branch[];
        channels: Channel[];
      };
      setBranches(value.branches);
      setChannels(value.channels);
      setBranchId(value.branches[0]?.id ?? "");
    })();
  }, []);
  useEffect(() => {
    setPage(1);
  }, [branchId, from, to, status, channelId]);

  async function load() {
    if (!branchId) return;
    setLoading(true);
    setError(false);
    try {
      const params = new URLSearchParams({
        branchId,
        from: new Date(`${from}T00:00:00`).toISOString(),
        to: new Date(`${to}T23:59:59.999`).toISOString(),
        page: String(page),
        pageSize: String(PAGE_SIZE),
      });
      if (status) params.set("status", status);
      if (channelId) params.set("salesChannelId", channelId);
      const response = await auth(
        `/api/v1/orders/history?${params.toString()}`,
      );
      if (!response.ok) throw new Error();
      setResult((await response.json()) as HistoryResponse);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    void load();
  }, [branchId, from, to, status, channelId, page]);

  async function open(id: string) {
    setPrintFailed(false);
    const [orderResponse, paymentResponse] = await Promise.all([
      auth(`/api/v1/orders/${id}`),
      auth(`/api/v1/orders/${id}/payments`),
    ]);
    if (!orderResponse.ok) return;
    const order = (await orderResponse.json()) as OrderDetail;
    setSelected({
      order,
      payments: paymentResponse.ok
        ? ((await paymentResponse.json()) as PaymentRow[])
        : [],
    });
  }

  const rows = result?.items ?? [];
  const total = result?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const rangeFrom = total === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const rangeTo = Math.min(page * PAGE_SIZE, total);

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-2xl font-bold">{t.title}</h1>
        <p className="mt-2 text-sm text-muted-foreground">{t.intro}</p>
      </div>
      <div className="flex flex-wrap items-end gap-3 rounded-xl border border-border bg-card p-4">
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
        <label className="text-xs font-medium text-muted-foreground">
          {t.from}
          <Input
            type="date"
            value={from}
            max={to}
            onChange={(e) => setFrom(e.target.value)}
            className={`mt-1 block ${input}`}
          />
        </label>
        <label className="text-xs font-medium text-muted-foreground">
          {t.to}
          <Input
            type="date"
            value={to}
            min={from}
            max={todayStr()}
            onChange={(e) => setTo(e.target.value)}
            className={`mt-1 block ${input}`}
          />
        </label>
        <SearchableSelect label={t.status} value={status} onChange={setStatus}>
          <option value="">{t.allStatuses}</option>
          {Object.keys(t.statuses).map((s) => (
            <option key={s} value={s}>
              {t.statuses[s]}
            </option>
          ))}
        </SearchableSelect>
        <SearchableSelect
          label={t.channel}
          value={channelId}
          onChange={setChannelId}
        >
          <option value="">{t.allChannels}</option>
          {channels.map((c) => (
            <option key={c.id} value={c.id}>
              {name(c)}
            </option>
          ))}
        </SearchableSelect>
      </div>
      {loading ? (
        <p
          role="status"
          className="rounded-xl border bg-card p-8 text-center text-sm"
        >
          {t.loading}
        </p>
      ) : error ? (
        <div
          role="alert"
          className="rounded-xl border border-destructive/40 bg-destructive/10 p-5 text-sm text-destructive"
        >
          <p>{t.error}</p>
          <Button
            onClick={() => void load()}
            className="mt-3 font-semibold underline"
          >
            {t.retry}
          </Button>
        </div>
      ) : rows.length === 0 ? (
        <p className="rounded-xl border bg-card p-8 text-center text-sm text-muted-foreground">
          {t.empty}
        </p>
      ) : (
        <>
          <div className="overflow-x-auto rounded-xl border border-border bg-card">
            <Table className="w-full text-sm">
              <TableHeader>
                <TableRow className="border-b border-border text-xs text-muted-foreground">
                  <TableHead className="px-4 py-3 text-start font-semibold">
                    {t.orderNo}
                  </TableHead>
                  <TableHead className="px-4 py-3 text-start font-semibold">
                    {t.time}
                  </TableHead>
                  <TableHead className="px-4 py-3 text-start font-semibold">
                    {t.channel}
                  </TableHead>
                  <TableHead className="px-4 py-3 text-start font-semibold">
                    {t.status}
                  </TableHead>
                  <TableHead className="px-4 py-3 text-start font-semibold">
                    {t.items}
                  </TableHead>
                  <TableHead className="px-4 py-3 text-start font-semibold">
                    {t.total}
                  </TableHead>
                  <TableHead className="px-4 py-3 text-end font-semibold"></TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {rows.map((o) => {
                  const ch = channels.find((c) => c.id === o.salesChannelId);
                  return (
                    <TableRow
                      key={o.id}
                      className="border-b border-border last:border-0 hover:bg-background"
                    >
                      <TableCell className="px-4 py-3 font-semibold tabular-nums">
                        {o.number ? `#${o.number}` : "—"}
                      </TableCell>
                      <TableCell className="px-4 py-3">
                        {new Date(o.createdAt).toLocaleString(language)}
                      </TableCell>
                      <TableCell className="px-4 py-3 text-muted-foreground">
                        {ch ? name(ch) : (t.sources[o.source] ?? o.source)}
                      </TableCell>
                      <TableCell className="px-4 py-3">
                        <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-semibold">
                          {t.statuses[o.status] ?? o.status}
                        </span>
                      </TableCell>
                      <TableCell className="px-4 py-3 text-muted-foreground">
                        {o.lineCount}
                      </TableCell>
                      <TableCell className="px-4 py-3 font-semibold">
                        OMR {o.grossAmount.toFixed(3)}
                      </TableCell>
                      <TableCell className="px-4 py-3 text-end">
                        <Button
                          onClick={() => void open(o.id)}
                          className={button}
                        >
                          {t.view}
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </div>
          <div className="flex items-center justify-between gap-3 text-sm text-muted-foreground">
            <span>
              {rangeFrom}–{rangeTo} / {total}
            </span>
            <div className="flex gap-2">
              <Button
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
                className={`${button} disabled:opacity-50`}
              >
                {t.prev}
              </Button>
              <Button
                disabled={page >= pageCount}
                onClick={() => setPage((p) => p + 1)}
                className={`${button} disabled:opacity-50`}
              >
                {t.next}
              </Button>
            </div>
          </div>
        </>
      )}
      {selected && (
        <FormDialog
          title={t.view}
          closeLabel={t.close}
          onClose={() => setSelected(null)}
        >
          <div className="space-y-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="rounded-full bg-muted px-2.5 py-0.5 text-xs font-semibold">
                {t.statuses[selected.order.status] ?? selected.order.status}
              </span>
              <span className="text-sm text-muted-foreground">
                {new Date(selected.order.createdAt).toLocaleString(language)}
              </span>
            </div>
            <ul className="space-y-2">
              {selected.order.lines.map((l) => (
                <li
                  key={l.id}
                  className="flex items-center justify-between gap-3 rounded-lg bg-muted px-3 py-2 text-sm"
                >
                  <span>
                    {l.quantity} ×{" "}
                    {language === "ar" ? l.productNameAr : l.productNameEn}
                  </span>
                  <span>OMR {(l.unitGrossAmount * l.quantity).toFixed(3)}</span>
                </li>
              ))}
            </ul>
            {selected.order.note && (
              <p className="text-sm text-muted-foreground">
                {t.note}: {selected.order.note}
              </p>
            )}
            <div className="grid grid-cols-3 gap-2 rounded-lg bg-muted p-3 text-sm">
              <div>
                <p className="text-xs text-muted-foreground">{t.net}</p>
                <p className="font-semibold">
                  OMR {selected.order.netAmount.toFixed(3)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">{t.tax}</p>
                <p className="font-semibold">
                  OMR {selected.order.taxAmount.toFixed(3)}
                </p>
              </div>
              <div>
                <p className="text-xs text-muted-foreground">{t.gross}</p>
                <p className="font-semibold">
                  OMR {selected.order.grossAmount.toFixed(3)}
                </p>
              </div>
            </div>
            <div>
              <h3 className="text-sm font-semibold">{t.payments}</h3>
              {selected.payments.length === 0 ? (
                <p className="mt-1 text-sm text-muted-foreground">
                  {t.noPayments}
                </p>
              ) : (
                <ul className="mt-2 space-y-1 text-sm">
                  {selected.payments.map((p) => (
                    <li key={p.id} className="flex justify-between">
                      <span>{name(p)}</span>
                      <span>OMR {p.amount.toFixed(3)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            {selected.payments.length > 0 && (
              <div className="flex flex-wrap items-center gap-3">
                <Button
                  type="button"
                  onClick={async () => {
                    setPrintFailed(false);
                    const printed = await printOrderReceipt(
                      auth,
                      branchId,
                      selected.order.id,
                      language,
                    );
                    setPrintFailed(!printed);
                  }}
                  className="min-h-10 rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground"
                >
                  {t.printReceipt}
                </Button>
                {printFailed && (
                  <p role="alert" className="text-sm text-destructive">
                    {t.printFailed}
                  </p>
                )}
              </div>
            )}
          </div>
        </FormDialog>
      )}
    </div>
  );
}
