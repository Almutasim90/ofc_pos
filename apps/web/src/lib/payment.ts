export type PaymentMethod = "Cash" | "Card" | "Mixed";

// Match decimal.Round(..., 3) on the API, including midpoint-to-even rounding.
export const roundMoney = (value: number) => {
  const scaled = value * 1000;
  const lower = Math.floor(scaled);
  const rounded = Math.abs(scaled - lower - 0.5) < 1e-9
    ? (lower % 2 === 0 ? lower : lower + 1)
    : Math.round(scaled);
  return rounded / 1000;
};

export function normalizeMoneyInput(raw: string): string | null {
  const normalized = raw
    .replace(/[٠-٩]/g, (digit) => String("٠١٢٣٤٥٦٧٨٩".indexOf(digit)))
    .replace(/[٫,]/g, ".");
  if (!/^\d*(?:\.\d{0,3})?$/.test(normalized)) return null;
  return normalized.startsWith(".") ? `0${normalized}` : normalized;
}

export function paymentAmounts(method: PaymentMethod, total: number, cash: string) {
  const cashAmount = method === "Cash" ? total : method === "Card" ? 0 : Number(cash);
  return { cashAmount, cardAmount: roundMoney(total - cashAmount) };
}

export function cashFromSplitInput(field: "cash" | "card", value: string, total: number): string | null {
  if (value === "") return "";
  const amount = Number(value);
  if (!Number.isFinite(amount) || amount < 0 || amount > total || roundMoney(amount) !== amount) return null;
  return field === "cash" ? value : String(roundMoney(total - amount));
}

export function validPayment(method: PaymentMethod, total: number, cash: string) {
  const amounts = paymentAmounts(method, total, cash);
  return Number.isFinite(amounts.cashAmount) && amounts.cashAmount >= 0 && amounts.cardAmount >= 0
    && roundMoney(amounts.cashAmount) === amounts.cashAmount
    && (method !== "Mixed" || (amounts.cashAmount > 0 && amounts.cardAmount > 0));
}
