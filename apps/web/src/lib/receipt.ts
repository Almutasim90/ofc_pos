// Customer receipt printed from the cashier's browser. Each branch designs its own free-form header and
// footer (text and/or an image, both optional); the order part in between is always generated.
// Sized for 80mm thermal paper; Chrome started with --kiosk-printing prints it without a dialog.
import { escapeHtml, printHtml, snapshotChoices } from "@/lib/kitchen-slip";
import { paymentMethodName } from "@/lib/payment-method";
import { store } from "@/lib/local-store";

type Language = "ar" | "en";

export type ReceiptLayout = {
  headerText: string | null;
  headerImage: string | null;
  footerText: string | null;
  footerImage: string | null;
};

export type ReceiptLine = {
  quantity: number;
  name: string;
  choices: string[];
  total: number;
};

export type Receipt = {
  language: Language;
  reference: string;
  createdAt: string;
  lines: ReceiptLine[];
  discountAmount: number;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
  payments: Array<{ name: string; amount: number; change: number }>;
};

export const emptyReceiptLayout: ReceiptLayout = {
  headerText: null,
  headerImage: null,
  footerText: null,
  footerImage: null,
};

const money = (value: number) => value.toFixed(3);

// Phone numbers, VAT numbers and handles typed inside Arabic text ("9911 2233", "OM123…", "@ofc") would
// otherwise be laid out right-to-left and print reversed; keep every Latin/digit run left-to-right.
const latinRun = /[@+\w][\w@.+ -]*\w/g;
function escapeIsolated(text: string) {
  let html = "";
  let last = 0;
  for (const match of text.matchAll(latinRun)) {
    html += escapeHtml(text.slice(last, match.index));
    html += `<bdi dir="ltr">${escapeHtml(match[0])}</bdi>`;
    last = match.index + match[0].length;
  }
  return html + escapeHtml(text.slice(last));
}

function block(text: string | null, image: string | null, className: string) {
  if (!text && !image) return "";
  return `<div class="${className}">
  ${image ? `<img src="${escapeHtml(image)}" alt="">` : ""}
  ${text ? `<div class="text">${escapeIsolated(text)}</div>` : ""}
</div>`;
}

export function receiptHtml(layout: ReceiptLayout, receipt: Receipt) {
  const ar = receipt.language === "ar";
  const label = ar
    ? {
        order: "رقم الطلب",
        subtotal: "المجموع قبل الضريبة",
        discount: "الخصم",
        tax: "الضريبة",
        total: "الإجمالي",
        change: "الباقي",
      }
    : {
        order: "Order",
        subtotal: "Subtotal",
        discount: "Discount",
        tax: "VAT",
        total: "Total",
        change: "Change",
      };
  const time = new Date(receipt.createdAt).toLocaleString(
    ar ? "ar-OM" : "en-GB",
    {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    },
  );
  const row = (name: string, value: string, className = "") =>
    `<div class="row ${className}"><span>${escapeHtml(name)}</span><span dir="ltr">${escapeHtml(value)}</span></div>`;
  const lines = receipt.lines
    .map(
      (line) => `<li>
    <div class="row"><span><strong>${line.quantity}×</strong> ${escapeHtml(line.name)}</span><span dir="ltr">${money(line.total)}</span></div>
    ${line.choices.map((c) => `<div class="sub">+ ${escapeHtml(c)}</div>`).join("")}
  </li>`,
    )
    .join("");
  const payments = receipt.payments
    .map(
      (p) =>
        row(p.name, money(p.amount)) +
        (p.change > 0 ? row(label.change, money(p.change)) : ""),
    )
    .join("");
  return `<!doctype html><html lang="${receipt.language}" dir="${ar ? "rtl" : "ltr"}"><head><meta charset="utf-8">
<title>${escapeHtml(receipt.reference)}</title>
<style>
  @page { size: 80mm auto; margin: 4mm; }
  * { box-sizing: border-box; }
  body { margin: 0; font-family: Tahoma, Arial, sans-serif; color: #000; background: #fff; font-size: 13px; }
  .header, .footer { text-align: center; }
  .header { padding-bottom: 6px; border-bottom: 2px dashed #000; }
  .footer { padding-top: 6px; border-top: 2px dashed #000; margin-top: 6px; }
  img { display: block; max-width: 100%; max-height: 40mm; margin: 0 auto 4px; }
  .text { white-space: pre-wrap; }
  .header .text { font-size: 14px; font-weight: 700; }
  .ref { margin: 6px 0 2px; font-size: 22px; font-weight: 900; text-align: center; }
  .time { text-align: center; font-size: 12px; }
  ul { list-style: none; margin: 6px 0; padding: 6px 0; border-top: 1px dashed #000; border-bottom: 1px dashed #000; }
  li + li { margin-top: 4px; }
  .row { display: flex; justify-content: space-between; gap: 8px; }
  .sub { padding-inline-start: 18px; font-size: 12px; }
  .total { font-size: 17px; font-weight: 900; border-top: 1px solid #000; margin-top: 3px; padding-top: 3px; }
  .payments { margin-top: 6px; }
</style></head><body>
${block(layout.headerText, layout.headerImage, "header")}
<div class="ref">${escapeHtml(label.order)} <bdi dir="ltr">${escapeHtml(receipt.reference)}</bdi></div>
<div class="time">${escapeHtml(time)}</div>
<ul>${lines}</ul>
${receipt.discountAmount > 0 ? row(label.discount, `-${money(receipt.discountAmount)}`) : ""}
${row(label.subtotal, money(receipt.netAmount))}
${row(label.tax, money(receipt.taxAmount))}
${row(label.total, `OMR ${money(receipt.grossAmount)}`, "total")}
${payments ? `<div class="payments">${payments}</div>` : ""}
${block(layout.footerText, layout.footerImage, "footer")}
</body></html>`;
}

// A made-up order so the designer can preview the header and footer around a realistic receipt.
export function sampleReceipt(language: Language): Receipt {
  const ar = language === "ar";
  return {
    language,
    reference: "#1024",
    createdAt: new Date().toISOString(),
    lines: [
      {
        quantity: 2,
        name: ar ? "وجبة دجاج مقلي" : "Fried chicken meal",
        choices: [ar ? "حار" : "Spicy"],
        total: 5.6,
      },
      {
        quantity: 1,
        name: ar ? "بطاطس كبير" : "Large fries",
        choices: [],
        total: 1.2,
      },
    ],
    discountAmount: 0,
    netAmount: 6.476,
    taxAmount: 0.324,
    grossAmount: 6.8,
    payments: [{ name: ar ? "نقدي" : "Cash", amount: 6.8, change: 0 }],
  };
}

type Auth = (path: string, init?: RequestInit) => Promise<Response>;

// The layout (images included) is kept in memory for a few minutes so each sale doesn't refetch it, and
// in local storage so a receipt still prints with the branch's design while the register is offline.
const layoutTtlMs = 5 * 60_000;
const layoutCache = new Map<string, { layout: ReceiptLayout; at: number }>();
const storageKey = (branchId: string) => `receipt-layout:${branchId}`;

export function rememberReceiptLayout(branchId: string, layout: ReceiptLayout) {
  layoutCache.set(branchId, { layout, at: Date.now() });
  store.set(storageKey(branchId), layout);
}

export function storedReceiptLayout(branchId: string) {
  return (
    layoutCache.get(branchId)?.layout ??
    store.get<ReceiptLayout>(storageKey(branchId)) ??
    emptyReceiptLayout
  );
}

export async function getReceiptLayout(auth: Auth, branchId: string) {
  const cached = layoutCache.get(branchId);
  if (cached && Date.now() - cached.at < layoutTtlMs) return cached.layout;
  try {
    const response = await auth(
      `/api/v1/print/receipt-layout?branchId=${branchId}`,
    );
    if (!response.ok) throw new Error("receipt-layout");
    const layout = (await response.json()) as ReceiptLayout;
    rememberReceiptLayout(branchId, layout);
    return layout;
  } catch {
    return storedReceiptLayout(branchId);
  }
}

export function printReceipt(layout: ReceiptLayout, receipt: Receipt) {
  return printHtml(receiptHtml(layout, receipt));
}

type OrderResponse = {
  number?: number | null;
  createdAt: string;
  manualDiscountAmount?: number;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
  lines: Array<{
    productNameAr: string;
    productNameEn: string;
    quantity: number;
    unitGrossAmount: number;
    selectionsSnapshot?: string;
  }>;
};
type PaymentResponse = {
  amount: number;
  changeAmount: number;
  status: string;
  nameAr: string | null;
  nameEn: string | null;
  code: string | null;
  kind: string | null;
};

// Fetches a paid order and prints its receipt with the branch's header and footer. Resolves to false when
// anything needed could not be loaded, so the caller can tell the cashier.
export async function printOrderReceipt(
  auth: Auth,
  branchId: string,
  orderId: string,
  language: Language,
) {
  try {
    const [orderResponse, paymentsResponse, layout] = await Promise.all([
      auth(`/api/v1/orders/${orderId}`),
      auth(`/api/v1/orders/${orderId}/payments`),
      getReceiptLayout(auth, branchId),
    ]);
    if (!orderResponse.ok || !paymentsResponse.ok) return false;
    const order = (await orderResponse.json()) as OrderResponse;
    const payments = (await paymentsResponse.json()) as PaymentResponse[];
    const lines = order.lines.filter((line) => line.quantity > 0);
    return printHtml(
      receiptHtml(layout, {
        language,
        reference: order.number ? `#${order.number}` : "#—",
        createdAt: order.createdAt,
        lines: lines.map((line) => ({
          quantity: line.quantity,
          name: language === "ar" ? line.productNameAr : line.productNameEn,
          choices: snapshotChoices(line.selectionsSnapshot, language),
          total: line.unitGrossAmount * line.quantity,
        })),
        discountAmount: order.manualDiscountAmount ?? 0,
        netAmount: order.netAmount,
        taxAmount: order.taxAmount,
        grossAmount: order.grossAmount,
        payments: payments
          .filter((p) => p.status === "Captured" || p.status === "Authorized")
          .map((p) => ({
            name: paymentMethodName(language, p),
            amount: p.amount,
            change: p.changeAmount,
          })),
      }),
    );
  } catch {
    return false;
  }
}
