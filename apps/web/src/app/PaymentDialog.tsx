import { useEffect, useState } from "react";
import { Banknote, CreditCard, X } from "lucide-react";
import { createId, store } from "@/lib/local-store";

type Language = "ar" | "en";
type Method = { id: string; nameAr: string; nameEn: string; kind: string };
type Mode = "Cash" | "Card" | "Split";
type Tender = { key: string; role: "Cash" | "Card"; methodId: string; amount: string; tendered: string; reference: string };
const text = { ar: { title: "الدفع", due: "المستحق", method: "طريقة الدفع", cash: "نقدي", card: "بطاقة", split: "نقدي + بطاقة", splitHint: "قسّم المبلغ: أدخل مبلغ أحد الخيارين ويُحسب الآخر تلقائيًا", amount: "المبلغ", tendered: "المستلم", reference: "مرجع العملية", change: "الباقي", pay: "إتمام الدفع", close: "إغلاق", none: "لا توجد وسائل دفع مفعلة لهذا الفرع", exceed: "المبلغ المدخل أكبر من المستحق", invalid: "المجموع لا يساوي المبلغ المستحق", cashShort: "المبلغ النقدي المستلم أقل من قيمة النقد", failed: "تعذر تسجيل الدفع", paid: "تم الدفع بنجاح" }, en: { title: "Payment", due: "Amount due", method: "Payment method", cash: "Cash", card: "Card", split: "Cash + Card", splitHint: "Split payment: enter one amount and the other is calculated automatically", amount: "Amount", tendered: "Received", reference: "Terminal reference", change: "Change", pay: "Complete payment", close: "Close", none: "No active payment methods are configured for this branch", exceed: "Amount entered exceeds the amount due", invalid: "Tender amounts must equal the amount due", cashShort: "Cash received is less than the cash amount", failed: "Unable to post payment", paid: "Payment completed" } } as const;

export function PaymentDialog({ language, orderId, branchId, total, onClose }: { language: Language; orderId: string; branchId: string; total: number; onClose: () => void }) {
  const t = text[language];
  const [methods, setMethods] = useState<Method[]>([]);
  const [mode, setMode] = useState<Mode>("Cash");
  const [tenders, setTenders] = useState<Tender[]>([]);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);
  const [saving, setSaving] = useState(false);
  const [paid, setPaid] = useState(false);
  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });
  const money = (value: number) => Math.round((value + Number.EPSILON) * 1000) / 1000;
  const f = (value: number) => money(value).toFixed(3);
  const number = (value: string) => Number(value) || 0;

  const cashMethods = methods.filter((m) => m.kind === "Cash");
  const cardMethods = methods.filter((m) => m.kind !== "Cash");
  const methodsFor = (role: "Cash" | "Card") => role === "Cash" ? cashMethods : cardMethods;
  const canMode = (m: Mode) => m === "Cash" ? cashMethods.length > 0 : m === "Card" ? cardMethods.length > 0 : cashMethods.length > 0 && cardMethods.length > 0;

  function buildTenders(list: Method[], chosen: Mode): Tender[] {
    const pick = (role: "Cash" | "Card") => {
      const candidates = role === "Cash" ? list.filter((m) => m.kind === "Cash") : list.filter((m) => m.kind !== "Cash");
      return candidates[0]?.id ?? list[0]?.id ?? "";
    };
    if (chosen === "Cash") return [{ key: createId(), role: "Cash", methodId: pick("Cash"), amount: f(total), tendered: f(total), reference: "" }];
    if (chosen === "Card") return [{ key: createId(), role: "Card", methodId: pick("Card"), amount: f(total), tendered: f(total), reference: "" }];
    const cashAmount = money(total / 2);
    const cardAmount = money(total - cashAmount);
    return [
      { key: createId(), role: "Cash", methodId: pick("Cash"), amount: f(cashAmount), tendered: f(cashAmount), reference: "" },
      { key: createId(), role: "Card", methodId: pick("Card"), amount: f(cardAmount), tendered: f(cardAmount), reference: "" },
    ];
  }

  function chooseMode(next: Mode) {
    if (!canMode(next) || next === mode) return;
    setMode(next);
    setMessage("");
    setIsError(false);
    setTenders(buildTenders(methods, next));
  }

  useEffect(() => {
    void (async () => {
      const response = await auth(`/api/v1/payment-methods?branchId=${branchId}`);
      if (!response.ok) return;
      const value = await response.json() as Method[];
      setMethods(value);
      if (value.length === 0) return;
      const initial = value.some((m) => m.kind === "Cash") ? "Cash" as Mode : "Card" as Mode;
      setMode(initial);
      setTenders(buildTenders(value, initial));
    })();
  }, [branchId, total]);

  const cashRows = tenders.filter((x) => x.role === "Cash");
  const applied = tenders.reduce((sum, x) => sum + money(number(x.amount)), 0);
  const cashTendered = cashRows.reduce((sum, x) => sum + money(number(x.tendered)), 0);
  const cashAmount = cashRows.reduce((sum, x) => sum + money(number(x.amount)), 0);
  const change = Math.max(0, money(cashTendered - cashAmount));

  function patch(key: string, patch: Partial<Tender>) { setTenders(tenders.map((x) => x.key === key ? { ...x, ...patch } : x)); }

  // Split payment amounts: whichever amount the user edits last wins, the other is recalculated to the
  // remaining due, and an entry above the amount due is rejected with a message.
  function editSplitAmount(key: string, raw: string) {
    setMessage("");
    setIsError(false);
    const value = raw.trim() === "" ? 0 : Number(raw);
    if (!Number.isFinite(value) || value < 0) return;
    if (value > total) { setMessage(t.exceed); setIsError(true); return; }
    setTenders(tenders.map((x) => {
      if (x.key === key) {
        if (x.role === "Card") return { ...x, amount: raw, tendered: raw };
        return { ...x, amount: raw, tendered: money(number(x.tendered)) === money(number(x.amount)) ? raw : x.tendered };
      }
      const remaining = f(total - value);
      if (x.role === "Card") return { ...x, amount: remaining, tendered: remaining };
      return { ...x, amount: remaining, tendered: money(number(x.tendered)) === money(number(x.amount)) ? remaining : x.tendered };
    }));
  }

  function normalizeAmount(key: string) {
    setTenders(tenders.map((x) => x.key === key ? { ...x, amount: f(number(x.amount)), tendered: x.role === "Cash" && money(number(x.tendered)) === money(number(x.amount)) ? f(number(x.amount)) : x.tendered } : x));
  }

  async function pay() {
    setMessage("");
    setIsError(false);
    if (Math.abs(applied - total) > 0.0001 || tenders.some((x) => money(number(x.amount)) <= 0)) { setMessage(t.invalid); setIsError(true); return; }
    if (cashRows.some((x) => money(number(x.tendered)) < money(number(x.amount)))) { setMessage(t.cashShort); setIsError(true); return; }
    setSaving(true);
    try {
      const response = await auth(`/api/v1/orders/${orderId}/payments`, { method: "POST", body: JSON.stringify({ payments: tenders.map((x) => ({ clientRequestId: createId(), paymentMethodId: x.methodId, amount: money(number(x.amount)), tenderedAmount: x.role === "Cash" ? money(number(x.tendered)) : money(number(x.amount)), status: "Captured", providerReference: x.reference.trim() || null })) }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.payments?.[0] ?? t.failed); }
      setMessage(t.paid);
      setPaid(true);
    } catch (error) { setMessage(error instanceof Error ? error.message : t.failed); setIsError(true); } finally { setSaving(false); }
  }

  const rowInputs = (x: Tender, index: number) => (
    <div key={x.key} className="rounded-xl border border-[#dfe5df] bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <strong className="flex items-center gap-2">{x.role === "Cash" ? <><Banknote size={18} className="text-[#0e5a4f]" />{t.cash}</> : <><CreditCard size={18} className="text-[#0e5a4f]" />{t.card}</>}</strong>
        {mode === "Split" && <span className="text-xs text-[#69766f]">{t.method} {index + 1}</span>}
      </div>
      <div className="mt-3 grid gap-3 sm:grid-cols-2">
        <label className="text-sm">{t.method}<select value={x.methodId} onChange={(e) => patch(x.key, { methodId: e.target.value })} className="mt-1 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{methodsFor(x.role).map((m) => <option key={m.id} value={m.id}>{language === "ar" ? m.nameAr : m.nameEn}</option>)}</select></label>
        <label className="text-sm">{t.amount}<input inputMode="decimal" value={x.amount} disabled={mode !== "Split"} onChange={(e) => mode === "Split" && editSplitAmount(x.key, e.target.value)} onBlur={() => mode === "Split" && normalizeAmount(x.key)} className="mt-1 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 disabled:bg-[#eef1ee] disabled:text-[#53615b]" /></label>
        {x.role === "Cash" ? <label className="text-sm">{t.tendered}<input inputMode="decimal" value={x.tendered} onChange={(e) => patch(x.key, { tendered: e.target.value })} className="mt-1 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3" /></label> : <label className="text-sm">{t.reference}<input value={x.reference} onChange={(e) => patch(x.key, { reference: e.target.value })} placeholder="—" className="mt-1 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3" /></label>}
      </div>
    </div>
  );

  const modeButton = (m: Mode, label: string, Icon: typeof Banknote) => (
    <button key={m} type="button" onClick={() => chooseMode(m)} disabled={!canMode(m)} className={`inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border px-3 text-sm font-semibold disabled:opacity-40 ${mode === m ? "border-[#0e5a4f] bg-[#0e5a4f] text-white" : "border-[#cdd7d0] bg-white text-[#53615b] hover:bg-[#f2f5f2]"}`}><Icon size={17} />{label}</button>
  );

  return (
    <div className="fixed inset-0 z-50 grid place-items-end bg-black/35 sm:place-items-center sm:p-5">
      <section role="dialog" aria-modal="true" aria-label={t.title} className="max-h-[92vh] w-full max-w-xl overflow-y-auto rounded-t-2xl bg-[#f5f6f2] p-5 shadow-2xl sm:rounded-2xl sm:p-6">
        <div className="flex items-center justify-between"><div><p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p><h2 className="text-2xl font-bold">OMR {total.toFixed(3)}</h2></div><button aria-label={t.close} onClick={onClose} className="grid size-11 place-items-center rounded-lg bg-white"><X size={19} /></button></div>
        {methods.length === 0 ? <p role="alert" className="mt-6 rounded-xl bg-[#fff5f4] p-4 text-sm text-[#9b2922]">{t.none}</p> : <>
          <p className="mt-5 text-sm font-medium">{t.method}</p>
          <div className="mt-2 grid grid-cols-3 gap-2">{modeButton("Cash", t.cash, Banknote)}{modeButton("Card", t.card, CreditCard)}{modeButton("Split", t.split, Banknote)}</div>
          {mode === "Split" && <p className="mt-3 rounded-lg bg-[#e6f1ec] px-3 py-2 text-xs text-[#08483f]">{t.splitHint}</p>}
          <div className="mt-4 space-y-3">{tenders.map((x, index) => rowInputs(x, index))}</div>
          <div className="mt-5 flex justify-between rounded-xl bg-white p-4"><span>{t.change}</span><strong>OMR {change.toFixed(3)}</strong></div>
          {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}
          <button disabled={saving || paid} onClick={() => void pay()} className="mt-5 flex min-h-13 w-full items-center justify-center gap-2 rounded-xl bg-[#0e5a4f] font-semibold text-white disabled:opacity-60"><CreditCard size={18} />{saving ? "..." : t.pay}</button>
        </>}
      </section>
    </div>
  );
}
