import { useEffect, useRef, useState } from "react";
import { Banknote, CreditCard, X } from "lucide-react";
import { createId, store } from "@/lib/local-store";
import { printOrderReceipt } from "@/lib/receipt";
import {
  normalizeMoneyInput,
  roundMoney,
  type PaymentMethod,
} from "@/lib/payment";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Language = "ar" | "en";
type Method = { id: string; nameAr: string; nameEn: string; kind: string };
const text = {
  ar: {
    title: "الدفع",
    due: "المستحق",
    method: "طريقة الدفع",
    cash: "نقدي",
    card: "بطاقة",
    mixed: "نقدي + بطاقة",
    pay: "إتمام الدفع",
    close: "إغلاق",
    none: "لا توجد وسائل دفع مفعلة لهذا الفرع",
    exceed: "المبلغ المدخل أكبر من المستحق",
    invalid: "أدخل مبلغًا صحيحًا (يجب تقسيم الدفع بين نقدي وبطاقة)",
    noCash: "لا توجد طريقة دفع نقدية",
    noCard: "لا توجد طريقة دفع بالبطاقة",
    failed: "تعذر تسجيل الدفع",
    paid: "تم الدفع بنجاح",
  },
  en: {
    title: "Payment",
    due: "Amount due",
    method: "Payment method",
    cash: "Cash",
    card: "Card",
    mixed: "Cash + Card",
    pay: "Complete payment",
    close: "Close",
    none: "No active payment methods are configured for this branch",
    exceed: "Amount entered exceeds the amount due",
    invalid: "Enter valid amounts (split must include both cash and card)",
    noCash: "No cash payment method is configured",
    noCard: "No card payment method is configured",
    failed: "Unable to post payment",
    paid: "Payment completed",
  },
} as const;

export function PaymentDialog({
  language,
  orderId,
  branchId,
  total,
  onClose,
  onPaid,
}: {
  language: Language;
  orderId: string;
  branchId: string;
  total: number;
  onClose: () => void;
  onPaid?: () => Promise<void>;
}) {
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
  const auth = (path: string, init?: RequestInit) =>
    fetch(path, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`,
        ...(init?.headers ?? {}),
      },
    });
  const fmt3 = (value: number) => roundMoney(value).toFixed(3);

  useEffect(() => {
    void (async () => {
      try {
        const response = await auth(
          `/api/v1/payment-methods?branchId=${branchId}`,
        );
        if (!response.ok) return;
        const value = (await response.json()) as Method[];
        setMethods(value);
        setMethod(value.some((m) => m.kind === "Cash") ? "Cash" : "Card");
      } finally {
        setMethodsLoaded(true);
      }
    })();
  }, [branchId, total]);
  useEffect(
    () => () => {
      if (closeTimer.current !== null) window.clearTimeout(closeTimer.current);
    },
    [],
  );

  const cashMethod = methods.find((m) => m.kind === "Cash");
  const cardMethod =
    methods.find((m) => m.kind === "Card") ??
    methods.find((m) => m.kind !== "Cash" && m.kind !== "External");

  // Cash stays the canonical split value (either field updates it); the edited field keeps the user's
  // raw text so fractions/decimal points can be typed freely, and the other field shows the remainder.
  const cashAmount =
    method === "Cash"
      ? total
      : method === "Card"
        ? 0
        : roundMoney(cash === "" ? 0 : Number(cash));
  const cardAmount =
    method === "Card"
      ? total
      : method === "Cash"
        ? 0
        : roundMoney(total - cashAmount);
  const valid =
    Number.isFinite(cashAmount) &&
    cashAmount >= 0 &&
    cardAmount >= 0 &&
    (method !== "Mixed" || (cashAmount > 0 && cardAmount > 0));

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
    if (value > total) {
      setCard("");
      setMessage(t.exceed);
      setIsError(true);
      return;
    }
    setCard(normalized === "" ? fmt3(total) : fmt3(total - value));
  }

  function setCardAmount(raw: string) {
    setMessage("");
    setIsError(false);
    const normalized = normalizeMoneyInput(raw);
    if (normalized === null) return;
    setCard(normalized);
    const value = normalized === "" ? 0 : Number(normalized);
    if (value > total) {
      setCash("");
      setMessage(t.exceed);
      setIsError(true);
      return;
    }
    setCash(normalized === "" ? fmt3(total) : fmt3(total - value));
  }

  async function pay() {
    setMessage("");
    setIsError(false);
    if (!valid) {
      setMessage(t.invalid);
      setIsError(true);
      return;
    }
    const payments: Array<{
      clientRequestId: string;
      paymentMethodId: string;
      amount: number;
      tenderedAmount: number;
      status: string;
      providerReference: string | null;
    }> = [];
    if (cashAmount > 0) {
      if (!cashMethod) {
        setMessage(t.noCash);
        setIsError(true);
        return;
      }
      payments.push({
        clientRequestId: createId(),
        paymentMethodId: cashMethod.id,
        amount: cashAmount,
        tenderedAmount: cashAmount,
        status: "Captured",
        providerReference: null,
      });
    }
    if (cardAmount > 0) {
      if (!cardMethod) {
        setMessage(t.noCard);
        setIsError(true);
        return;
      }
      payments.push({
        clientRequestId: createId(),
        paymentMethodId: cardMethod.id,
        amount: cardAmount,
        tenderedAmount: cardAmount,
        status: "Captured",
        providerReference: `POS-${createId()}`,
      });
    }
    setSaving(true);
    try {
      const response = await auth(`/api/v1/orders/${orderId}/payments`, {
        method: "POST",
        body: JSON.stringify({ payments }),
      });
      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        throw new Error(problem?.errors?.payments?.[0] ?? t.failed);
      }
      setMessage(t.paid);
      setPaid(true);
      void printOrderReceipt(auth, branchId, orderId, language);
      try {
        if (onPaid) await onPaid();
        else {
          const dispatch = await auth("/api/v1/kitchen/tickets", {
            method: "POST",
            body: JSON.stringify({
              branchId,
              orderId,
              clientDispatchId: crypto.randomUUID(),
              orderNumber: null,
              note: null,
              targetMinutes: null,
            }),
          });
          if (!dispatch.ok) throw new Error();
        }
      } catch {
        setMessage(
          language === "ar"
            ? "تم الدفع، لكن تعذر إرسال الطلب للمطبخ. أعد الإرسال من الطلبات الحالية."
            : "Payment completed, but kitchen dispatch failed. Retry from Current orders.",
        );
        setIsError(true);
      }
      closeTimer.current = window.setTimeout(onClose, 3000);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : t.failed);
      setIsError(true);
    } finally {
      setSaving(false);
    }
  }

  const modeButton = (
    m: PaymentMethod,
    label: string,
    Icon: typeof Banknote | null,
    enabled: boolean,
  ) => (
    <Button
      key={m}
      type="button"
      onClick={() => chooseMethod(m)}
      disabled={!enabled}
      className={`inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border px-3 text-sm font-semibold disabled:opacity-40 ${method === m ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card text-muted-foreground hover:bg-muted"}`}
    >
      {Icon && <Icon size={17} />}
      {label}
    </Button>
  );

  const splitField = (
    label: string,
    value: string,
    onChange: (v: string) => void,
    Icon: typeof Banknote,
  ) => (
    <label className="block text-sm font-medium">
      <span className="flex items-center gap-2 text-muted-foreground">
        <Icon size={17} className="text-primary" />
        {label}
      </span>
      <Input
        type="text"
        inputMode="decimal"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="0.000"
        className="mt-2 min-h-14 w-full rounded-xl border border-border bg-card px-4 text-lg font-semibold outline-none focus:border-primary"
      />
    </label>
  );

  return (
    <div className="fixed inset-0 z-[100] grid min-w-0 place-items-end bg-black/35 sm:place-items-center sm:p-5">
      <section
        role="dialog"
        aria-modal="true"
        aria-label={t.title}
        className="max-h-[100dvh] min-w-0 w-full max-w-md overscroll-contain overflow-x-hidden overflow-y-auto rounded-t-2xl bg-background p-4 pb-[max(1rem,env(safe-area-inset-bottom))] shadow-2xl sm:max-h-[calc(100dvh-2.5rem)] sm:rounded-2xl sm:p-6"
      >
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm font-semibold text-primary">{t.title}</p>
            <h2 className="text-2xl font-bold">OMR {total.toFixed(3)}</h2>
          </div>
          <Button
            aria-label={t.close}
            onClick={onClose}
            className="grid size-11 place-items-center rounded-lg bg-card"
          >
            <X size={19} />
          </Button>
        </div>
        {!methodsLoaded ? null : methods.length === 0 ? (
          <p
            role="alert"
            className="mt-6 rounded-xl bg-destructive/10 p-4 text-sm text-destructive"
          >
            {t.none}
          </p>
        ) : (
          <>
            <p className="mt-5 text-sm font-medium">{t.method}</p>
            <div className="mt-2 grid grid-cols-3 gap-2">
              {modeButton("Cash", t.cash, Banknote, !!cashMethod)}
              {modeButton("Card", t.card, CreditCard, !!cardMethod)}
              {modeButton("Mixed", t.mixed, null, !!cashMethod && !!cardMethod)}
            </div>
            {method === "Mixed" && (
              <div className="mt-4 grid grid-cols-2 gap-3">
                {splitField(t.cash, cash, setCashAmount, Banknote)}
                {splitField(t.card, card, setCardAmount, CreditCard)}
              </div>
            )}
            {message && (
              <p
                role={isError ? "alert" : "status"}
                className={`mt-4 text-sm ${isError ? "text-destructive" : "text-success"}`}
              >
                {message}
              </p>
            )}
            <Button
              disabled={saving || paid}
              onClick={() => void pay()}
              className="mt-5 flex min-h-13 w-full items-center justify-center gap-2 rounded-xl bg-primary font-semibold text-primary-foreground disabled:opacity-60"
            >
              <CreditCard size={18} />
              {saving ? "..." : t.pay}
            </Button>
          </>
        )}
      </section>
    </div>
  );
}
