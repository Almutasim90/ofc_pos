import { memo, useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Banknote,
  CreditCard,
  Minus,
  Plus,
  QrCode,
  Search,
  ShoppingBag,
  Store,
  UtensilsCrossed,
  WifiOff,
  ScanBarcode,
  ReceiptText,
  Trash2,
  Clock,
  Timer,
  UserRound,
  MonitorSmartphone,
  Printer,
  ChevronUp,
  ChevronDown,
  ChevronsUpDown,
  X,
} from "lucide-react";
import { createId, store } from "@/lib/local-store";
import { normalizeMoneyInput, type PaymentMethod } from "@/lib/payment";
import { paymentMethodName } from "@/lib/payment-method";
import {
  enqueue,
  setBranchId as persistBranch,
  warmOfflineStore,
} from "@/lib/sync-outbox";
import {
  useQrOrdersLive,
  type QrOrderReceivedEvent,
  type QrOrderReviewedEvent,
} from "@/lib/orders-realtime";
import { useBarcodeScanner } from "@/lib/use-barcode-scanner";
import { VirtualTicketList } from "@/app/VirtualTicketList";
import "@/app/pos-register.css";
import { ProductPhoto } from "@/app/ProductPhoto";
import { PaymentDialog } from "@/app/PaymentDialog";
import { FormDialog } from "@/app/FormDialog";
import { printKitchenSlips, snapshotChoices } from "@/lib/kitchen-slip";
import {
  getReceiptLayout,
  printOrderReceipt,
  printReceipt,
  storedReceiptLayout,
} from "@/lib/receipt";
import {
  openCustomerDisplayWindow,
  openDisplayChannel,
  type DisplayMessage,
} from "@/lib/customer-display";
import { SearchableSelect } from "@/app/SearchableSelect";
import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Input } from "@/components/ui/input";

type Language = "ar" | "en";
type SalesChannelKind = "InStore" | "DineIn" | "Takeaway" | "Qr" | "Electronic";
type SalesChannel = {
  id: string;
  code: string;
  nameAr: string;
  nameEn: string;
  kind?: SalesChannelKind;
};

function salesChannelKind(channel: SalesChannel): SalesChannelKind {
  if (channel.kind) return channel.kind;
  switch (channel.code.toUpperCase()) {
    case "DINEIN":
      return "DineIn";
    case "TAKEAWAY":
      return "Takeaway";
    case "WEBQR":
      return "Qr";
    case "POS":
      return "InStore";
    default:
      return "Electronic";
  }
}
type Context = {
  branches: Array<{ id: string; nameAr: string; nameEn: string }>;
  channels: SalesChannel[];
};
type Choice = {
  id: string;
  productId: string;
  nameAr: string;
  nameEn: string;
  priceAdjustment: number;
  isDefault: boolean;
  maxQuantity: number;
  isAvailable?: boolean;
};
type Group = {
  id: string;
  nameAr: string;
  nameEn: string;
  isRequired: boolean;
  minSelections: number;
  maxSelections: number;
  options: Choice[];
};
type Pricing = {
  listPrice: number;
  discountRate: number;
  taxRate: number;
  taxCalculationMode: "Exclusive" | "Inclusive";
  priceSource: string;
  priceRuleId: string | null;
  promotionId: string | null;
  taxRuleId: string | null;
  catalogVersionId: string | null;
  catalogVersionNumber: number | null;
};
type Product = {
  id: string;
  sku: string;
  barcode: string | null;
  categoryId: string;
  categoryNameAr: string;
  categoryNameEn: string;
  nameAr: string;
  nameEn: string;
  basePrice: number | null;
  imageUrl: string | null;
  pricing: Pricing;
  selectionGroups: Group[];
};
type CartLine = {
  key: string;
  product: Product;
  quantity: number;
  note: string;
  selections: Record<string, string[]>;
};
type Method = {
  id: string;
  code?: string;
  nameAr: string;
  nameEn: string;
  kind: string;
};
const words = {
  ar: {
    title: "نقطة البيع",
    search: "ابحث عن صنف",
    all: "الكل",
    cart: "السلة",
    empty: "أضف أصنافًا للبدء",
    hold: "تعليق",
    payLater: "دفع لاحقاً",
    sentPayLater: "أُرسل الطلب للمطبخ. ادفعه لاحقاً من الطلبات الحالية.",
    payLaterOffline: "الدفع لاحقاً يحتاج اتصالاً بالإنترنت.",
    send: "الدفع",
    confirmPay: "تأكيد الدفع",
    clearOrder: "إفراغ",
    cashier: "الكاشير",
    notReceivedCount: "طلبات لم تُستلم في المطبخ",
    printAllKitchen: "طباعة الكل للمطبخ",
    kitchenScreenOffline: "شاشة المطبخ غير متصلة منذ",
    kitchenScreenOfflineNote:
      "الطلبات الجديدة لن تظهر في المطبخ؛ اطبعها أو أبلغ المطبخ.",
    hide: "إخفاء",
    sentToKitchen: "تم إرسال الطلب للمطبخ.",
    closeMessage: "إغلاق الرسالة",
    offlineSaveFailed:
      "تعذر حفظ الطلب على هذا الجهاز. لم يُسجَّل شيء؛ لا تُفرغ السلة وأعد المحاولة.",
    customerDisplay: "شاشة العميل",
    customerDisplayBlocked: "اسمح بالنوافذ المنبثقة لفتح شاشة العميل.",
    shiftOpenSince: "الوردية مفتوحة منذ",
    noOpenShift: "لا توجد وردية مفتوحة",
    editOrder: "تعديل",
    editingOrder: "تعديل الطلب",
    cancelEdit: "إلغاء التعديل",
    addOnOrder: "طلب إضافي",
    addOnFor: "طلب إضافي للطلب",
    emptyCartFirst: "أفرغ السلة أولاً ثم أعد المحاولة.",
    editUnavailable: "بعض أصناف هذا الطلب غير متوفرة حالياً، لا يمكن تعديله.",
    editDiscountDropped: "كان على الطلب خصم؛ أعد إدخاله قبل الدفع.",
    editOffline: "تعديل الطلبات يحتاج اتصالاً بالخادم.",
    printKitchen: "طباعة للمطبخ",
    kitchenSlip: "طلب مطبخ",
    notReceived: "لم يُستلم في المطبخ",
    kitchenInformed: "تم إبلاغ المطبخ",
    confirmClear: "تأكيد الإفراغ",
    backToCart: "رجوع",
    notes: "ملاحظة",
    branch: "الفرع",
    channel: "قناة البيع",
    offline: "غير متصل: ستتم المزامنة عند عودة الاتصال",
    online: "متصل",
    total: "الإجمالي",
    add: "إضافة",
    confirm: "تأكيد الاختيارات",
    selections: "الاختيارات",
    saved: "تم حفظ الطلب",
    unavailable: "تعذر حفظ الطلب، سيبقى في السلة",
    sessionExpired:
      "انتهت صلاحية الجلسة. سجّل الدخول من جديد؛ الطلب سيبقى في السلة.",
    viewCart: "عرض السلة",
    offlinePayTitle: "دفع غير متصل",
    offlinePayMethod: "طريقة الدفع",
    offlinePayTendered: "المبلغ المستلم",
    offlinePayConfirm: "تأكيد الدفع وحفظ الطلب",
    offlinePayCancel: "إلغاء",
    offlineNoMethods:
      "لا توجد وسائل دفع محفوظة لهذا الفرع؛ اتصل بالإنترنت مرة واحدة على الأقل",
    offlinePayInvalid: "المبلغ المستلم غير كافٍ",
    heldOrders: "الطلبات الحالية",
    heldScope: "طلبات الوردية الحالية والطلبات غير المكتملة",
    resume: "استئناف",
    noHeld: "لا توجد طلبات حالية",
    heldSearch: "ابحث برقم الطلب أو الطاولة",
    heldNoMatch: "لا توجد طلبات مطابقة",
    filterCurrent: "الحالية",
    colOrder: "الطلب",
    colType: "النوع",
    colStage: "حالة الطلب",
    colPayment: "الدفع",
    colTime: "الوقت",
    colAmount: "المبلغ",
    colActions: "إجراءات",
    minutesAgo: "منذ",
    minutesShort: "د",
    unpaidSummary: "غير مدفوع",
    currentSummary: "طلبات حالية",
    readySummary: "جاهزة للتسليم",
    filterAll: "الكل",
    filterUnpaid: "غير مدفوع",
    filterKitchen: "في المطبخ",
    filterReady: "جاهز",
    filterDone: "مكتمل",
    filterCancelled: "ملغى",
    stageHeld: "معلّق",
    stageNotSent: "لم يُرسل للمطبخ",
    stageRefunded: "مسترجع",
    paidBadge: "مدفوع",
    unpaidBadge: "غير مدفوع",
    sendKitchen: "للمطبخ",
    payNow: "دفع",
    heldSince: "منذ",
    orderRef: "رقم الطلب",
    table: "الطاولة",
    back: "رجوع",
    loadingOrder: "جارٍ تحميل تفاصيل الطلب…",
    orderItems: "الأصناف",
    orderNet: "صافي المبلغ",
    orderTax: "الضريبة",
    dailySales: "مبيعات اليوم",
    dailySalesOrders: "طلب",
    qrNew: "وصل طلب QR جديد",
    qrPending: "طلب QR جديد بانتظار الاعتماد",
    qrApproved: "تم اعتماد طلب QR",
    qrRejected: "تم رفض طلب QR",
    qrReviewTitle: "طلبات QR بانتظار الاعتماد",
    qrApprove: "اعتماد",
    qrReject: "رفض",
    payMethod: "طريقة الدفع",
    cash: "نقدي",
    card: "بطاقة",
    mixed: "نقد + بطاقة",
    payNoMethods: "لا توجد وسائل دفع مفعلة لهذا الفرع.",
    payExceed: "المبلغ المدخل أكبر من المستحق.",
    payInvalid: "أدخل مبلغًا صحيحًا لتقسيم الدفع.",
    payNoCash: "لا توجد طريقة دفع نقدية.",
    payNoCard: "لا توجد طريقة دفع بالبطاقة.",
    payFailed: "تعذر تسجيل الدفع.",
    paidAndSent: "تم الدفع وإرسال الطلب للمطبخ.",
    addDiscount: "إضافة خصم",
    discountPercentage: "نسبة %",
    discountAmount: "مبلغ ثابت",
    discountApply: "تطبيق",
    discountRemove: "إزالة الخصم",
    discountLabel: "خصم",
    subtotal: "المجموع الفرعي",
    discountTooHigh: "لا يمكن أن يتجاوز الخصم الحد الأقصى المسموح.",
    discountFailed: "تعذر تطبيق الخصم على هذا الطلب.",
    channelLocked:
      "أفرغ السلة أولاً لتغيير قناة البيع أو شركة الطلب الإلكتروني — كل قناة لها قائمة أسعار مختلفة.",
  },
  en: {
    title: "Point of sale",
    search: "Search products",
    all: "All",
    cart: "Cart",
    empty: "Add products to begin",
    hold: "Hold",
    payLater: "Pay later",
    sentPayLater:
      "Order sent to the kitchen. Take payment later from Current orders.",
    payLaterOffline: "Pay later needs an internet connection.",
    send: "Pay",
    confirmPay: "Confirm payment",
    clearOrder: "Clear",
    cashier: "Cashier",
    notReceivedCount: "orders not received in the kitchen",
    printAllKitchen: "Print all for kitchen",
    kitchenScreenOffline: "Kitchen screen disconnected since",
    kitchenScreenOfflineNote:
      "New orders will not appear in the kitchen; print them or tell the kitchen.",
    hide: "Hide",
    sentToKitchen: "Order sent to kitchen.",
    closeMessage: "Dismiss message",
    offlineSaveFailed:
      "Could not store the order on this device. Nothing was recorded; keep the cart and try again.",
    customerDisplay: "Customer display",
    customerDisplayBlocked: "Allow pop-ups to open the customer display.",
    shiftOpenSince: "Shift open since",
    noOpenShift: "No open shift",
    editOrder: "Edit",
    editingOrder: "Editing order",
    cancelEdit: "Cancel edit",
    addOnOrder: "Add-on order",
    addOnFor: "Add-on for order",
    emptyCartFirst: "Clear the cart first, then try again.",
    editUnavailable:
      "Some items of this order are unavailable now; it cannot be edited.",
    editDiscountDropped:
      "This order had a discount; re-enter it before paying.",
    editOffline: "Editing orders needs a server connection.",
    printKitchen: "Print for kitchen",
    kitchenSlip: "Kitchen order",
    notReceived: "Not received in the kitchen",
    kitchenInformed: "Kitchen informed",
    confirmClear: "Confirm clear",
    backToCart: "Back",
    notes: "Note",
    branch: "Branch",
    channel: "Sales channel",
    offline: "Offline: the cart will sync when connection returns",
    online: "Online",
    total: "Total",
    add: "Add",
    confirm: "Confirm selections",
    selections: "Selections",
    saved: "Order saved",
    unavailable: "Unable to save; cart remains available",
    sessionExpired:
      "Your session has expired. Please sign in again; your cart will stay saved.",
    viewCart: "View cart",
    offlinePayTitle: "Offline payment",
    offlinePayMethod: "Payment method",
    offlinePayTendered: "Amount tendered",
    offlinePayConfirm: "Confirm payment and save order",
    offlinePayCancel: "Cancel",
    offlineNoMethods:
      "No payment methods are cached for this branch; connect to the internet at least once first",
    offlinePayInvalid: "Tendered amount does not cover the total",
    heldOrders: "Current orders",
    heldScope: "This shift's orders and any unfinished orders",
    resume: "Resume",
    noHeld: "No current orders",
    heldSearch: "Search by order # or table",
    heldNoMatch: "No orders match",
    filterCurrent: "Current",
    colOrder: "Order",
    colType: "Type",
    colStage: "Status",
    colPayment: "Payment",
    colTime: "Time",
    colAmount: "Amount",
    colActions: "Actions",
    minutesAgo: "",
    minutesShort: "min ago",
    unpaidSummary: "Unpaid",
    currentSummary: "Current orders",
    readySummary: "Ready to hand over",
    filterAll: "All",
    filterUnpaid: "Unpaid",
    filterKitchen: "In kitchen",
    filterReady: "Ready",
    filterDone: "Completed",
    filterCancelled: "Cancelled",
    stageHeld: "Held",
    stageNotSent: "Not sent to kitchen",
    stageRefunded: "Refunded",
    paidBadge: "Paid",
    unpaidBadge: "Unpaid",
    sendKitchen: "Kitchen",
    payNow: "Pay",
    heldSince: "Held since",
    orderRef: "Order no.",
    table: "Table",
    back: "Back",
    loadingOrder: "Loading order details…",
    orderItems: "Items",
    orderNet: "Net amount",
    orderTax: "Tax",
    dailySales: "Today's sales",
    dailySalesOrders: "orders",
    qrNew: "New QR order received",
    qrPending: "New QR order awaiting approval",
    qrApproved: "QR order approved",
    qrRejected: "QR order rejected",
    qrReviewTitle: "QR orders awaiting approval",
    qrApprove: "Approve",
    qrReject: "Reject",
    payMethod: "Payment method",
    cash: "Cash",
    card: "Card",
    mixed: "Cash + Card",
    payNoMethods: "No active payment methods are configured for this branch.",
    payExceed: "Amount entered exceeds the amount due.",
    payInvalid: "Enter a valid split payment amount.",
    payNoCash: "No cash payment method is configured.",
    payNoCard: "No card payment method is configured.",
    payFailed: "Unable to record the payment.",
    paidAndSent: "Payment completed and the order was sent to the kitchen.",
    addDiscount: "Add discount",
    discountPercentage: "Percentage %",
    discountAmount: "Fixed amount",
    discountApply: "Apply",
    discountRemove: "Remove discount",
    discountLabel: "Discount",
    subtotal: "Subtotal",
    discountTooHigh: "The discount cannot exceed the maximum allowed.",
    discountFailed: "Unable to apply the discount to this order.",
    channelLocked:
      "Empty the cart first to change the sales channel or electronic order company — each channel has its own price list.",
  },
} as const;
type HeldOrder = {
  id: string;
  number?: number | null;
  status: string;
  grossAmount: number;
  note: string | null;
  createdAt: string;
  table: { code: string; nameAr: string; nameEn: string } | null;
  salesChannelId?: string;
  paidAt?: string | null;
};
const payableStatuses = [
  "Draft",
  "Pending",
  "Confirmed",
  "SentToKitchen",
  "Preparing",
  "Ready",
  "Completed",
];
type OrderDetailLine = {
  id: string;
  productId: string;
  productNameAr: string;
  productNameEn: string;
  quantity: number;
  note: string | null;
  unitGrossAmount: number;
  selectionsSnapshot?: string;
};
type OrderDetail = {
  id: string;
  number?: number | null;
  salesChannelId?: string;
  manualDiscountAmount?: number;
  status: string;
  note: string | null;
  netAmount: number;
  taxAmount: number;
  grossAmount: number;
  createdAt: string;
  lines: OrderDetailLine[];
};
// Rebuilds a cart line's chosen options (groupId -> optionIds) from the order line's JSON snapshot, which
// the server may serialize in PascalCase or camelCase.
function snapshotSelections(snapshot: string | undefined) {
  const selections: Record<string, string[]> = {};
  if (!snapshot) return selections;
  try {
    type Choice = { OptionId?: string; optionId?: string };
    type Group = {
      Id?: string;
      id?: string;
      Choices?: Choice[];
      choices?: Choice[];
    };
    for (const group of JSON.parse(snapshot) as Group[]) {
      const groupId = group.Id ?? group.id;
      if (!groupId) continue;
      selections[groupId] = (group.choices ?? group.Choices ?? [])
        .map((choice) => choice.OptionId ?? choice.optionId)
        .filter((id): id is string => !!id);
    }
  } catch {
    /* A malformed snapshot just means no options are preselected. */
  }
  return selections;
}
function PosClock({ language }: { language: Language }) {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 15_000);
    return () => clearInterval(timer);
  }, []);
  return (
    <time dateTime={now.toISOString()} className="pos-session-item">
      <Clock size={14} aria-hidden="true" />
      {now.toLocaleTimeString(language, { hour: "numeric", minute: "2-digit" })}
    </time>
  );
}
function PosSessionInfo({
  language,
  cashier,
  cashierLabel,
  shiftLabel,
  shiftMissing,
}: {
  language: Language;
  cashier: string;
  cashierLabel: string;
  shiftLabel: string | null;
  shiftMissing: boolean;
}) {
  return (
    <div className="pos-session" aria-live="polite">
      {cashier && (
        <span className="pos-session-item" title={cashierLabel}>
          <UserRound size={14} aria-hidden="true" />
          <span className="sr-only">{cashierLabel}: </span>
          {cashier}
        </span>
      )}
      {shiftLabel && (
        <span
          className={`pos-session-item ${shiftMissing ? "is-warning" : ""}`}
        >
          <Timer size={14} aria-hidden="true" />
          {shiftLabel}
        </span>
      )}
      <PosClock language={language} />
    </div>
  );
}
// Adding an item that is already in the cart with the same options raises that line's quantity instead
// of opening a second line. A line that carries a note ("no onion") stays separate, so the new unit does
// not silently inherit an instruction meant for another one.
function sameSelections(
  a: Record<string, string[]>,
  b: Record<string, string[]>,
) {
  const normalize = (value: Record<string, string[]>) =>
    JSON.stringify(
      Object.entries(value)
        .filter(([, ids]) => ids.length > 0)
        .map(([group, ids]) => [group, [...ids].sort()])
        .sort(([x], [y]) => String(x).localeCompare(String(y))),
    );
  return normalize(a) === normalize(b);
}
function addToCart(
  current: CartLine[],
  product: Product,
  selections: Record<string, string[]>,
): CartLine[] {
  const match = current.find(
    (line) =>
      line.product.id === product.id &&
      !line.note.trim() &&
      sameSelections(line.selections, selections),
  );
  if (match)
    return current.map((line) =>
      line === match ? { ...line, quantity: line.quantity + 1 } : line,
    );
  return [
    ...current,
    { key: createId(), product, quantity: 1, note: "", selections },
  ];
}
// Names of the options chosen on a cart line, for the receipt line and the customer display.
function lineChoiceNames(line: CartLine, language: Language) {
  return Object.entries(line.selections).flatMap(([groupId, ids]) => {
    const group = line.product.selectionGroups.find((g) => g.id === groupId);
    return ids.flatMap((id) => {
      const option = group?.options.find((o) => o.id === id);
      return option ? [language === "ar" ? option.nameAr : option.nameEn] : [];
    });
  });
}
// Orders are identified to staff by their sequential number only (never the internal id).
function orderLabel(number?: number | null) {
  return number ? `#${number}` : "#—";
}
type QrToast = { id: number; text: string };
type QrPendingOrder = {
  id: string;
  number?: number | null;
  clientRequestId: string;
  grossAmount: number;
  lines: Array<{
    id: string;
    productNameAr: string;
    productNameEn: string;
    quantity: number;
  }>;
  approval: { id: string; status: string } | null;
};
const channelIcons: Record<string, typeof Store> = {
  POS: Store,
  DINEIN: UtensilsCrossed,
  TAKEAWAY: ShoppingBag,
  WEBQR: QrCode,
};
function channelIcon(code: string) {
  return channelIcons[code] ?? Store;
}
const channelLogos: Record<string, string> = {
  TALABAT: "/channels/talabat.jpg",
  TMDONE: "/channels/tmdone.png",
  KHIDMA: "/channels/khedmah.jpg",
};
function channelLogo(code: string) {
  return channelLogos[code.toUpperCase()];
}

export function PosSection({
  language,
  kiosk,
  permissions,
  cashierName = "",
}: {
  language: Language;
  kiosk: boolean;
  permissions: string[] | null;
  cashierName?: string;
}) {
  const t = words[language];
  const canDiscount =
    permissions !== null && permissions.includes("orders.discount");
  const name = (x: {
    nameAr: string;
    nameEn: string;
    code?: string;
    kind?: string;
  }) => paymentMethodName(language, x);
  const [context, setContext] = useState<Context | null>(null);
  const [branchId, setBranchId] = useState("");
  const [channelId, setChannelId] = useState("");
  const [products, setProducts] = useState<Product[]>([]);
  const [category, setCategory] = useState("");
  const [search, setSearch] = useState("");
  const [cart, setCart] = useState<CartLine[]>(
    () => store.get<CartLine[]>("pos-cart") ?? [],
  );
  const [customizing, setCustomizing] = useState<Product | null>(null);
  const [selections, setSelections] = useState<Record<string, string[]>>({});
  const [online, setOnline] = useState(navigator.onLine);
  const [message, setMessage] = useState("");
  const messageIsSuccess = (
    [t.sentToKitchen, t.paidAndSent, t.saved, t.offline] as string[]
  ).includes(message);
  useEffect(() => {
    if (!message) return;
    const timer = setTimeout(
      () => setMessage(""),
      messageIsSuccess ? 4000 : 8000,
    );
    return () => clearTimeout(timer);
  }, [message, messageIsSuccess]);
  const [cartOpen, setCartOpen] = useState(false);
  const [payment, setPayment] = useState<{
    orderId: string;
    total: number;
  } | null>(null);
  const [offlineMethods, setOfflineMethods] = useState<Method[]>(
    () => store.get<Method[]>("pos-payment-methods") ?? [],
  );
  const [offlinePay, setOfflinePay] = useState<{
    methodId: string;
    tendered: string;
  } | null>(null);
  const [payMethod, setPayMethod] = useState<PaymentMethod>("Cash");
  // Payment methods are only shown after the cashier presses Pay, keeping the receipt compact.
  useEffect(() => warmOfflineStore(), []);
  const [payStep, setPayStep] = useState(false);
  // Opening time of the branch's open shift (null = none open, undefined = not known yet / offline).
  const [shiftOpenedAt, setShiftOpenedAt] = useState<string | null | undefined>(
    undefined,
  );
  // An open order loaded back into the cart: Pay/Hold update it in place instead of creating a new one.
  const [editing, setEditing] = useState<{
    orderId: string;
    number: number | null;
    status: string;
  } | null>(null);
  // A new order rung up for a customer whose earlier order is already paid; linked through its note.
  const [addOnFor, setAddOnFor] = useState<number | null>(null);
  // Orders sent to the kitchen tablet that it has not acknowledged yet; after 20s the cashier is warned
  // and can print the slip or confirm they told the kitchen.
  const [kitchenWatch, setKitchenWatch] = useState<
    Array<{
      orderId: string;
      number: number | null;
      sentAt: number;
      late: boolean;
    }>
  >([]);
  // Clearing needs a second tap so a stray touch never wipes an order.
  const [confirmClear, setConfirmClear] = useState(false);
  useEffect(() => {
    if (!confirmClear) return;
    const timer = setTimeout(() => setConfirmClear(false), 3000);
    return () => clearTimeout(timer);
  }, [confirmClear]);
  const [payCash, setPayCash] = useState("");
  const [payCard, setPayCard] = useState("");
  const [payMessage, setPayMessage] = useState("");
  const [discountOpen, setDiscountOpen] = useState(false);
  const [discountType, setDiscountType] = useState<"Percentage" | "Amount">(
    "Percentage",
  );
  const [discountValue, setDiscountValue] = useState("");
  useEffect(() => {
    setPayCash("");
    setPayCard("");
    setPayMessage("");
    setDiscountOpen(false);
    setDiscountValue("");
  }, [branchId]);
  const [heldOrders, setHeldOrders] = useState<HeldOrder[]>([]);
  const [heldOpen, setHeldOpen] = useState(false);
  const [heldSearch, setHeldSearch] = useState("");
  const [heldFilter, setHeldFilter] = useState<
    "current" | "unpaid" | "ready" | "done" | "cancelled" | "all"
  >("current");
  const [heldSort, setHeldSort] = useState<{
    key: "number" | "type" | "stage" | "paid" | "time" | "amount";
    dir: "asc" | "desc";
  }>({ key: "time", dir: "desc" });
  const [detailOrderId, setDetailOrderId] = useState<string | null>(null);
  const [orderDetail, setOrderDetail] = useState<OrderDetail | null>(null);
  const [salesSummary, setSalesSummary] = useState<{
    count: number;
    gross: number;
  } | null>(null);
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const searchRef = useRef<HTMLInputElement>(null);
  const salesGridRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const grid = salesGridRef.current;
    if (!grid) return;
    // Account for the actual toolbar height, wrapping and display scaling so checkout
    // stays inside the viewport instead of relying on a fixed header-height estimate.
    const resize = () => {
      const rect = grid.getBoundingClientRect();
      const scale = grid.offsetWidth ? rect.width / grid.offsetWidth : 1;
      const height = Math.max(240, (window.innerHeight - rect.top) / scale);
      grid.style.setProperty("--pos-height", `${height}px`);
    };
    const observer = new ResizeObserver(resize);
    if (grid.parentElement) observer.observe(grid.parentElement);
    const header = document.querySelector("header");
    if (header) observer.observe(header);
    window.addEventListener("resize", resize);
    resize();
    return () => {
      observer.disconnect();
      window.removeEventListener("resize", resize);
    };
  }, [kiosk]);
  const requestRef = useRef({ snapshot: "", id: "" });
  const [qrToasts, setQrToasts] = useState<QrToast[]>([]);
  const qrToastId = useRef(0);
  const [qrPending, setQrPending] = useState<QrPendingOrder[]>([]);
  const [qrReviewing, setQrReviewing] = useState<string | null>(null);
  const auth = (path: string, init?: RequestInit) =>
    fetch(path, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`,
        ...(init?.headers ?? {}),
      },
    });
  // 401 means the token itself is invalid/expired; 403 here means the token is valid but the user/branch
  // link it points at is gone (e.g. accounts were reseeded) — both need a fresh login, not a generic retry.
  function handleAuthFailure(response: Response): boolean {
    if (response.status !== 401 && response.status !== 403) return false;
    setMessage(t.sessionExpired);
    store.remove("session-token");
    setTimeout(() => location.reload(), 1500);
    return true;
  }
  useEffect(() => {
    const update = () => setOnline(navigator.onLine);
    addEventListener("online", update);
    addEventListener("offline", update);
    return () => {
      removeEventListener("online", update);
      removeEventListener("offline", update);
    };
  }, []);
  useEffect(() => {
    void (async () => {
      try {
        let value = store.get<Context>("pos-context");
        if (navigator.onLine) {
          const response = await auth("/api/v1/pos/context");
          if (!response.ok) throw new Error();
          value = (await response.json()) as Context;
          store.set("pos-context", value);
        }
        if (!value) throw new Error();
        setContext(value);
        setBranchId(
          value.branches.find((b) => b.id === store.get<string>("pos-branch"))
            ?.id ??
            value.branches[0]?.id ??
            "",
        );
        setChannelId(
          value.channels.find((c) => c.id === store.get<string>("pos-channel"))
            ?.id ??
            value.channels.find((c) => c.code.toUpperCase() === "DINEIN")?.id ??
            value.channels.find((c) => c.code.toUpperCase() === "POS")?.id ??
            value.channels[0]?.id ??
            "",
        );
      } catch {
        setMessage(
          language === "ar"
            ? "تعذر تحميل بيانات البيع. تحقق من الاتصال ثم حدّث الصفحة."
            : "Unable to load POS. Check the connection and refresh.",
        );
      }
    })();
  }, []);
  useEffect(() => {
    if (!branchId || !channelId) return;
    let live = true;
    setCatalogLoading(true);
    setCategory("");
    store.set("pos-branch", branchId);
    store.set("pos-channel", channelId);
    void (async () => {
      try {
        const key = "pos-catalog-" + branchId + "-" + channelId;
        let value = store.get<Product[]>(key);
        if (online) {
          const response = await auth(
            "/api/v1/pos/catalog?branchId=" +
              branchId +
              "&salesChannelId=" +
              channelId,
          );
          if (!response.ok) throw new Error();
          value = (await response.json()) as Product[];
          store.set(key, value);
        }
        if (!value) throw new Error();
        if (live) setProducts(value);
      } catch {
        if (live) {
          setProducts([]);
          setMessage(
            language === "ar"
              ? "تعذر تحميل المنتجات لهذا الفرع. تحقق من الاتصال وتوفر المنتجات."
              : "Unable to load this branch’s products. Check connection and availability.",
          );
        }
      } finally {
        if (live) setCatalogLoading(false);
      }
    })();
    return () => {
      live = false;
    };
  }, [branchId, channelId, online]);
  // Payment methods are cached locally the moment we're online so an offline sale can still name a real
  // method and collect a tendered amount instead of skipping payment capture entirely (see buildOfflineOrder).
  useEffect(() => {
    if (!branchId) return;
    let live = true;
    const key = "pos-payment-methods-" + branchId;
    setOfflineMethods(store.get<Method[]>(key) ?? []);
    if (online)
      void (async () => {
        try {
          const response = await auth(
            `/api/v1/payment-methods?branchId=${branchId}`,
          );
          if (!response.ok) return;
          const value = (await response.json()) as Method[];
          if (live) {
            setOfflineMethods(value);
            store.set(key, value);
          }
        } catch {
          /* The branch-specific cache remains available offline. */
        }
      })();
    return () => {
      live = false;
    };
  }, [branchId, online]);
  useEffect(() => {
    store.set("pos-cart", cart);
  }, [cart]);
  async function loadHeld() {
    if (!branchId || !online) return;
    try {
      const response = await auth(
        `/api/v1/orders?branchId=${branchId}&scope=shift`,
      );
      if (handleAuthFailure(response)) return;
      // Every order of the shift, whatever its kitchen status — a paid order is in the kitchen right away,
      // and a pay-later order is unpaid while it is there. Tabs in the screen narrow the list instead.
      if (response.ok) setHeldOrders((await response.json()) as HeldOrder[]);
    } catch {
      setMessage(t.unavailable);
    }
  }
  // Local calendar day (not UTC) so the total lines up with what the cashier calls "today" — orders/history
  // defaults to a UTC day, which would flip at 4am in Oman rather than midnight.
  async function loadSalesSummary() {
    if (!branchId || !online) return;
    try {
      const startOfDay = new Date();
      startOfDay.setHours(0, 0, 0, 0);
      const endOfDay = new Date(startOfDay.getTime() + 24 * 60 * 60 * 1000);
      const params = new URLSearchParams({
        branchId,
        paid: "true",
        pageSize: "200",
        from: startOfDay.toISOString(),
        to: endOfDay.toISOString(),
      });
      const response = await auth(`/api/v1/orders/history?${params}`);
      if (handleAuthFailure(response)) return;
      if (!response.ok) return;
      const data = (await response.json()) as {
        total: number;
        grossTotal?: number;
        items: Array<{ grossAmount: number }>;
      };
      setSalesSummary({
        count: data.total,
        // The server's total covers every paid order of the day, not just the first page.
        gross:
          data.grossTotal ??
          data.items.reduce((sum, x) => sum + x.grossAmount, 0),
      });
    } catch {
      /* Daily sales is a convenience widget; a failed refresh just leaves the last known total. */
    }
  }
  // QR orders awaiting staff approval stay Order.Status "Pending" until reviewed (auto-approved ones jump
  // straight to Confirmed), so filtering the branch's QR orders by that status is exactly the approval queue.
  async function loadQrPending() {
    if (!branchId || !online) return;
    try {
      const response = await auth(
        `/api/v1/qr/orders?branchId=${branchId}&status=Pending`,
      );
      if (handleAuthFailure(response)) return;
      if (!response.ok) return;
      setQrPending((await response.json()) as QrPendingOrder[]);
    } catch {
      /* The approval queue is best-effort here; the QR admin screen remains the source of truth. */
    }
  }
  async function loadShift() {
    if (!branchId || !online) return;
    try {
      const response = await auth(
        `/api/v1/shifts/current?branchId=${branchId}`,
      );
      if (!response.ok) return;
      const body = (await response.json()) as {
        shift: { openedAt: string } | null;
      };
      setShiftOpenedAt(body.shift?.openedAt ?? null);
    } catch {
      /* The header simply keeps its last known shift state. */
    }
  }
  async function refreshOrders() {
    await Promise.all([
      loadHeld(),
      loadSalesSummary(),
      loadQrPending(),
      loadShift(),
    ]);
  }
  useEffect(() => {
    void refreshOrders();
  }, [branchId, online]);
  async function reviewQr(approvalId: string, decision: "approve" | "reject") {
    if (qrReviewing) return;
    setQrReviewing(approvalId);
    try {
      const response = await auth(`/api/v1/qr/approvals/${approvalId}/review`, {
        method: "POST",
        body: JSON.stringify({ decision }),
      });
      if (handleAuthFailure(response)) return;
      if (!response.ok) {
        setMessage(t.unavailable);
        return;
      }
      await refreshOrders();
    } catch {
      setMessage(t.unavailable);
    } finally {
      setQrReviewing(null);
    }
  }
  async function openOrderDetail(id: string) {
    setDetailOrderId(id);
    setOrderDetail(null);
    try {
      const response = await auth(`/api/v1/orders/${id}`);
      if (handleAuthFailure(response)) return;
      if (!response.ok) {
        setMessage(t.unavailable);
        setDetailOrderId(null);
        return;
      }
      setOrderDetail((await response.json()) as OrderDetail);
    } catch {
      setMessage(t.unavailable);
      setDetailOrderId(null);
    }
  }
  // Realtime QR-order alerts for the cashier. SignalR is transport only — events never carry
  // authoritative state; they just refresh the REST-backed lists and raise a notification (SPA, no reload).
  function qrNotify(text: string) {
    const id = ++qrToastId.current;
    setQrToasts((prev) => [...prev, { id, text }]);
    window.setTimeout(
      () => setQrToasts((prev) => prev.filter((x) => x.id !== id)),
      3000,
    );
  }
  function onQrOrderReceived(payload: QrOrderReceivedEvent) {
    if (!branchId || payload.branchId !== branchId) return;
    const who = payload.approvalStatus === "Pending" ? t.qrPending : t.qrNew;
    qrNotify(
      [
        who,
        payload.contextCode,
        `OMR ${Number(payload.grossAmount).toFixed(3)}`,
      ]
        .filter(Boolean)
        .join(" · "),
    );
    void refreshOrders();
  }
  function onQrOrderReviewed(payload: QrOrderReviewedEvent) {
    if (!branchId || payload.branchId !== branchId) return;
    qrNotify(
      payload.approvalStatus === "Approved" ? t.qrApproved : t.qrRejected,
    );
    void refreshOrders();
  }
  useQrOrdersLive(
    branchId && online ? branchId : null,
    onQrOrderReceived,
    onQrOrderReviewed,
    () => {
      void refreshOrders();
    },
  );
  const qrToastsNode = qrToasts.length > 0 && (
    <div className="pointer-events-none fixed inset-x-0 top-4 z-[60] flex flex-col items-center gap-2 px-4">
      {qrToasts.map((toast) => (
        <div
          key={toast.id}
          role="status"
          className="pointer-events-auto flex w-full max-w-md items-start justify-between gap-3 rounded-xl border border-success/40 bg-success/15 px-4 py-3 text-sm font-medium text-primary shadow-lg"
        >
          <span>{toast.text}</span>
          <Button
            onClick={() =>
              setQrToasts((prev) => prev.filter((x) => x.id !== toast.id))
            }
            className="shrink-0 rounded-lg p-1 opacity-70 hover:opacity-100"
          >
            ×
          </Button>
        </div>
      ))}
    </div>
  );
  // Rendered inline (not a toast) since it's actionable, not just informational — a cashier can approve
  // or reject right here instead of navigating to the separate QR admin screen.
  const pendingQr = qrPending.filter((o) => o.approval?.status === "Pending");
  const qrPendingBanner = pendingQr.length > 0 && (
    <div className="border-b border-warning/40 bg-warning/15 px-3 py-3 sm:px-4">
      <p className="text-xs font-semibold text-warning">
        {t.qrReviewTitle} ({pendingQr.length})
      </p>
      <ul className="mt-2 space-y-2">
        {pendingQr.map((o) => (
          <li
            key={o.id}
            className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-card p-2.5 text-sm shadow-sm"
          >
            <div className="min-w-0">
              <p className="font-medium">
                {orderLabel(o.number)} · OMR {o.grossAmount.toFixed(3)}
              </p>
              <p className="mt-0.5 truncate text-xs text-muted-foreground">
                {o.lines
                  .map(
                    (l) =>
                      `${l.quantity}× ${language === "ar" ? l.productNameAr : l.productNameEn}`,
                  )
                  .join("، ")}
              </p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button
                disabled={qrReviewing === o.approval!.id}
                onClick={() => void reviewQr(o.approval!.id, "approve")}
                className="min-h-12 rounded-lg bg-primary px-3 text-xs font-semibold text-primary-foreground disabled:opacity-60"
              >
                {t.qrApprove}
              </Button>
              <Button
                disabled={qrReviewing === o.approval!.id}
                onClick={() => void reviewQr(o.approval!.id, "reject")}
                className="min-h-12 rounded-lg border border-destructive px-3 text-xs font-semibold text-destructive disabled:opacity-60"
              >
                {t.qrReject}
              </Button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
  useEffect(() => {
    if (!kitchenWatch.length || !branchId || !online) return;
    const timer = setInterval(async () => {
      try {
        const response = await auth(`/api/v1/orders?branchId=${branchId}`);
        if (!response.ok) return;
        const list = (await response.json()) as Array<{
          id: string;
          status: string;
          number?: number | null;
        }>;
        const byId = new Map(list.map((order) => [order.id, order]));
        setKitchenWatch((watch) =>
          watch.flatMap((entry) => {
            const order = byId.get(entry.orderId);
            // Anything past SentToKitchen means a cook acknowledged it (or it was cancelled).
            if (!order || order.status !== "SentToKitchen") return [];
            return [
              {
                ...entry,
                number: order.number ?? entry.number,
                late: Date.now() - entry.sentAt >= 20_000,
              },
            ];
          }),
        );
      } catch {
        /* Best effort: the next tick retries. */
      }
    }, 5000);
    return () => clearInterval(timer);
  }, [kitchenWatch.length, branchId, online]);
  // Prints one or several orders as a single print job (one dialog, one slip per order).
  // Kitchen screen presence: warn once when the kitchen tablet drops, instead of per unacknowledged
  // order. Shown only if a screen was connected earlier (a branch without a tablet never sees it) and has
  // been gone for more than 20 seconds (a brief network blip is ignored).
  const [kdsPresence, setKdsPresence] = useState<{
    screens: number;
    lastSeenAt: string | null;
  } | null>(null);
  const [kdsDismissed, setKdsDismissed] = useState<string | null>(null);
  useEffect(() => {
    if (!branchId || !online) return;
    let cancelled = false;
    const check = async () => {
      try {
        const response = await auth(
          `/api/v1/kitchen/presence?branchId=${branchId}`,
        );
        if (response.ok && !cancelled) setKdsPresence(await response.json());
      } catch {
        /* Best effort; the next check retries. */
      }
    };
    void check();
    const timer = setInterval(() => void check(), 15_000);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [branchId, online]);
  // Keep the branch's receipt design on this device so offline sales still print with it.
  useEffect(() => {
    if (branchId && online) void getReceiptLayout(auth, branchId);
  }, [branchId, online]);
  const kdsOfflineSince =
    kdsPresence &&
    kdsPresence.screens === 0 &&
    kdsPresence.lastSeenAt &&
    kdsDismissed !== kdsPresence.lastSeenAt &&
    Date.now() - new Date(kdsPresence.lastSeenAt).getTime() > 20_000 &&
    Date.now() - new Date(kdsPresence.lastSeenAt).getTime() < 12 * 3_600_000
      ? kdsPresence.lastSeenAt
      : null;
  async function printForKitchen(orderIds: string | string[]) {
    const ids = Array.isArray(orderIds) ? orderIds : [orderIds];
    try {
      const responses = await Promise.all(
        ids.map((id) => auth(`/api/v1/orders/${id}`)),
      );
      if (responses.some((response) => handleAuthFailure(response))) return;
      if (responses.some((response) => !response.ok)) {
        setMessage(t.unavailable);
        return;
      }
      const details = (await Promise.all(
        responses.map((response) => response.json()),
      )) as OrderDetail[];
      const printed = printKitchenSlips(
        details.map((detail) => {
          const held = heldOrders.find((order) => order.id === detail.id);
          return {
            title: t.kitchenSlip,
            reference: orderLabel(detail.number),
            table: held?.table ? name(held.table) : null,
            createdAt: detail.createdAt,
            note: detail.note,
            language,
            lines: detail.lines.map((line) => ({
              quantity: line.quantity,
              name: language === "ar" ? line.productNameAr : line.productNameEn,
              choices: snapshotChoices(line.selectionsSnapshot, language),
              note: line.note,
            })),
          };
        }),
      );
      if (!printed) setMessage(t.unavailable);
      setKitchenWatch((watch) => watch.filter((x) => !ids.includes(x.orderId)));
    } catch {
      setMessage(t.unavailable);
    }
  }
  async function dispatchOrder(orderId: string) {
    const response = await auth("/api/v1/kitchen/tickets", {
      method: "POST",
      body: JSON.stringify({
        branchId,
        orderId,
        clientDispatchId: createId(),
        orderNumber: null,
        note: null,
        targetMinutes: null,
      }),
    });
    if (!response.ok)
      throw new Error(
        language === "ar"
          ? "الطلب محفوظ. تعذر إرساله للمطبخ؛ أعد الإرسال من الطلبات الحالية."
          : "Order saved. Kitchen dispatch failed; retry from Current orders.",
      );
    setKitchenWatch((watch) => [
      ...watch.filter((x) => x.orderId !== orderId),
      { orderId, number: null, sentAt: Date.now(), late: false },
    ]);
    setMessage(t.sentToKitchen);
  }
  async function startEdit(order: HeldOrder) {
    if (busy) return;
    if (!online) return setMessage(t.editOffline);
    if (cart.length) return setMessage(t.emptyCartFirst);
    setBusy(true);
    setMessage("");
    try {
      const response = await auth(`/api/v1/orders/${order.id}`);
      if (handleAuthFailure(response)) return;
      if (!response.ok) throw new Error(t.unavailable);
      const detail = (await response.json()) as OrderDetail;
      const byId = new Map(products.map((product) => [product.id, product]));
      const lines: CartLine[] = [];
      for (const line of detail.lines) {
        const product = byId.get(line.productId);
        if (!product) throw new Error(t.editUnavailable);
        lines.push({
          key: createId(),
          product,
          quantity: line.quantity,
          note: line.note ?? "",
          selections: snapshotSelections(line.selectionsSnapshot),
        });
      }
      if (detail.salesChannelId) setChannelId(detail.salesChannelId);
      clearDiscount();
      setCart(lines);
      setEditing({
        orderId: order.id,
        number: order.number ?? null,
        status: order.status,
      });
      setAddOnFor(null);
      closeHeldOrders();
      if ((detail.manualDiscountAmount ?? 0) > 0)
        setMessage(t.editDiscountDropped);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t.unavailable);
    } finally {
      setBusy(false);
    }
  }
  function startAddOn(order: HeldOrder) {
    if (cart.length) return setMessage(t.emptyCartFirst);
    if (order.salesChannelId) setChannelId(order.salesChannelId);
    setEditing(null);
    setAddOnFor(order.number ?? null);
    closeHeldOrders();
  }
  async function resumeHeld(order: HeldOrder, kitchen = false) {
    if (busy) return;
    setBusy(true);
    setMessage("");
    try {
      if (order.status === "Draft") {
        const response = await auth("/api/v1/orders/" + order.id + "/status", {
          method: "POST",
          body: JSON.stringify({ status: "Pending", note: null }),
        });
        if (handleAuthFailure(response)) return;
        if (!response.ok) throw new Error(t.unavailable);
      }
      if (kitchen) await dispatchOrder(order.id);
      else {
        setHeldOpen(false);
        setPayment({ orderId: order.id, total: order.grossAmount });
      }
      await refreshOrders();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t.unavailable);
    } finally {
      setBusy(false);
    }
  }
  const categories = useMemo(
    () =>
      Array.from(
        new Map(
          products.map((p) => [
            p.categoryId,
            {
              id: p.categoryId,
              nameAr: p.categoryNameAr,
              nameEn: p.categoryNameEn,
            },
          ]),
        ).values(),
      ),
    [products],
  );
  const productIndex = useMemo(() => {
    const index = new Map<string, Product>();
    for (const product of products) {
      index.set(product.sku, product);
      if (product.barcode) index.set(product.barcode, product);
    }
    return index;
  }, [products]);
  const activeChannel = context?.channels.find(
    (channel) => channel.id === channelId,
  );
  const dineInChannel =
    context?.channels.find(
      (channel) => salesChannelKind(channel) === "DineIn",
    ) ??
    context?.channels.find(
      (channel) => salesChannelKind(channel) === "InStore",
    );
  const takeawayChannel = context?.channels.find(
    (channel) => salesChannelKind(channel) === "Takeaway",
  );
  const externalChannels =
    context?.channels.filter(
      (channel) => salesChannelKind(channel) === "Electronic",
    ) ?? [];
  const isExternallyPaidChannel =
    !!activeChannel && salesChannelKind(activeChannel) === "Electronic";
  useEffect(() => {
    if (cart.length === 0 || isExternallyPaidChannel) setPayStep(false);
  }, [cart.length, isExternallyPaidChannel]);
  const visible = useMemo(() => {
    const query = search.trim().toLowerCase();
    return products.filter(
      (p) =>
        (!category || p.categoryId === category) &&
        [p.nameAr, p.nameEn, p.sku, p.barcode ?? ""]
          .join(" ")
          .toLowerCase()
          .includes(query),
    );
  }, [products, category, search]);
  const amounts = useMemo(
    () =>
      cart.reduce(
        (sum, line) => {
          const price = resolveOfflinePricing(
            line.product.pricing,
            lineAdjustment(line),
          );
          sum.gross += price.gross * line.quantity;
          sum.net += price.net * line.quantity;
          sum.quantity += line.quantity;
          return sum;
        },
        { gross: 0, net: 0, quantity: 0 },
      ),
    [cart],
  );
  const total = roundMoney(amounts.gross);
  // The POS offers exactly three tenders (cash, card, cash + card), whatever else the branch has configured.
  const payCashMethod = offlineMethods.find((m) => m.kind === "Cash");
  const payCardMethod =
    offlineMethods.find((m) => m.kind === "Card") ??
    offlineMethods.find((m) => m.kind !== "Cash" && m.kind !== "External");
  const selectableOfflineMethods = isExternallyPaidChannel
    ? offlineMethods.filter((method) => method.kind === "External")
    : [payCashMethod, payCardMethod].filter((m): m is Method => !!m);
  // Mirrors backend OrderRules.ManualDiscountMaxPercent — this is a display estimate only, the server
  // is the authority and re-validates the cap independently when the order is created.
  const discountMaxPercent = 20;
  const discountRawValue = discountValue === "" ? 0 : Number(discountValue);
  const discountRequestedAmount = roundMoney(
    discountType === "Percentage"
      ? (total * discountRawValue) / 100
      : discountRawValue,
  );
  const discountMaxAmount = roundMoney((total * discountMaxPercent) / 100);
  const discountEstimate =
    discountOpen && discountRawValue > 0
      ? Math.min(discountRequestedAmount, total)
      : 0;
  // The server enforces the cap independently and rejects the order outright rather than silently
  // shrinking the discount, so the button stays disabled until the cashier lowers the value themselves.
  const discountExceedsMax =
    discountOpen &&
    discountRawValue > 0 &&
    discountRequestedAmount > discountMaxAmount;
  const payableTotal = roundMoney(Math.max(0, total - discountEstimate));
  // Mirrors OrderingEngine's proportional invoice discount; server remains authoritative.
  const netTotal = roundMoney(
    roundMoney(amounts.net) * (total ? payableTotal / total : 0),
  );
  const taxTotal = roundMoney(payableTotal - netTotal);
  // Customer display (SRS §57): mirror the cart; a newly opened display asks for the current state.
  const displaySnapshot: DisplayMessage = cart.length
    ? {
        kind: "cart",
        language,
        lines: cart.map((line) => {
          const unit = resolveOfflinePricing(
            line.product.pricing,
            lineAdjustment(line),
          ).gross;
          return {
            key: line.key,
            name: language === "ar" ? line.product.nameAr : line.product.nameEn,
            quantity: line.quantity,
            unit,
            amount: roundMoney(unit * line.quantity),
            details: [
              ...lineChoiceNames(line, language),
              ...(line.note ? [line.note] : []),
            ],
          };
        }),
        subtotal: total,
        discount: discountEstimate,
        tax: taxTotal,
        total: payableTotal,
      }
    : { kind: "idle", language };
  const displayState = useRef(displaySnapshot);
  displayState.current = displaySnapshot;
  const display = useRef<ReturnType<typeof openDisplayChannel>>(null);
  useEffect(() => {
    display.current = openDisplayChannel((message) => {
      if (message.kind === "hello") display.current?.post(displayState.current);
    });
    return () => display.current?.close();
  }, []);
  const displayKey = JSON.stringify(displaySnapshot);
  useEffect(() => {
    display.current?.post(displayState.current);
  }, [displayKey]);
  function announcePaid(amount: number) {
    display.current?.post({ kind: "paid", language, total: amount });
  }
  // Falls back to whichever tender the branch actually has configured, so a branch with only one of
  // cash/card (payMethod still defaulting to "Cash") doesn't silently build a payment for a tender
  // that isn't available there.
  const effectivePayMethod: PaymentMethod =
    payMethod === "Cash" && !payCashMethod && payCardMethod
      ? "Card"
      : payMethod === "Card" && !payCardMethod && payCashMethod
        ? "Cash"
        : payMethod;
  const payCashAmount =
    effectivePayMethod === "Cash"
      ? payableTotal
      : effectivePayMethod === "Card"
        ? 0
        : roundMoney(payCash === "" ? 0 : Number(payCash));
  const payCardAmount =
    effectivePayMethod === "Card"
      ? payableTotal
      : effectivePayMethod === "Cash"
        ? 0
        : roundMoney(payableTotal - payCashAmount);
  const payValid =
    Number.isFinite(payCashAmount) &&
    payCashAmount >= 0 &&
    payCardAmount >= 0 &&
    (effectivePayMethod !== "Mixed" ||
      (payCashAmount > 0 && payCardAmount > 0));
  function choosePayMethod(next: PaymentMethod) {
    setPayMethod(next);
    setPayCash("");
    setPayCard("");
    setPayMessage("");
  }
  function setPayCashAmount(raw: string) {
    setPayMessage("");
    const normalized = normalizeMoneyInput(raw);
    if (normalized === null) return;
    setPayCash(normalized);
    const value = normalized === "" ? 0 : Number(normalized);
    if (value > payableTotal) {
      setPayCard("");
      setPayMessage(t.payExceed);
      return;
    }
    setPayCard(
      normalized === ""
        ? payableTotal.toFixed(3)
        : (payableTotal - value).toFixed(3),
    );
  }
  function setPayCardAmount(raw: string) {
    setPayMessage("");
    const normalized = normalizeMoneyInput(raw);
    if (normalized === null) return;
    setPayCard(normalized);
    const value = normalized === "" ? 0 : Number(normalized);
    if (value > payableTotal) {
      setPayCash("");
      setPayMessage(t.payExceed);
      return;
    }
    setPayCash(
      normalized === ""
        ? payableTotal.toFixed(3)
        : (payableTotal - value).toFixed(3),
    );
  }
  function applyDiscount() {
    setPayCash("");
    setPayCard("");
    setPayMessage("");
  }
  function clearOrder() {
    setCart([]);
    setEditing(null);
    setAddOnFor(null);
    clearDiscount();
    requestRef.current = { snapshot: "", id: "" };
    setPayStep(false);
    setConfirmClear(false);
  }
  function clearDiscount() {
    setDiscountOpen(false);
    setDiscountValue("");
    setPayCash("");
    setPayCard("");
    setPayMessage("");
  }
  const add = useCallback((product: Product) => {
    if (!product.selectionGroups.length) {
      setCart((current) => addToCart(current, product, {}));
      return;
    }
    setCustomizing(product);
    setSelections(
      Object.fromEntries(
        product.selectionGroups.map((group) => [
          group.id,
          group.options
            .filter(
              (option) => option.isDefault && option.isAvailable !== false,
            )
            .map((option) => option.id),
        ]),
      ),
    );
  }, []);
  useBarcodeScanner(
    (code) => {
      const product = productIndex.get(code);
      if (!product) return false;
      add(product);
      return true;
    },
    !busy &&
      !catalogLoading &&
      !customizing &&
      !heldOpen &&
      !payment &&
      !offlinePay,
  );
  useEffect(() => {
    const focusSearch = (event: KeyboardEvent) => {
      if (
        event.key !== "F2" ||
        event.ctrlKey ||
        event.altKey ||
        event.metaKey ||
        document.querySelector('[role="dialog"]')
      )
        return;
      event.preventDefault();
      searchRef.current?.focus();
    };
    window.addEventListener("keydown", focusSearch);
    return () => window.removeEventListener("keydown", focusSearch);
  }, []);
  function confirm() {
    if (!customizing) return;
    if (
      customizing.selectionGroups.some((group) => {
        const count = selections[group.id]?.length ?? 0;
        return count < group.minSelections || count > group.maxSelections;
      })
    )
      return;
    const chosen = customizing;
    setCart((current) => addToCart(current, chosen, selections));
    setCustomizing(null);
  }
  const quantity = useCallback((key: string, delta: number) => {
    setCart((current) =>
      current.flatMap((line) =>
        line.key !== key
          ? [line]
          : line.quantity + delta < 1
            ? []
            : [{ ...line, quantity: line.quantity + delta }],
      ),
    );
  }, []);
  const updateNote = useCallback((key: string, note: string) => {
    setCart((current) =>
      current.map((line) => (line.key === key ? { ...line, note } : line)),
    );
  }, []);
  // Every order the cashier places is sent to the kitchen immediately — the cashier is trusted
  // staff, so there is no separate manual "send to kitchen" confirmation step for a normal order.
  // Hold saves a draft; Pay captures the selected inline tender before kitchen dispatch.
  // payLater: the order goes to the kitchen now and is paid afterwards from Current orders (e.g. a VIP guest).
  async function submit(status: "Draft" | "Pending", payLater = false) {
    if (!cart.length || !branchId || !channelId || busy) return;
    if (!online && payLater) {
      setMessage(t.payLaterOffline);
      return;
    }
    if (!online && editing) {
      setMessage(t.editOffline);
      return;
    }
    if (!online) {
      persistBranch(branchId);
      if (status === "Draft") {
        // Only clear the cart once the sale is really stored on this device (busy blocks a double tap).
        setBusy(true);
        try {
          await enqueue({
            operationType: "order.create",
            baseVersion: null,
            baseCatalogVersion: null,
            occurredAt: new Date().toISOString(),
            payload: buildOfflineOrder(channelId, cart, "Draft"),
          });
        } catch {
          setMessage(t.offlineSaveFailed);
          return;
        } finally {
          setBusy(false);
        }
        setCart([]);
        setMessage(t.offline);
        return;
      }
      setOfflinePay({
        methodId: selectableOfflineMethods[0]?.id ?? "",
        tendered: total.toFixed(3),
      });
      return;
    }
    if (discountExceedsMax) {
      setMessage(t.discountTooHigh);
      return;
    }
    if (status === "Pending" && !isExternallyPaidChannel && !payLater) {
      if (offlineMethods.length === 0) {
        setMessage(t.payNoMethods);
        return;
      }
      if (!payValid) {
        setMessage(payMessage || t.payInvalid);
        return;
      }
    }
    setBusy(true);
    setMessage("");
    try {
      const snapshot = JSON.stringify({ branchId, channelId, cart });
      if (requestRef.current.snapshot !== snapshot)
        requestRef.current = { snapshot, id: createId() };
      const body = {
        branchId,
        salesChannelId: channelId,
        clientRequestId: requestRef.current.id,
        source: "Pos",
        note: addOnFor ? `${t.addOnFor} #${addOnFor}` : null,
        discount:
          canDiscount && discountOpen && discountRawValue > 0
            ? { type: discountType, value: discountRawValue }
            : null,
        lines: cart.map((line) => ({
          productId: line.product.id,
          quantity: line.quantity,
          note: line.note || null,
          selections: Object.entries(line.selections).map(
            ([selectionGroupId, ids]) => ({
              selectionGroupId,
              choices: ids.map((optionId) => ({ optionId, quantity: 1 })),
            }),
          ),
        })),
      };
      const response = editing
        ? await auth(`/api/v1/orders/${editing.orderId}/lines`, {
            method: "PUT",
            body: JSON.stringify({
              lines: body.lines,
              discount: body.discount,
            }),
          })
        : await auth("/api/v1/orders", {
            method: "POST",
            body: JSON.stringify(body),
          });
      if (handleAuthFailure(response)) return;
      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        throw new Error(
          problem?.errors?.discount?.[0] ??
            problem?.errors?.order?.[0] ??
            t.unavailable,
        );
      }
      const order = (await response.json()) as {
        id: string;
        grossAmount: number;
      };
      const alreadyPending = editing?.status === "Pending";
      setCart([]);
      setEditing(null);
      setAddOnFor(null);
      requestRef.current = { snapshot: "", id: "" };
      if (status === "Draft") {
        setDiscountOpen(false);
        setDiscountValue("");
        setMessage(t.saved);
        return;
      }
      const changed = alreadyPending
        ? null
        : await auth("/api/v1/orders/" + order.id + "/status", {
            method: "POST",
            body: JSON.stringify({ status: "Pending", note: null }),
          });
      if (changed && handleAuthFailure(changed)) return;
      if (changed && !changed.ok)
        throw new Error(
          language === "ar"
            ? "تم حفظ الطلب. أكمل من الطلبات الحالية."
            : "Order saved. Continue from Current orders.",
        );
      if (payLater) {
        await dispatchOrder(order.id);
        setDiscountOpen(false);
        setDiscountValue("");
        setMessage(t.sentPayLater);
        return;
      }
      if (isExternallyPaidChannel) {
        const externalMethod = offlineMethods.find(
          (method) =>
            method.kind === "External" ||
            method.code?.toUpperCase() === "EXTERNAL",
        );
        if (!externalMethod)
          throw new Error(
            language === "ar"
              ? "لا توجد وسيلة دفع خارجي مفعلة لهذا الفرع."
              : "No external payment method is configured for this branch.",
          );
        const paid = await auth(`/api/v1/orders/${order.id}/payments`, {
          method: "POST",
          body: JSON.stringify({
            payments: [
              {
                clientRequestId: createId(),
                paymentMethodId: externalMethod.id,
                amount: order.grossAmount,
                tenderedAmount: order.grossAmount,
                status: "Captured",
                providerReference: `${activeChannel?.code ?? "EXTERNAL"}-${createId()}`,
              },
            ],
          }),
        });
        if (!paid.ok)
          throw new Error(
            language === "ar"
              ? "تم حفظ الطلب، لكن تعذر تسجيل الدفع الخارجي."
              : "Order saved, but its external payment could not be recorded.",
          );
        announcePaid(order.grossAmount);
        void printOrderReceipt(auth, branchId, order.id, language);
        await dispatchOrder(order.id);
        setDiscountOpen(false);
        setDiscountValue("");
        return;
      }
      // Re-based on the order the server actually created (order.grossAmount), never on the client's
      // pre-submit estimate: the server independently validates and rounds the discount, so this is the
      // only amount guaranteed to match what the cashier is actually collecting.
      const finalCashAmount =
        effectivePayMethod === "Cash"
          ? order.grossAmount
          : effectivePayMethod === "Card"
            ? 0
            : roundMoney(
                order.grossAmount *
                  (payableTotal > 0 ? payCashAmount / payableTotal : 0),
              );
      const finalCardAmount = roundMoney(order.grossAmount - finalCashAmount);
      const payments: Array<{
        clientRequestId: string;
        paymentMethodId: string;
        amount: number;
        tenderedAmount: number;
        status: string;
        providerReference: string | null;
      }> = [];
      if (finalCashAmount > 0) {
        if (!payCashMethod) throw new Error(t.payNoCash);
        payments.push({
          clientRequestId: createId(),
          paymentMethodId: payCashMethod.id,
          amount: finalCashAmount,
          tenderedAmount: finalCashAmount,
          status: "Captured",
          providerReference: null,
        });
      }
      if (finalCardAmount > 0) {
        if (!payCardMethod) throw new Error(t.payNoCard);
        payments.push({
          clientRequestId: createId(),
          paymentMethodId: payCardMethod.id,
          amount: finalCardAmount,
          tenderedAmount: finalCardAmount,
          status: "Captured",
          providerReference: `POS-${createId()}`,
        });
      }
      const paid = await auth(`/api/v1/orders/${order.id}/payments`, {
        method: "POST",
        body: JSON.stringify({ payments }),
      });
      if (!paid.ok) {
        const problem = await paid.json().catch(() => null);
        throw new Error(problem?.errors?.payments?.[0] ?? t.payFailed);
      }
      setCartOpen(false);
      announcePaid(order.grossAmount);
      void printOrderReceipt(auth, branchId, order.id, language);
      await dispatchOrder(order.id);
      setPayCash("");
      setPayCard("");
      setDiscountOpen(false);
      setDiscountValue("");
      setMessage(t.paidAndSent);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t.unavailable);
    } finally {
      setBusy(false);
      void refreshOrders();
    }
  }
  async function confirmOfflinePayment() {
    if (!offlinePay || busy) return;
    const method = offlineMethods.find((m) => m.id === offlinePay.methodId);
    if (!method) return;
    const isCash = method.kind === "Cash";
    const tendered = Number(offlinePay.tendered) || 0;
    if (
      (isCash && tendered < total) ||
      (!isCash && Math.abs(tendered - total) > 0.0001)
    ) {
      setMessage(t.offlinePayInvalid);
      return;
    }
    persistBranch(branchId);
    // Busy while storing, so a double tap cannot queue the same sale twice.
    setBusy(true);
    try {
      await enqueue({
        operationType: "order.create",
        baseVersion: null,
        baseCatalogVersion: null,
        occurredAt: new Date().toISOString(),
        payload: buildOfflineOrder(channelId, cart, "Paid", {
          clientRequestId: createId(),
          paymentMethodId: method.id,
          amount: total,
          tenderedAmount: tendered,
        }),
      });
    } catch {
      // The sale is not stored: keep the cart and the payment dialog so nothing is lost.
      setMessage(t.offlineSaveFailed);
      return;
    } finally {
      setBusy(false);
    }
    announcePaid(total);
    // No order number exists until the sale syncs, so the receipt is built from the cart itself.
    const net = roundMoney(amounts.net);
    printReceipt(storedReceiptLayout(branchId), {
      language,
      reference: "—",
      createdAt: new Date().toISOString(),
      lines: cart.map((line) => ({
        quantity: line.quantity,
        name: language === "ar" ? line.product.nameAr : line.product.nameEn,
        choices: lineChoiceNames(line, language),
        total: roundMoney(
          resolveOfflinePricing(line.product.pricing, lineAdjustment(line))
            .gross * line.quantity,
        ),
      })),
      discountAmount: 0,
      netAmount: net,
      taxAmount: roundMoney(total - net),
      grossAmount: total,
      payments: [
        {
          name: paymentMethodName(language, method),
          amount: total,
          change: isCash ? roundMoney(tendered - total) : 0,
        },
      ],
    });
    setCart([]);
    setOfflinePay(null);
    setMessage(t.offline);
  }
  const statusLabels = {
    Draft: language === "ar" ? "معلّق" : "Held",
    Pending: language === "ar" ? "بانتظار الدفع" : "Unpaid",
    Confirmed: language === "ar" ? "مؤكد" : "Confirmed",
    Paid: language === "ar" ? "مدفوع" : "Paid",
  } as Record<string, string>;
  // Payment is separate from the kitchen status: a pay-later order can be in the kitchen and still unpaid.
  const isPaid = (order: HeldOrder) =>
    !!order.paidAt || order.status === "Paid";
  const canPay = (order: HeldOrder) =>
    !isPaid(order) && payableStatuses.includes(order.status);
  // Where the order is in the kitchen, independent of whether it has been paid. rank orders the stages.
  type StageGroup =
    "held" | "notSent" | "kitchen" | "ready" | "done" | "cancelled";
  const orderStage = (
    order: HeldOrder,
  ): { label: string; group: StageGroup; tone: string; rank: number } => {
    switch (order.status) {
      case "Draft":
        return { label: t.stageHeld, group: "held", tone: "bg-muted", rank: 0 };
      case "SentToKitchen":
      case "Preparing":
        return {
          label: t.filterKitchen,
          group: "kitchen",
          tone: "bg-warning/15 text-warning",
          rank: 2,
        };
      case "Ready":
        return {
          label: t.filterReady,
          group: "ready",
          tone: "bg-success/15 text-success",
          rank: 3,
        };
      case "Completed":
        return {
          label: t.filterDone,
          group: "done",
          tone: "bg-muted",
          rank: 4,
        };
      case "Refunded":
      case "PartiallyRefunded":
        return {
          label: t.stageRefunded,
          group: "done",
          tone: "bg-muted",
          rank: 5,
        };
      case "Cancelled":
      case "Rejected":
        return {
          label: t.filterCancelled,
          group: "cancelled",
          tone: "bg-destructive/15 text-destructive",
          rank: 6,
        };
      default:
        return {
          label: t.stageNotSent,
          group: "notSent",
          tone: "bg-muted",
          rank: 1,
        };
    }
  };
  // "Current" = still needs the cashier: unpaid, or not yet finished in the kitchen.
  const isCurrent = (order: HeldOrder) =>
    canPay(order) || !["done", "cancelled"].includes(orderStage(order).group);
  const inHeldFilter = (order: HeldOrder, filter: typeof heldFilter) =>
    filter === "all"
      ? true
      : filter === "current"
        ? isCurrent(order)
        : filter === "unpaid"
          ? canPay(order)
          : orderStage(order).group === filter;
  const orderChannel = (order: HeldOrder) =>
    context?.channels.find((channel) => channel.id === order.salesChannelId);
  // Same colours as the kitchen screen: green dine-in, orange takeaway, black delivery apps.
  const channelTone = (channel: SalesChannel | undefined) => {
    const kind = channel ? salesChannelKind(channel) : null;
    return kind === "Takeaway"
      ? "bg-warning text-warning-foreground"
      : kind === "DineIn" || kind === "InStore"
        ? "bg-success text-success-foreground"
        : kind === "Electronic"
          ? "bg-foreground text-background"
          : "bg-primary text-primary-foreground";
  };
  const openOrderCount = heldOrders.filter(isCurrent).length;
  const unpaidOrders = heldOrders.filter(canPay);
  const unpaidTotal = unpaidOrders.reduce(
    (sum, order) => sum + order.grossAmount,
    0,
  );
  // Not yet in the kitchen: the cashier can still send it there.
  const canSendToKitchen = (order: HeldOrder) =>
    ["Draft", "Pending", "Confirmed", "Paid"].includes(order.status);
  function closeHeldOrders() {
    setHeldOpen(false);
    setDetailOrderId(null);
    setOrderDetail(null);
    setHeldSearch("");
  }
  const sortValue = (
    order: HeldOrder,
    key: typeof heldSort.key,
  ): number | string => {
    switch (key) {
      case "number":
        return order.number ?? 0;
      case "type":
        return orderChannel(order) ? name(orderChannel(order)!) : "";
      case "stage":
        return orderStage(order).rank;
      case "paid":
        return canPay(order) ? 0 : 1;
      case "amount":
        return order.grossAmount;
      default:
        return new Date(order.createdAt).getTime();
    }
  };
  const matchingHeldOrders = heldOrders
    .filter((order) => {
      if (!inHeldFilter(order, heldFilter)) return false;
      const query = heldSearch.trim().toLowerCase();
      if (!query) return true;
      const table = order.table
        ? `${order.table.code} ${order.table.nameAr} ${order.table.nameEn}`
        : "";
      const searchable = [
        order.number ? `#${order.number} ${order.number}` : "",
        table,
        order.note ?? "",
        order.status,
        orderStage(order).label,
        canPay(order) ? t.unpaidBadge : isPaid(order) ? t.paidBadge : "",
        orderChannel(order) ? name(orderChannel(order)!) : "",
        order.grossAmount.toFixed(3),
      ]
        .join(" ")
        .toLowerCase();
      return searchable.includes(query);
    })
    .sort((x, y) => {
      const a = sortValue(x, heldSort.key);
      const b = sortValue(y, heldSort.key);
      const order =
        typeof a === "string" || typeof b === "string"
          ? String(a).localeCompare(String(b), language)
          : a - b;
      return heldSort.dir === "asc" ? order : -order;
    });
  function sortHeldBy(key: typeof heldSort.key) {
    setHeldSort((current) =>
      current.key === key
        ? { key, dir: current.dir === "asc" ? "desc" : "asc" }
        : { key, dir: key === "time" || key === "amount" ? "desc" : "asc" },
    );
  }
  const orderDetailView = detailOrderId && (
    <>
      <div className="sticky top-0 z-20 -mx-3 -mt-3 flex items-center gap-2 border-b border-border bg-card/95 px-3 py-3 backdrop-blur sm:-mx-5 sm:-mt-5 sm:px-5">
        <Button
          onClick={() => {
            setDetailOrderId(null);
            setOrderDetail(null);
          }}
          className="grid size-9 place-items-center rounded-lg bg-muted"
        >
          {language === "ar" ? "→" : "←"}
        </Button>
        <h2 className="text-lg font-bold">
          {t.orderRef} {orderLabel(orderDetail?.number)}
        </h2>
      </div>
      {!orderDetail ? (
        <p
          role="status"
          className="mt-6 text-center text-sm text-muted-foreground"
        >
          {t.loadingOrder}
        </p>
      ) : (
        <div className="mt-4 space-y-4">
          <p className="text-sm text-muted-foreground">
            {statusLabels[orderDetail.status] ?? orderDetail.status} ·{" "}
            {new Date(orderDetail.createdAt).toLocaleString(language)}
            {orderDetail.note ? ` · ${orderDetail.note}` : ""}
          </p>
          <div>
            <p className="text-sm font-semibold">{t.orderItems}</p>
            <ul className="mt-2 space-y-2">
              {orderDetail.lines.map((line) => (
                <li key={line.id} className="rounded-xl bg-muted p-3">
                  <div className="flex justify-between gap-3">
                    <span>
                      {line.quantity} ×{" "}
                      {language === "ar"
                        ? line.productNameAr
                        : line.productNameEn}
                    </span>
                    <span>
                      OMR {(line.unitGrossAmount * line.quantity).toFixed(3)}
                    </span>
                  </div>
                  {line.note && (
                    <p className="mt-1 text-xs text-muted-foreground">
                      {line.note}
                    </p>
                  )}
                </li>
              ))}
            </ul>
          </div>
          <div className="space-y-1 border-t border-border pt-3 text-sm">
            <div className="flex justify-between text-muted-foreground">
              <span>{t.orderNet}</span>
              <span>OMR {orderDetail.netAmount.toFixed(3)}</span>
            </div>
            <div className="flex justify-between text-muted-foreground">
              <span>{t.orderTax}</span>
              <span>OMR {orderDetail.taxAmount.toFixed(3)}</span>
            </div>
            <div className="flex justify-between text-lg font-bold">
              <span>{t.total}</span>
              <span>OMR {orderDetail.grossAmount.toFixed(3)}</span>
            </div>
          </div>
        </div>
      )}
    </>
  );
  const heldOrdersModal = heldOpen && (
    <div className="fixed inset-0 z-[100] grid min-w-0 place-items-end bg-black/35 sm:place-items-center sm:p-5">
      <section
        role="dialog"
        aria-modal="true"
        aria-label={t.heldOrders}
        className="flex max-h-[100dvh] min-w-0 w-full max-w-5xl flex-col overflow-hidden rounded-t-2xl border border-border bg-card shadow-2xl sm:max-h-[calc(100dvh-2.5rem)] sm:rounded-2xl"
      >
        {detailOrderId ? (
          <div className="min-w-0 overscroll-contain overflow-x-hidden overflow-y-auto p-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] sm:p-5">
            {orderDetailView}
          </div>
        ) : (
          <>
            <div className="sticky top-0 z-20 border-b border-border bg-card/95 px-3 py-3 backdrop-blur sm:px-5 sm:py-4">
              <div className="flex items-center justify-between gap-3">
                <h2 className="text-lg font-bold">
                  {t.heldOrders}
                  {heldOrders.length > 0 && (
                    <span className="ms-2 font-normal text-muted-foreground">
                      ({heldOrders.length})
                    </span>
                  )}
                  <span className="block text-xs font-normal text-muted-foreground">
                    {t.heldScope}
                  </span>
                </h2>
                <Button
                  onClick={closeHeldOrders}
                  aria-label={language === "ar" ? "إغلاق" : "Close"}
                  className="grid size-11 shrink-0 place-items-center rounded-xl bg-muted"
                >
                  ×
                </Button>
              </div>
              <label className="mt-3 flex min-h-12 items-center gap-2 rounded-xl border border-border bg-background px-3 focus-within:ring-2 focus-within:ring-ring">
                <Search
                  size={18}
                  className="shrink-0 text-muted-foreground"
                  aria-hidden="true"
                />
                <Input
                  type="search"
                  aria-label={t.heldSearch}
                  value={heldSearch}
                  onChange={(e) => setHeldSearch(e.target.value)}
                  placeholder={t.heldSearch}
                  autoComplete="off"
                  className="min-w-0 flex-1 border-0 bg-transparent px-0 text-sm shadow-none outline-none focus-visible:ring-0"
                />
              </label>
              <div className="mt-3 grid grid-cols-3 gap-2">
                {(
                  [
                    [
                      "unpaid",
                      t.unpaidSummary,
                      unpaidOrders.length,
                      `OMR ${unpaidTotal.toFixed(3)}`,
                      "border-warning bg-warning/10 text-warning",
                    ],
                    [
                      "current",
                      t.currentSummary,
                      openOrderCount,
                      "",
                      "border-primary bg-primary/10 text-primary",
                    ],
                    [
                      "ready",
                      t.readySummary,
                      heldOrders.filter((o) => orderStage(o).group === "ready")
                        .length,
                      "",
                      "border-success bg-success/10 text-success",
                    ],
                  ] as const
                ).map(([filter, label, count, extra, tone]) => (
                  <Button
                    key={filter}
                    type="button"
                    onClick={() => setHeldFilter(filter)}
                    className={`flex h-auto min-h-16 flex-col items-start justify-center gap-0.5 rounded-xl border-2 px-3 py-2 text-start ${tone} ${heldFilter === filter ? "ring-2 ring-current" : ""}`}
                  >
                    <span className="text-xs font-semibold">{label}</span>
                    <span className="text-xl font-black leading-none text-foreground">
                      {count}
                    </span>
                    {extra && (
                      <span
                        className="text-xs font-bold tabular-nums text-foreground"
                        dir="ltr"
                      >
                        {extra}
                      </span>
                    )}
                  </Button>
                ))}
              </div>
              <div
                role="tablist"
                aria-label={t.heldOrders}
                className="mt-3 flex gap-1.5 overflow-x-auto pb-0.5"
              >
                {(
                  [
                    ["current", t.filterCurrent],
                    ["unpaid", t.filterUnpaid],
                    ["ready", t.filterReady],
                    ["done", t.filterDone],
                    ["cancelled", t.filterCancelled],
                    ["all", t.filterAll],
                  ] as const
                ).map(([filter, label]) => {
                  const count = heldOrders.filter((order) =>
                    inHeldFilter(order, filter),
                  ).length;
                  const selected = heldFilter === filter;
                  return (
                    <Button
                      key={filter}
                      type="button"
                      role="tab"
                      aria-selected={selected}
                      onClick={() => setHeldFilter(filter)}
                      className={`inline-flex min-h-10 shrink-0 items-center gap-1.5 rounded-full px-3.5 text-sm font-semibold ${selected ? "bg-primary text-primary-foreground" : "border border-border bg-background"}`}
                    >
                      {label}
                      <span
                        className={`rounded-full px-1.5 text-xs ${selected ? "bg-black/20" : "bg-muted"}`}
                      >
                        {count}
                      </span>
                    </Button>
                  );
                })}
              </div>
            </div>
            <div className="min-h-0 flex-1 overflow-auto">
              {heldOrders.length === 0 ? (
                <p className="p-8 text-center text-sm text-muted-foreground">
                  {t.noHeld}
                </p>
              ) : matchingHeldOrders.length === 0 ? (
                <p className="p-8 text-center text-sm text-muted-foreground">
                  {t.heldNoMatch}
                </p>
              ) : (
                <Table className="w-full min-w-[860px] text-sm">
                  <TableHeader className="sticky top-0 z-10 bg-muted">
                    <TableRow>
                      {(
                        [
                          ["number", t.colOrder],
                          ["type", t.colType],
                          ["stage", t.colStage],
                          ["paid", t.colPayment],
                          ["time", t.colTime],
                          ["amount", t.colAmount],
                        ] as const
                      ).map(([key, label]) => {
                        const active = heldSort.key === key;
                        return (
                          <TableHead
                            key={key}
                            aria-sort={
                              active
                                ? heldSort.dir === "asc"
                                  ? "ascending"
                                  : "descending"
                                : "none"
                            }
                            className={`px-3 py-2 ${key === "amount" ? "text-end" : "text-start"}`}
                          >
                            <button
                              type="button"
                              onClick={() => sortHeldBy(key)}
                              className={`inline-flex min-h-9 items-center gap-1 font-bold ${active ? "text-primary" : "text-foreground"}`}
                            >
                              {label}
                              {active ? (
                                heldSort.dir === "asc" ? (
                                  <ChevronUp size={14} aria-hidden="true" />
                                ) : (
                                  <ChevronDown size={14} aria-hidden="true" />
                                )
                              ) : (
                                <ChevronsUpDown
                                  size={14}
                                  className="opacity-40"
                                  aria-hidden="true"
                                />
                              )}
                            </button>
                          </TableHead>
                        );
                      })}
                      <TableHead className="px-3 py-2 text-end font-bold">
                        {t.colActions}
                      </TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {matchingHeldOrders.map((order) => {
                      const channel = orderChannel(order);
                      const stage = orderStage(order);
                      const paid = isPaid(order);
                      const unpaid = canPay(order);
                      const minutes = Math.max(
                        0,
                        Math.floor(
                          (Date.now() - new Date(order.createdAt).getTime()) /
                            60_000,
                        ),
                      );
                      return (
                        <TableRow
                          key={order.id}
                          className={
                            unpaid
                              ? "bg-warning/5 shadow-[inset_4px_0_0_var(--warning)] rtl:shadow-[inset_-4px_0_0_var(--warning)]"
                              : stage.group === "cancelled"
                                ? "opacity-60"
                                : ""
                          }
                        >
                          <TableCell className="px-3 py-2">
                            <Button
                              type="button"
                              onClick={() => void openOrderDetail(order.id)}
                              className="min-h-10 px-0 text-base font-black text-primary hover:underline"
                            >
                              {orderLabel(order.number)}
                            </Button>
                            {(order.table || order.note) && (
                              <p
                                className="max-w-44 truncate text-xs text-muted-foreground"
                                title={order.note ?? undefined}
                              >
                                {[
                                  order.table ? name(order.table) : "",
                                  order.note ?? "",
                                ]
                                  .filter(Boolean)
                                  .join(" · ")}
                              </p>
                            )}
                          </TableCell>
                          <TableCell className="px-3 py-2">
                            {channel && (
                              <span
                                className={`rounded-full px-2.5 py-1 text-xs font-bold ${channelTone(channel)}`}
                              >
                                {name(channel)}
                              </span>
                            )}
                          </TableCell>
                          <TableCell className="px-3 py-2">
                            <span
                              className={`rounded-full px-2.5 py-1 text-xs font-semibold ${stage.tone}`}
                            >
                              {stage.label}
                            </span>
                          </TableCell>
                          <TableCell className="px-3 py-2">
                            {stage.group !== "cancelled" && (
                              <span
                                className={`rounded-full px-2.5 py-1 text-xs font-bold ${paid ? "bg-success/15 text-success" : "bg-warning text-warning-foreground"}`}
                              >
                                {paid ? t.paidBadge : t.unpaidBadge}
                              </span>
                            )}
                          </TableCell>
                          <TableCell className="whitespace-nowrap px-3 py-2">
                            <span className="font-semibold">
                              {new Date(order.createdAt).toLocaleTimeString(
                                language,
                                { hour: "2-digit", minute: "2-digit" },
                              )}
                            </span>
                            <span className="block text-xs text-muted-foreground">
                              {t.minutesAgo} {minutes} {t.minutesShort}
                            </span>
                          </TableCell>
                          <TableCell className="whitespace-nowrap px-3 py-2 text-end font-bold tabular-nums">
                            OMR {order.grossAmount.toFixed(3)}
                          </TableCell>
                          <TableCell className="px-3 py-2">
                            <div className="flex justify-end gap-1.5">
                              <Button
                                type="button"
                                disabled={busy}
                                onClick={() => void printForKitchen(order.id)}
                                title={t.printKitchen}
                                aria-label={t.printKitchen}
                                className="grid size-10 place-items-center rounded-lg border border-border"
                              >
                                <Printer size={16} />
                              </Button>
                              {canSendToKitchen(order) && (
                                <Button
                                  type="button"
                                  disabled={busy}
                                  onClick={() => void resumeHeld(order, true)}
                                  className="min-h-10 rounded-lg border border-border px-2.5 text-xs font-semibold"
                                >
                                  {t.sendKitchen}
                                </Button>
                              )}
                              {(order.status === "Draft" ||
                                order.status === "Pending") && (
                                <Button
                                  type="button"
                                  disabled={busy}
                                  onClick={() => void startEdit(order)}
                                  className="min-h-10 rounded-lg border border-border px-2.5 text-xs font-semibold"
                                >
                                  {t.editOrder}
                                </Button>
                              )}
                              {paid && stage.group !== "cancelled" && (
                                <Button
                                  type="button"
                                  disabled={busy}
                                  onClick={() => startAddOn(order)}
                                  className="min-h-10 rounded-lg border border-border px-2.5 text-xs font-semibold"
                                >
                                  {t.addOnOrder}
                                </Button>
                              )}
                              {unpaid && (
                                <Button
                                  type="button"
                                  disabled={busy}
                                  onClick={() => void resumeHeld(order)}
                                  className="min-h-10 rounded-lg bg-primary px-4 text-sm font-bold text-primary-foreground"
                                >
                                  {t.payNow}
                                </Button>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              )}
            </div>
          </>
        )}
      </section>
    </div>
  );
  const offlinePayModal = offlinePay && (
    <div className="fixed inset-0 z-50 grid min-w-0 place-items-end bg-black/35 sm:place-items-center sm:p-5">
      <section
        role="dialog"
        aria-modal="true"
        aria-label={t.offlinePayTitle}
        className="max-h-[100dvh] min-w-0 w-full max-w-md overscroll-contain overflow-x-hidden overflow-y-auto rounded-t-2xl bg-background p-4 pb-[max(1rem,env(safe-area-inset-bottom))] shadow-2xl sm:max-h-[calc(100dvh-2.5rem)] sm:rounded-2xl sm:p-6"
      >
        <h2 className="text-lg font-bold">{t.offlinePayTitle}</h2>
        <p className="mt-1 text-2xl font-bold text-primary">
          OMR {total.toFixed(3)}
        </p>
        {selectableOfflineMethods.length === 0 ? (
          <p
            role="alert"
            className="mt-5 rounded-xl bg-destructive/10 p-4 text-sm text-destructive"
          >
            {t.offlineNoMethods}
          </p>
        ) : (
          <>
            <div className="mt-5">
              <SearchableSelect
                label={t.offlinePayMethod}
                value={offlinePay.methodId}
                onChange={(v) => setOfflinePay({ ...offlinePay, methodId: v })}
              >
                {selectableOfflineMethods.map((m) => (
                  <option key={m.id} value={m.id}>
                    {name(m)}
                  </option>
                ))}
              </SearchableSelect>
            </div>
            <label className="mt-3 block text-sm">
              {t.offlinePayTendered}
              <Input
                inputMode="decimal"
                value={offlinePay.tendered}
                onChange={(e) =>
                  setOfflinePay({ ...offlinePay, tendered: e.target.value })
                }
                className="mt-1 min-h-12 w-full rounded-lg border px-3"
              />
            </label>
          </>
        )}
        <div className="mt-5 grid grid-cols-2 gap-3">
          <Button
            onClick={() => setOfflinePay(null)}
            className="pos-cancel min-h-12 rounded-lg border"
          >
            {t.offlinePayCancel}
          </Button>
          <Button
            disabled={busy || selectableOfflineMethods.length === 0}
            onClick={confirmOfflinePayment}
            className="min-h-12 rounded-lg bg-primary font-semibold text-primary-foreground disabled:opacity-60"
          >
            {t.offlinePayConfirm}
          </Button>
        </div>
      </section>
    </div>
  );
  const cartPanel = (
    <>
      <aside
        className="pos-ticket"
        aria-label={language === "ar" ? "الفاتورة الحالية" : "Current ticket"}
      >
        <div className="pos-ticket-heading">
          <ReceiptText size={20} aria-hidden="true" />
          <h2>
            {t.cart}{" "}
            <span key={cart.length} className="pos-count pos-count-bump">
              {cart.length}
            </span>
          </h2>
          <span className="pos-ticket-channel">
            {activeChannel && name(activeChannel)}
          </span>
          {cart.length > 0 && (
            <Button
              type="button"
              disabled={busy}
              onClick={() =>
                confirmClear ? clearOrder() : setConfirmClear(true)
              }
              className={`pos-clear ${confirmClear ? "is-confirming" : ""}`}
            >
              <Trash2 size={16} aria-hidden="true" />
              {confirmClear ? t.confirmClear : t.clearOrder}
            </Button>
          )}
          <Button
            className="lg:hidden"
            aria-label={language === "ar" ? "إغلاق السلة" : "Close cart"}
            onClick={() => setCartOpen(false)}
          >
            <X size={20} />
          </Button>
        </div>
        {(editing || addOnFor) && (
          <div className="pos-mode-banner" role="status">
            <span>
              {editing
                ? `${t.editingOrder} ${orderLabel(editing.number)}`
                : `${t.addOnFor} #${addOnFor}`}
            </span>
            <Button
              type="button"
              onClick={clearOrder}
              className="pos-mode-cancel"
            >
              {t.cancelEdit}
            </Button>
          </div>
        )}
        <VirtualTicketList
          lines={cart}
          label={t.cart}
          empty={t.empty}
          renderLine={(line) => (
            <TicketLine
              line={line}
              language={language}
              onQuantity={quantity}
              onNote={updateNote}
              disabled={busy}
            />
          )}
        />
        <footer className="pos-ticket-footer">
          <dl
            className="pos-totals"
            aria-label={language === "ar" ? "ملخص المبلغ" : "Order totals"}
          >
            <div>
              <dt>{t.subtotal}</dt>
              <dd data-testid="ticket-subtotal">OMR {total.toFixed(3)}</dd>
            </div>
            <div>
              <dt>{t.discountLabel}</dt>
              <dd data-testid="ticket-discount">
                − OMR {discountEstimate.toFixed(3)}
              </dd>
            </div>
            <div>
              <dt>
                {language === "ar"
                  ? "الضريبة ضمن الإجمالي"
                  : "Tax included in total"}
              </dt>
              <dd data-testid="ticket-tax">OMR {taxTotal.toFixed(3)}</dd>
            </div>
            <div className="pos-grand-total">
              <dt>{t.total}</dt>
              <dd data-testid="ticket-total">
                <small>OMR</small> {payableTotal.toFixed(3)}
              </dd>
            </div>
          </dl>
          <div className="pos-checkout-actions">
            <Button
              disabled={busy || !cart.length || discountExceedsMax}
              onClick={() => void submit("Draft")}
              className="pos-hold"
            >
              {t.hold}
            </Button>
            {!isExternallyPaidChannel && (
              <Button
                disabled={busy || !cart.length || discountExceedsMax || !online}
                onClick={() => void submit("Pending", true)}
                className="pos-hold"
              >
                {t.payLater}
              </Button>
            )}
            <Button
              disabled={busy || !cart.length || discountExceedsMax}
              onClick={() => {
                if (isExternallyPaidChannel) void submit("Pending");
                else setPayStep(true);
              }}
              className="pos-checkout"
            >
              {isExternallyPaidChannel
                ? language === "ar"
                  ? "تأكيد وإرسال للمطبخ"
                  : "Confirm & send to kitchen"
                : t.send}
            </Button>
          </div>
        </footer>
      </aside>
    </>
  );
  return (
    <div className="pos-register">
      <header className="pos-toolbar">
        <div className="flex min-w-0 flex-1 flex-wrap items-center gap-2">
          <strong className="pos-title">{t.title}</strong>
          {salesSummary && (
            <span className="shrink-0 rounded-full bg-muted px-3 py-1.5 text-xs font-semibold text-primary">
              {t.dailySales}: OMR {salesSummary.gross.toFixed(3)} ·{" "}
              {salesSummary.count} {t.dailySalesOrders}
            </span>
          )}
          <div className="w-40 shrink-0">
            <SearchableSelect
              label={t.branch}
              hideLabel
              disabled={cart.length > 0 || busy || !!payment}
              value={branchId}
              onChange={setBranchId}
            >
              {context?.branches.map((x) => (
                <option key={x.id} value={x.id}>
                  {name(x)}
                </option>
              ))}
            </SearchableSelect>
          </div>
          <div
            role="radiogroup"
            aria-label={t.channel}
            className="flex shrink-0 gap-1.5"
          >
            {[dineInChannel, takeawayChannel]
              .filter((channel): channel is SalesChannel => !!channel)
              .map((x) => {
                const Icon = channelIcon(x.code);
                const selected = channelId === x.id;
                return (
                  <Button
                    key={x.id}
                    type="button"
                    role="radio"
                    aria-checked={selected}
                    disabled={cart.length > 0 || busy || !!payment}
                    onClick={() => setChannelId(x.id)}
                    className={`flex min-h-12 shrink-0 items-center gap-1.5 rounded-full px-3 text-sm font-semibold disabled:opacity-50 ${selected ? "bg-primary text-primary-foreground" : "bg-card"}`}
                  >
                    <Icon size={16} />
                    {name(x)}
                  </Button>
                );
              })}
            {externalChannels.length > 0 && (
              <Button
                type="button"
                role="radio"
                aria-checked={isExternallyPaidChannel}
                disabled={cart.length > 0 || busy || !!payment}
                onClick={() =>
                  setChannelId(
                    externalChannels.find(
                      (channel) =>
                        channel.id ===
                        store.get<string>("pos-electronic-channel"),
                    )?.id ?? externalChannels[0].id,
                  )
                }
                className={`flex min-h-12 shrink-0 items-center gap-1.5 rounded-full px-3 text-sm font-semibold disabled:opacity-50 ${isExternallyPaidChannel ? "bg-primary text-primary-foreground" : "bg-card"}`}
              >
                <ShoppingBag size={16} />
                {language === "ar" ? "إلكتروني" : "Electronic"}
              </Button>
            )}
          </div>
          <Button
            onClick={() => setHeldOpen(true)}
            className="min-h-12 shrink-0 rounded-full border border-primary px-3 text-xs font-semibold text-primary"
          >
            {t.heldOrders} ({openOrderCount})
          </Button>
          <Button
            onClick={() => {
              if (!openCustomerDisplayWindow())
                setMessage(t.customerDisplayBlocked);
            }}
            title={t.customerDisplay}
            aria-label={t.customerDisplay}
            className="inline-flex min-h-12 shrink-0 items-center gap-1.5 rounded-full border border-border px-3 text-xs font-semibold text-muted-foreground"
          >
            <MonitorSmartphone size={16} aria-hidden="true" />
            <span className="pos-display-label">{t.customerDisplay}</span>
          </Button>
        </div>
        {isExternallyPaidChannel && (
          <div
            className="pos-electronic-channels"
            role="radiogroup"
            aria-label={
              language === "ar"
                ? "شركة الطلب الإلكتروني"
                : "Electronic order company"
            }
          >
            {externalChannels.map((channel) => {
              const logo = channelLogo(channel.code);
              return (
                <Button
                  key={channel.id}
                  type="button"
                  role="radio"
                  aria-checked={channel.id === channelId}
                  disabled={cart.length > 0 || busy || !!payment}
                  onClick={() => {
                    setChannelId(channel.id);
                    store.set("pos-electronic-channel", channel.id);
                  }}
                  className={`flex min-h-12 shrink-0 items-center gap-2 rounded-lg border px-4 text-sm font-semibold disabled:opacity-50 ${channel.id === channelId ? "border-primary bg-accent text-primary" : "border-border bg-card"}`}
                >
                  {logo ? (
                    <img
                      src={logo}
                      alt=""
                      className="h-6 w-6 shrink-0 rounded object-cover"
                    />
                  ) : (
                    <ShoppingBag size={16} className="shrink-0" />
                  )}
                  {name(channel)}
                </Button>
              );
            })}
            <span
              className={`pos-channel-hint ${cart.length > 0 ? "text-warning" : "text-muted-foreground"}`}
            >
              {cart.length > 0
                ? t.channelLocked
                : language === "ar"
                  ? "مدفوع خارجيًا · تُطبق قائمة أسعار الشركة"
                  : "Externally paid · company price list applied"}
            </span>
          </div>
        )}
        <PosSessionInfo
          language={language}
          cashier={cashierName}
          cashierLabel={t.cashier}
          shiftLabel={
            shiftOpenedAt === undefined
              ? null
              : shiftOpenedAt === null
                ? t.noOpenShift
                : `${t.shiftOpenSince} ${new Date(shiftOpenedAt).toLocaleTimeString(language, { hour: "numeric", minute: "2-digit" })}`
          }
          shiftMissing={shiftOpenedAt === null}
        />
        <span
          className={`flex shrink-0 items-center gap-1 text-xs ${online ? "text-success" : "text-destructive"}`}
        >
          {online ? (
            t.online
          ) : (
            <>
              <WifiOff size={15} />
              {t.offline}
            </>
          )}
        </span>
      </header>
      {qrPendingBanner}
      <div ref={salesGridRef} className="pos-workspace">
        <>
          <section className="pos-catalog" aria-label={t.search}>
            <div className="pos-catalog-tools">
              <label className="pos-search">
                <Search size={18} />
                <Input
                  ref={searchRef}
                  aria-label={t.search}
                  aria-keyshortcuts="F2"
                  onKeyDown={(e) => {
                    if (
                      e.key === "Enter" &&
                      !e.nativeEvent.isComposing &&
                      !busy &&
                      !catalogLoading
                    ) {
                      const product = productIndex.get(search.trim());
                      if (product) {
                        add(product);
                        setSearch("");
                      }
                    }
                  }}
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder={t.search}
                  className="w-full bg-transparent outline-none"
                />
                <ScanBarcode size={20} aria-hidden="true" />
                <kbd>F2</kbd>
              </label>
              <div
                className="pos-categories"
                aria-label={language === "ar" ? "الفئات" : "Categories"}
              >
                <Button
                  onClick={() => setCategory("")}
                  aria-pressed={!category}
                  className={`min-h-12 shrink-0 rounded-full px-4 text-sm font-semibold ${!category ? "bg-primary text-primary-foreground" : "bg-card"}`}
                >
                  {t.all}
                </Button>
                {categories.map((x) => (
                  <Button
                    key={x.id}
                    onClick={() => setCategory(x.id)}
                    aria-pressed={category === x.id}
                    className={`min-h-12 shrink-0 rounded-full px-4 text-sm font-semibold ${category === x.id ? "bg-primary text-primary-foreground" : "bg-card"}`}
                  >
                    {name(x)}
                  </Button>
                ))}
              </div>
            </div>
            <div className="pos-products-scroll">
              {catalogLoading && (
                <p role="status" className="mt-4">
                  {language === "ar" ? "جارٍ تحميل القائمة…" : "Loading menu…"}
                </p>
              )}
              {!catalogLoading && visible.length === 0 && (
                <p className="mt-4 rounded-xl bg-card p-6 text-sm">
                  {language === "ar"
                    ? "لا توجد منتجات مطابقة. جرّب بحثًا آخر، أو تحقق من تفعيل المنتجات وتوفرها في الفرع."
                    : "No matching products. Try another search or check product availability at this branch."}
                </p>
              )}
              <ProductGrid
                products={visible}
                language={language}
                busy={busy || catalogLoading}
                onAdd={add}
              />
            </div>
          </section>
          <div className="pos-ticket-desktop">{cartPanel}</div>
        </>
      </div>
      <Button
        onClick={() => setCartOpen(true)}
        className="pos-cart-toggle lg:hidden"
      >
        {t.viewCart} ({amounts.quantity}) · OMR {payableTotal.toFixed(3)}
      </Button>
      {cartOpen && (
        <div className="fixed inset-0 z-30 bg-black/35 lg:hidden">
          <div className="pos-mobile-ticket">{cartPanel}</div>
        </div>
      )}
      {customizing && (
        <div className="fixed inset-0 z-40 grid place-items-end bg-black/35 sm:place-items-center">
          <section
            role="dialog"
            aria-modal="true"
            aria-label={name(customizing)}
            className="max-h-[85vh] w-full overflow-y-auto rounded-t-2xl bg-card p-5 sm:max-w-lg sm:rounded-2xl"
          >
            <h2 className="text-lg font-bold">{name(customizing)}</h2>
            <p className="mt-1 text-sm text-muted-foreground">{t.selections}</p>
            {customizing.selectionGroups.map((group) => (
              <fieldset key={group.id} className="mt-5">
                <legend className="font-semibold">
                  {name(group)} {group.isRequired ? "*" : ""}
                </legend>
                <div className="mt-2 space-y-2">
                  {group.options.map((option) => (
                    <label
                      key={option.id}
                      className="flex min-h-12 items-center justify-between rounded-lg bg-muted px-3"
                    >
                      <span>
                        <Input
                          type="checkbox"
                          disabled={option.isAvailable === false}
                          checked={
                            selections[group.id]?.includes(option.id) ?? false
                          }
                          onChange={() =>
                            setSelections({
                              ...selections,
                              [group.id]: selections[group.id]?.includes(
                                option.id,
                              )
                                ? selections[group.id].filter(
                                    (x) => x !== option.id,
                                  )
                                : [
                                    ...(selections[group.id] ?? []),
                                    option.id,
                                  ].slice(-group.maxSelections),
                            })
                          }
                          className="me-2"
                        />
                        {name(option)}
                        {option.isAvailable === false
                          ? language === "ar"
                            ? " — غير متوفر"
                            : " — Unavailable"
                          : ""}
                      </span>
                      <span>+{option.priceAdjustment.toFixed(3)}</span>
                    </label>
                  ))}
                </div>
              </fieldset>
            ))}
            <div className="mt-6 flex gap-3">
              <Button
                onClick={() => setCustomizing(null)}
                className="pos-cancel min-h-12 flex-1 rounded-lg border"
              >
                {t.offlinePayCancel}
              </Button>
              <Button
                onClick={confirm}
                className="min-h-12 flex-1 rounded-lg bg-primary font-semibold text-primary-foreground"
              >
                {t.confirm}
              </Button>
            </div>
          </section>
        </div>
      )}
      {payStep && !isExternallyPaidChannel && cart.length > 0 && (
        <FormDialog
          title={t.send}
          closeLabel={t.backToCart}
          onClose={() => setPayStep(false)}
          width="max-w-lg"
        >
          <div className="grid gap-4 p-4 sm:p-5">
            <ul
              aria-label={language === "ar" ? "تفاصيل الطلب" : "Order details"}
              className="max-h-56 divide-y divide-border overflow-y-auto rounded-lg border border-border text-sm"
            >
              {cart.map((line) => {
                const title =
                  language === "ar" ? line.product.nameAr : line.product.nameEn;
                const unit = resolveOfflinePricing(
                  line.product.pricing,
                  lineAdjustment(line),
                ).gross;
                return (
                  <li
                    key={line.key}
                    className="flex items-start justify-between gap-3 px-3 py-2"
                  >
                    <span className="min-w-0">
                      <span className="font-semibold">{line.quantity} × </span>
                      {title}
                      {line.note && (
                        <span className="block text-xs text-muted-foreground">
                          {line.note}
                        </span>
                      )}
                    </span>
                    <span dir="ltr" className="shrink-0 font-semibold">
                      {(unit * line.quantity).toFixed(3)}
                    </span>
                  </li>
                );
              })}
            </ul>
            <dl className="grid gap-1 text-sm">
              <div className="flex justify-between text-muted-foreground">
                <dt>{t.subtotal}</dt>
                <dd dir="ltr">OMR {total.toFixed(3)}</dd>
              </div>
              {discountEstimate > 0 && (
                <div className="flex justify-between text-muted-foreground">
                  <dt>{t.discountLabel}</dt>
                  <dd dir="ltr">− OMR {discountEstimate.toFixed(3)}</dd>
                </div>
              )}
              <div className="flex justify-between text-muted-foreground">
                <dt>
                  {language === "ar"
                    ? "الضريبة ضمن الإجمالي"
                    : "Tax included in total"}
                </dt>
                <dd dir="ltr">OMR {taxTotal.toFixed(3)}</dd>
              </div>
              <div className="mt-1 flex items-baseline justify-between border-t border-border pt-2">
                <dt className="font-bold">{t.total}</dt>
                <dd dir="ltr" className="text-2xl font-extrabold">
                  OMR {payableTotal.toFixed(3)}
                </dd>
              </div>
            </dl>
            {canDiscount && online && cart.length > 0 && (
              <div>
                {!discountOpen ? (
                  <Button
                    type="button"
                    onClick={() => setDiscountOpen(true)}
                    className="flex min-h-10 w-full items-center justify-center gap-1.5 rounded-lg border border-dashed border-border text-sm font-semibold text-muted-foreground"
                  >
                    <Plus size={13} />
                    {t.addDiscount}
                  </Button>
                ) : (
                  <div className="rounded-lg border border-border p-3">
                    <div className="flex items-center justify-between">
                      <p className="text-sm font-medium">{t.addDiscount}</p>
                      <Button
                        type="button"
                        onClick={clearDiscount}
                        className="text-xs text-destructive"
                      >
                        {t.discountRemove}
                      </Button>
                    </div>
                    <div className="mt-2 grid grid-cols-2 gap-2">
                      <Button
                        type="button"
                        onClick={() => {
                          setDiscountType("Percentage");
                          applyDiscount();
                        }}
                        className={`min-h-12 rounded-lg border text-sm font-semibold ${discountType === "Percentage" ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card"}`}
                      >
                        {t.discountPercentage}
                      </Button>
                      <Button
                        type="button"
                        onClick={() => {
                          setDiscountType("Amount");
                          applyDiscount();
                        }}
                        className={`min-h-12 rounded-lg border text-sm font-semibold ${discountType === "Amount" ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card"}`}
                      >
                        {t.discountAmount}
                      </Button>
                    </div>
                    <Input
                      aria-label={t.addDiscount}
                      type="text"
                      inputMode="decimal"
                      value={discountValue}
                      onChange={(e) => {
                        const normalized = normalizeMoneyInput(e.target.value);
                        if (normalized === null) return;
                        setDiscountValue(normalized);
                        setPayCash("");
                        setPayCard("");
                      }}
                      placeholder={
                        discountType === "Percentage" ? "0" : "0.000"
                      }
                      className="mt-2 min-h-12 w-full rounded-lg border border-border bg-card px-3 text-base outline-none focus:border-primary md:text-sm"
                    />
                    {discountEstimate > 0 && (
                      <p className="mt-2 text-xs text-muted-foreground">
                        {t.discountLabel}: -OMR {discountEstimate.toFixed(3)}
                      </p>
                    )}
                    {discountExceedsMax && (
                      <p className="mt-1 text-xs text-destructive">
                        {t.discountTooHigh}
                      </p>
                    )}
                  </div>
                )}
              </div>
            )}
            <div>
              <p className="text-sm font-medium">{t.payMethod}</p>
              <div className="mt-2 grid grid-cols-3 gap-2">
                <Button
                  type="button"
                  disabled={!payCashMethod}
                  onClick={() => choosePayMethod("Cash")}
                  className={`flex min-h-11 items-center justify-center gap-1.5 rounded-lg border text-sm font-semibold disabled:opacity-40 ${effectivePayMethod === "Cash" ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card"}`}
                >
                  <Banknote size={14} />
                  {t.cash}
                </Button>
                <Button
                  type="button"
                  disabled={!payCardMethod}
                  onClick={() => choosePayMethod("Card")}
                  className={`flex min-h-11 items-center justify-center gap-1.5 rounded-lg border text-sm font-semibold disabled:opacity-40 ${effectivePayMethod === "Card" ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card"}`}
                >
                  <CreditCard size={14} />
                  {t.card}
                </Button>
                <Button
                  type="button"
                  disabled={!payCashMethod || !payCardMethod}
                  onClick={() => choosePayMethod("Mixed")}
                  className={`flex min-h-11 items-center justify-center rounded-lg border text-sm font-semibold disabled:opacity-40 ${effectivePayMethod === "Mixed" ? "border-primary bg-primary text-primary-foreground" : "border-border bg-card"}`}
                >
                  {t.mixed}
                </Button>
              </div>
              {/* The split row keeps its space for every method so the layout never shifts under the cashier. */}
              <div
                className={`mt-2 grid grid-cols-2 gap-2 ${effectivePayMethod === "Mixed" ? "" : "invisible"}`}
                aria-hidden={effectivePayMethod !== "Mixed"}
              >
                {(
                  [
                    [t.cash, payCash, setPayCashAmount],
                    [t.card, payCard, setPayCardAmount],
                  ] as const
                ).map(([label, value, onChange], index) => (
                  <label
                    key={label}
                    className="flex min-h-11 items-center gap-2 rounded-lg border border-border bg-card px-3 focus-within:border-primary"
                  >
                    <span className="shrink-0 text-xs font-semibold text-muted-foreground">
                      {label}
                    </span>
                    <Input
                      aria-label={label}
                      type="text"
                      inputMode="decimal"
                      value={value}
                      tabIndex={effectivePayMethod === "Mixed" ? 0 : -1}
                      autoFocus={effectivePayMethod === "Mixed" && index === 0}
                      onChange={(e) => onChange(e.target.value)}
                      placeholder="0.000"
                      dir="ltr"
                      className="min-w-0 flex-1 border-0 bg-transparent dark:bg-transparent focus-visible:ring-0 px-0 text-end text-base shadow-none outline-none md:text-sm"
                    />
                  </label>
                ))}
              </div>
              <p role="alert" className="mt-1 min-h-4 text-xs text-destructive">
                {offlineMethods.length === 0 || payMessage
                  ? payMessage || t.payNoMethods
                  : ""}
              </p>
            </div>
            <div className="sticky bottom-0 -mx-4 -mb-4 grid grid-cols-[auto_minmax(0,1fr)] gap-2 border-t border-border bg-background px-4 py-3 sm:-mx-5 sm:-mb-5 sm:px-5">
              <Button
                type="button"
                onClick={() => setPayStep(false)}
                className="min-h-11 rounded-lg border border-border px-4 text-sm font-semibold"
              >
                {t.backToCart}
              </Button>
              <Button
                type="button"
                disabled={
                  busy ||
                  discountExceedsMax ||
                  !payValid ||
                  offlineMethods.length === 0
                }
                onClick={() => void submit("Pending")}
                className="min-h-11 rounded-lg bg-primary px-4 font-bold text-primary-foreground hover:bg-primary/90"
              >
                {t.confirmPay}
              </Button>
            </div>
          </div>
        </FormDialog>
      )}
      {payment && (
        <PaymentDialog
          language={language}
          orderId={payment.orderId}
          branchId={branchId}
          total={payment.total}
          onClose={() => {
            setPayment(null);
            void refreshOrders();
          }}
        />
      )}
      {qrToastsNode}
      {offlinePayModal}
      {heldOrdersModal}
      {(kdsOfflineSince || kitchenWatch.some((entry) => entry.late)) && (
        <div role="alert" className="pos-kitchen-alerts">
          {kdsOfflineSince && (
            <div className="pos-kitchen-alert is-offline">
              <span>
                <strong>
                  {t.kitchenScreenOffline}{" "}
                  {new Date(kdsOfflineSince).toLocaleTimeString(language, {
                    hour: "numeric",
                    minute: "2-digit",
                  })}
                </strong>
                <small>{t.kitchenScreenOfflineNote}</small>
              </span>
              <Button
                type="button"
                onClick={() => setKdsDismissed(kdsOfflineSince)}
                className="pos-kitchen-dismiss"
              >
                {t.hide}
              </Button>
            </div>
          )}
          {(() => {
            const late = kitchenWatch.filter((entry) => entry.late);
            if (!late.length) return null;
            const ids = late.map((entry) => entry.orderId);
            return (
              <div className="pos-kitchen-alert">
                <span>
                  {late.length === 1 ? (
                    <>
                      <strong>{orderLabel(late[0].number)}</strong> ·{" "}
                      {t.notReceived}
                    </>
                  ) : (
                    <>
                      <strong>
                        {late.length} {t.notReceivedCount}
                      </strong>
                      <small>
                        {late
                          .map((entry) => orderLabel(entry.number))
                          .join(" · ")}
                      </small>
                    </>
                  )}
                </span>
                <Button
                  type="button"
                  onClick={() => void printForKitchen(ids)}
                  className="pos-kitchen-print"
                >
                  {late.length === 1 ? t.printKitchen : t.printAllKitchen}
                </Button>
                <Button
                  type="button"
                  onClick={() =>
                    setKitchenWatch((watch) =>
                      watch.filter((x) => !ids.includes(x.orderId)),
                    )
                  }
                  className="pos-kitchen-dismiss"
                >
                  {t.kitchenInformed}
                </Button>
              </div>
            );
          })()}
        </div>
      )}
      {message && (
        <div
          key={message}
          role={messageIsSuccess ? "status" : "alert"}
          className={`pos-message ${messageIsSuccess ? "is-success" : "is-error"}`}
        >
          <span>{message}</span>
          <Button
            type="button"
            onClick={() => setMessage("")}
            aria-label={t.closeMessage}
            className="pos-message-close"
          >
            <X size={16} aria-hidden="true" />
          </Button>
        </div>
      )}
    </div>
  );
}

const ProductGrid = memo(function ProductGrid({
  products,
  language,
  busy,
  onAdd,
}: {
  products: Product[];
  language: Language;
  busy: boolean;
  onAdd: (product: Product) => void;
}) {
  return (
    <div className="pos-product-grid">
      {products.map((product) => {
        const title = language === "ar" ? product.nameAr : product.nameEn;
        return (
          <Button
            key={product.id}
            disabled={busy}
            onClick={(event) => {
              onAdd(product);
              // Instant visual confirmation without re-rendering the grid.
              event.currentTarget.animate(
                [
                  { transform: "scale(1)" },
                  {
                    transform: "scale(0.96)",
                    boxShadow: "0 0 0 3px var(--ring)",
                  },
                  { transform: "scale(1)" },
                ],
                { duration: 260, easing: "ease-out" },
              );
            }}
            className="pos-product"
          >
            <ProductPhoto
              src={product.imageUrl}
              name=""
              className="pos-product-photo"
            />
            <strong>{title}</strong>
            <span className="pos-product-price">
              OMR {resolveOfflinePricing(product.pricing, 0).gross.toFixed(3)}
            </span>
          </Button>
        );
      })}
    </div>
  );
});

const TicketLine = memo(function TicketLine({
  line,
  language,
  onQuantity,
  onNote,
  disabled,
}: {
  line: CartLine;
  language: Language;
  onQuantity: (key: string, delta: number) => void;
  onNote: (key: string, note: string) => void;
  disabled: boolean;
}) {
  const title = language === "ar" ? line.product.nameAr : line.product.nameEn;
  const unit = resolveOfflinePricing(
    line.product.pricing,
    lineAdjustment(line),
  ).gross;
  // Unit price, quantity and chosen modifiers so the cashier can verify a line at a glance.
  const choices = lineChoiceNames(line, language);
  const details = [
    `${unit.toFixed(3)} × ${line.quantity}`,
    ...choices,
    ...(line.note ? [line.note] : []),
  ].join(" · ");
  return (
    <div className="pos-ticket-line">
      <strong title={title}>{title}</strong>
      <span className="pos-line-amount">
        {(unit * line.quantity).toFixed(3)}
      </span>
      <small className="pos-line-details" title={details}>
        {details}
      </small>
      <div className="pos-quantity">
        <Button
          disabled={disabled}
          aria-label={language === "ar" ? "تقليل الكمية" : "Decrease"}
          className={line.quantity === 1 ? "pos-remove" : ""}
          onClick={() => onQuantity(line.key, -1)}
        >
          <Minus size={18} />
        </Button>
        <span>{line.quantity}</span>
        <Button
          disabled={disabled}
          aria-label={language === "ar" ? "زيادة الكمية" : "Increase"}
          onClick={() => onQuantity(line.key, 1)}
        >
          <Plus size={18} />
        </Button>
      </div>
      <Input
        disabled={disabled}
        aria-label={`${words[language].notes}: ${title}`}
        value={line.note}
        onChange={(event) => onNote(line.key, event.target.value)}
        placeholder={words[language].notes}
        className="pos-line-note"
      />
    </div>
  );
});

function lineAdjustment(line: CartLine) {
  return Object.entries(line.selections).reduce((sum, [groupId, ids]) => {
    const group = line.product.selectionGroups.find(
      (group) => group.id === groupId,
    );
    return (
      sum +
      ids.reduce(
        (value, id) =>
          value +
          (group?.options.find((option) => option.id === id)?.priceAdjustment ??
            0),
        0,
      )
    );
  }, 0);
}

// Mirrors backend PricingRules.Resolve exactly (3-decimal money, away-from-zero) so an offline sale
// carries the same tax the server would have computed online, instead of a stale/zeroed snapshot.
function roundMoney(value: number) {
  return Math.round((value + Number.EPSILON) * 1000) / 1000;
}

function resolveOfflinePricing(pricing: Pricing, adjustment: number) {
  const listPrice = roundMoney(pricing.listPrice + adjustment);
  const discount = roundMoney(
    Math.min(listPrice, listPrice * pricing.discountRate),
  );
  const taxable = roundMoney(listPrice - discount);
  const rate = pricing.taxRate;
  let net: number, gross: number;
  if (pricing.taxCalculationMode === "Inclusive") {
    net = roundMoney(taxable / (1 + rate / 100));
    gross = taxable;
  } else {
    net = taxable;
    gross = roundMoney(net + roundMoney((net * rate) / 100));
  }
  const taxAmount = roundMoney(gross - net);
  return { listPrice, discount, net, taxAmount, gross };
}

function buildOfflineOrder(
  channelId: string,
  cartLines: CartLine[],
  status: "Draft" | "Paid",
  payment?: {
    clientRequestId: string;
    paymentMethodId: string;
    amount: number;
    tenderedAmount: number;
  },
) {
  return {
    salesChannelId: channelId,
    source: "Pos",
    status,
    note: null,
    payment: status === "Paid" ? payment : null,
    lines: cartLines.map((line) => {
      const selectionsSnapshot = JSON.stringify(
        Object.entries(line.selections).map(([groupId, ids]) => ({
          groupId,
          options: ids,
        })),
      );
      // Sent alongside the (informational-only) snapshot so the server can independently recompute
      // this line's price from the live catalog instead of trusting client-calculated amounts.
      const selections = Object.entries(line.selections)
        .filter(([, ids]) => ids.length > 0)
        .map(([groupId, ids]) => ({
          selectionGroupId: groupId,
          choices: ids.map((optionId) => ({ optionId, quantity: 1 })),
        }));
      const adjustment = Object.entries(line.selections)
        .flatMap(([groupId, ids]) =>
          ids.map(
            (optionId) =>
              line.product.selectionGroups
                .find((group) => group.id === groupId)
                ?.options.find((option) => option.id === optionId)
                ?.priceAdjustment ?? 0,
          ),
        )
        .reduce((a, b) => a + b, 0);
      const { pricing } = line.product;
      const resolved = resolveOfflinePricing(pricing, adjustment);
      return {
        productId: line.product.id,
        productNameAr: line.product.nameAr,
        productNameEn: line.product.nameEn,
        quantity: line.quantity,
        note: line.note || null,
        selectionsSnapshot,
        selections,
        unitListAmount: resolved.listPrice,
        unitDiscountAmount: resolved.discount,
        unitNetAmount: resolved.net,
        unitTaxAmount: resolved.taxAmount,
        unitGrossAmount: resolved.gross,
        taxRate: pricing.taxRate,
        taxCalculationMode: pricing.taxCalculationMode,
        priceSource: "OfflineSnapshot",
        priceRuleId: pricing.priceRuleId,
        promotionId: pricing.promotionId,
        taxRuleId: pricing.taxRuleId,
        catalogVersionId: pricing.catalogVersionId,
        catalogVersionNumber: pricing.catalogVersionNumber,
      };
    }),
  };
}
