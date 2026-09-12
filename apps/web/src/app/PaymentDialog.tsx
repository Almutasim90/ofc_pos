import { useEffect, useRef, useState } from "react";
import { Banknote, CreditCard, X } from "lucide-react";
import { createId, store } from "@/lib/local-store";
import { normalizeMoneyInput, roundMoney, type PaymentMethod } from "@/lib/payment";

type Language = "ar" | "en";
type Method = { id: string; nameAr: string; nameEn: string; kind: string };
const text = { ar: { title: "الدفع", due: "المستحق", method: "طريقة الدفع", cash: "نقدي", card: "بطاقة", mixed: "نقدي + بطاقة", pay: "إتمام الدفع", close: "إغلاق", none: "لا توجد وسائل دفع مفعلة لهذا الفرع", exceed: "المبلغ المدخل أكبر من المستحق", invalid: "أدخل مبلغًا صحيحًا (يجب تقسيم الدفع بين نقدي وبطاقة)", noCash: "لا توجد طريقة دفع نقدية", noCard: "لا توجد طريقة دفع بالبطاقة", failed: "تعذر تسجيل الدفع", paid: "تم الدفع بنجاح" }, en: { title: "Payment", due: "Amount due", method: "Payment method", cash: "Cash", card: "Card", mixed: "Cash + Card", pay: "Complete payment", close: "Close", none: "No active payment methods are configured for this branch", exceed: "Amount entered exceeds the amount due", invalid: "Enter valid amounts (split must include both cash and card)", noCash: "No cash payment method is configured", noCard: "No card payment method is configured", failed: "Unable to post payment", paid: "Payment completed" } } as const;

export function PaymentDialog({ language, orderId, branchId, total, onClose }: { language: Language; orderId: string; branchId: string; total: number; onClose: () => void }) {
  const t = text[language];
  const [methods, setMethods] = useState<Method[]>([]);
  const [methodsLoaded, setMethodsLoaded] = useState(false);
  const [method, setMethod] = useState<PaymentMethod>("Cash");
  const [cash, setCash] = useState("");
  const [card, setCard] = useState("");
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);
  const [saving, setSaving] = useState(false);
  const [paid, setPaid] = useState(false);
  const closeTimer = useRef<number | null>(null);
  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });
  const fmt3 = (value: number) => roundMoney(value).toFixed(3);

  useEffect(() => {
    void (async () => {
      try {
        const response = await auth(`/api/v1/payment-methods?branchId=${branchId}`);
        if (!response.ok) return;
        const value = await response.json() as Method[];
        setMethods(value);
        setMethod(value.some((m) => m.kind === "Cash") ? "Cash" : "Card");
      } finally { setMethodsLoaded(true); }
    })();
  }, [branchId, total]);
  useEffect(() => () => { if (closeTimer.current !== null) window.clearTimeout(closeTimer.current); }, []);

  const cashMethod = methods.find((m) => m.kind === "Cash");
  const cardMethod = methods.find((m) => m.kind !== "Cash");

  // Cash stays the canonical split value (either field updates it); the edited field keeps the user's
  // raw text so fractions/decimal points can be typed freely, and the other field shows the remainder.
  const cashAmount = method === "Cash" ? total : method === "Card" ? 0 : roundMoney(cash === "" ? 0 : Number(cash));
  const cardAmount = method === "Card" ? total : method === "Cash" ? 0 : roundMoney(total - cashAmount);
  const valid = Number.isFinite(cashAmount) && cashAmount >= 0 && cardAmount >= 0 && (method !== "Mixed" || (cashAmount > 0 && cardAmount > 0));

  function chooseMethod(next: PaymentMethod) {
    setMethod(next);
    setCash("");
    setCard("");
    setMessage("");
    setIsError(false);
  }

  function setCashAmount(raw: string) {
    setMessage("");
    setIsError(false);
    const normalized = normalizeMoneyInput(raw);
    if (normalized === null) return;
    setCash(normalized);
    const value = normalized === "" ? 0 : Number(normalized);
    if (value > total) { setCard(""); setMessage(t.exceed); setIsError(true); return; }
    setCard(normalized === "" ? fmt3(total) : fmt3(total - value));
  }

  function setCardAmount(raw: string) {
    setMessage("");
    setIsError(false);
    const normalized = normalizeMoneyInput(raw);
    if (normalized === null) return;
    setCard(normalized);
    const value = normalized === "" ? 0 : Number(normalized);
    if (value > total) { setCash(""); setMessage(t.exceed); setIsError(true); return; }
    setCash(normalized === "" ? fmt3(total) : fmt3(total - value));
  }

  async function pay() {
    setMessage("");
    setIsError(false);
    if (!valid) { setMessage(t.invalid); setIsError(true); return; }
    const payments: Array<{ clientRequestId: string; paymentMethodId: string; amount: number; tenderedAmount: number; status: string; providerReference: string | null }> = [];
    if (cashAmount > 0) { if (!cashMethod) { setMessage(t.noCash); setIsError(true); return; } payments.push({ clientRequestId: createId(), paymentMethodId: cashMethod.id, amount: cashAmount, tenderedAmount: cashAmount, status: "Captured", providerReference: null }); }
    if (cardAmount > 0) { if (!cardMethod) { setMessage(t.noCard); setIsError(true); return; } payments.push({ clientRequestId: createId(), paymentMethodId: cardMethod.id, amount: cardAmount, tenderedAmount: cardAmount, status: "Captured", providerReference: null }); }
    setSaving(true);
    try {
      const response = await auth(`/api/v1/orders/${orderId}/payments`, { method: "POST", body: JSON.stringify({ payments }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.payments?.[0] ?? t.failed); }
      setMessage(t.paid);
      setPaid(true);
      closeTimer.current = window.setTimeout(onClose, 3000);
    } catch (error) { setMessage(error instanceof Error ? error.message : t.failed); setIsError(true); } finally { setSaving(false); }
  }

  const modeButton = (m: PaymentMethod, label: string, Icon: typeof Banknote | null, enabled: boolean) => (
    <button key={m} type="button" onClick={() => chooseMethod(m)} disabled={!enabled} className={`inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border px-3 text-sm font-semibold disabled:opacity-40 ${method === m ? "border-[#0e5a4f] bg-[#0e5a4f] text-white" : "border-[#cdd7d0] bg-white text-[#53615b] hover:bg-[#f2f5f2]"}`}>{Icon && <Icon size={17} />}{label}</button>
  );

  const splitField = (label: string, value: string, onChange: (v: string) => void, Icon: typeof Banknote) => (
    <label className="block text-sm font-medium">
      <span className="flex items-center gap-2 text-[#53615b]"><Icon size={17} className="text-[#0e5a4f]" />{label}</span>
      <input type="text" inputMode="decimal" value={value} onChange={(e) => onChange(e.target.value)} placeholder="0.000" className="mt-2 min-h-14 w-full rounded-xl border border-[#cdd7d0] bg-white px-4 text-lg font-semibold outline-none focus:border-[#0e5a4f]" />
    </label>
  );

  return (
    <div className="fixed inset-0 z-50 grid place-items-end bg-black/35 sm:place-items-center sm:p-5">
      <section role="dialog" aria-modal="true" aria-label={t.title} className="max-h-[92vh] w-full max-w-md overflow-y-auto rounded-t-2xl bg-[#f5f6f2] p-5 shadow-2xl sm:rounded-2xl sm:p-6">
        <div className="flex items-center justify-between"><div><p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p><h2 className="text-2xl font-bold">OMR {total.toFixed(3)}</h2></div><button aria-label={t.close} onClick={onClose} className="grid size-11 place-items-center rounded-lg bg-white"><X size={19} /></button></div>
        {!methodsLoaded ? null : methods.length === 0 ? <p role="alert" className="mt-6 rounded-xl bg-[#fff5f4] p-4 text-sm text-[#9b2922]">{t.none}</p> : <>
          <p className="mt-5 text-sm font-medium">{t.method}</p>
          <div className="mt-2 grid grid-cols-3 gap-2">{modeButton("Cash", t.cash, Banknote, !!cashMethod)}{modeButton("Card", t.card, CreditCard, !!cardMethod)}{modeButton("Mixed", t.mixed, null, !!cashMethod && !!cardMethod)}</div>
          {method === "Mixed" && <div className="mt-4 grid grid-cols-2 gap-3">{splitField(t.cash, cash, setCashAmount, Banknote)}{splitField(t.card, card, setCardAmount, CreditCard)}</div>}
          {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}
          <button disabled={saving || paid} onClick={() => void pay()} className="mt-5 flex min-h-13 w-full items-center justify-center gap-2 rounded-xl bg-[#0e5a4f] font-semibold text-white disabled:opacity-60"><CreditCard size={18} />{saving ? "..." : t.pay}</button>
        </>}
      </section>
    </div>
  );
}
