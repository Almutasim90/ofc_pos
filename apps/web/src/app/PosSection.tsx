import { useEffect, useRef, useState } from "react";
import { Minus, Plus, QrCode, Search, ShoppingBag, Store, UtensilsCrossed, WifiOff } from "lucide-react";
import { createId, store } from "@/lib/local-store";
import { enqueue, setBranchId as persistBranch } from "@/lib/sync-outbox";
import { exitKiosk } from "@/lib/fullscreen-kiosk";
import { useQrOrdersLive, type QrOrderReceivedEvent, type QrOrderReviewedEvent } from "@/lib/orders-realtime";
import { ProductPhoto } from "@/app/CatalogScreen";
import { PaymentDialog } from "@/app/PaymentDialog";

type Language = "ar" | "en";
type Context = { branches: Array<{ id: string; nameAr: string; nameEn: string }>; channels: Array<{ id: string; code: string; nameAr: string; nameEn: string }> };
type Choice = { id: string; productId: string; nameAr: string; nameEn: string; priceAdjustment: number; isDefault: boolean; maxQuantity: number };
type Group = { id: string; nameAr: string; nameEn: string; isRequired: boolean; minSelections: number; maxSelections: number; options: Choice[] };
type Pricing = { listPrice: number; discountRate: number; taxRate: number; taxCalculationMode: "Exclusive" | "Inclusive"; priceSource: string; priceRuleId: string | null; promotionId: string | null; taxRuleId: string | null; catalogVersionId: string | null; catalogVersionNumber: number | null };
type Product = { id: string; sku: string; barcode: string | null; categoryId: string; categoryNameAr: string; categoryNameEn: string; nameAr: string; nameEn: string; basePrice: number | null; imageUrl: string | null; pricing: Pricing; selectionGroups: Group[] };
type CartLine = { key: string; product: Product; quantity: number; note: string; selections: Record<string, string[]> };
type Method = { id: string; nameAr: string; nameEn: string; kind: string };
const words = { ar: { title: "نقطة البيع", search: "ابحث عن صنف", all: "الكل", cart: "السلة", empty: "أضف أصنافًا للبدء", hold: "تعليق", send: "الدفع", notes: "ملاحظة", branch: "الفرع", channel: "قناة البيع", offline: "غير متصل: ستتم المزامنة عند عودة الاتصال", online: "متصل", total: "الإجمالي", add: "إضافة", confirm: "تأكيد الاختيارات", selections: "الاختيارات", saved: "تم حفظ الطلب", unavailable: "تعذر حفظ الطلب، سيبقى في السلة", viewCart: "عرض السلة", offlinePayTitle: "دفع غير متصل", offlinePayMethod: "طريقة الدفع", offlinePayTendered: "المبلغ المستلم", offlinePayConfirm: "تأكيد الدفع وحفظ الطلب", offlinePayCancel: "إلغاء", offlineNoMethods: "لا توجد وسائل دفع محفوظة لهذا الفرع؛ اتصل بالإنترنت مرة واحدة على الأقل", offlinePayInvalid: "المبلغ المستلم غير كافٍ", heldOrders: "الطلبات الحالية", resume: "استئناف", noHeld: "لا توجد طلبات حالية", heldSince: "منذ", qrNew: "وصل طلب QR جديد", qrPending: "طلب QR جديد بانتظار الاعتماد", qrApproved: "تم اعتماد طلب QR", qrRejected: "تم رفض طلب QR" }, en: { title: "Point of sale", search: "Search products", all: "All", cart: "Cart", empty: "Add products to begin", hold: "Hold", send: "Pay", notes: "Note", branch: "Branch", channel: "Sales channel", offline: "Offline: the cart will sync when connection returns", online: "Online", total: "Total", add: "Add", confirm: "Confirm selections", selections: "Selections", saved: "Order saved", unavailable: "Unable to save; cart remains available", viewCart: "View cart", offlinePayTitle: "Offline payment", offlinePayMethod: "Payment method", offlinePayTendered: "Amount tendered", offlinePayConfirm: "Confirm payment and save order", offlinePayCancel: "Cancel", offlineNoMethods: "No payment methods are cached for this branch; connect to the internet at least once first", offlinePayInvalid: "Tendered amount does not cover the total", heldOrders: "Current orders", resume: "Resume", noHeld: "No current orders", heldSince: "Held since", qrNew: "New QR order received", qrPending: "New QR order awaiting approval", qrApproved: "QR order approved", qrRejected: "QR order rejected" } } as const;
type HeldOrder = { id: string; status: string; grossAmount: number; note: string | null; createdAt: string };
type QrToast = { id: number; text: string };
const channelIcons: Record<string, typeof Store> = { POS: Store, DINEIN: UtensilsCrossed, TAKEAWAY: ShoppingBag, WEBQR: QrCode };
function channelIcon(code: string) { return channelIcons[code] ?? Store; }

export function PosSection({ language, kiosk, onKioskChange }: { language: Language; kiosk: boolean; onKioskChange: (value: boolean) => void }) {
  const t = words[language]; const name = (x: { nameAr: string; nameEn: string }) => language === "ar" ? x.nameAr : x.nameEn;
  const [context, setContext] = useState<Context | null>(null); const [branchId, setBranchId] = useState(""); const [channelId, setChannelId] = useState(""); const [products, setProducts] = useState<Product[]>([]); const [category, setCategory] = useState(""); const [search, setSearch] = useState(""); const [cart, setCart] = useState<CartLine[]>(() => store.get<CartLine[]>("pos-cart") ?? []); const [customizing, setCustomizing] = useState<Product | null>(null); const [selections, setSelections] = useState<Record<string, string[]>>({}); const [online, setOnline] = useState(navigator.onLine); const [message, setMessage] = useState(""); const [cartOpen, setCartOpen] = useState(false); const [payment, setPayment] = useState<{ orderId: string; total: number } | null>(null); const [offlineMethods, setOfflineMethods] = useState<Method[]>(() => store.get<Method[]>("pos-payment-methods") ?? []); const [offlinePay, setOfflinePay] = useState<{ methodId: string; tendered: string } | null>(null); const [heldOrders, setHeldOrders] = useState<HeldOrder[]>([]); const [heldOpen, setHeldOpen] = useState(false);
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const requestRef = useRef({ snapshot: "", id: "" });
  const [qrToasts, setQrToasts] = useState<QrToast[]>([]);
  const qrToastId = useRef(0);
  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });
  useEffect(() => { const update = () => setOnline(navigator.onLine); addEventListener("online", update); addEventListener("offline", update); return () => { removeEventListener("online", update); removeEventListener("offline", update); }; }, []);
  // When kiosk is left (toggle, navigation, or the browser exiting fullscreen), clean up fullscreen,
  // cursor and the blocked shortcuts.
  useEffect(() => { if (kiosk) return; exitKiosk(); }, [kiosk]);
  useEffect(() => {
    if (!kiosk) return;
    const onFsChange = () => { if (!document.fullscreenElement && !(document as unknown as { webkitFullscreenElement?: Element | null }).webkitFullscreenElement) { exitKiosk(); onKioskChange(false); } };
    document.addEventListener("fullscreenchange", onFsChange);
    document.addEventListener("webkitfullscreenchange", onFsChange as EventListener);
    return () => { document.removeEventListener("fullscreenchange", onFsChange); document.removeEventListener("webkitfullscreenchange", onFsChange as EventListener); };
  }, [kiosk, onKioskChange]);
  useEffect(() => { void (async () => {
    try {
      let value = store.get<Context>("pos-context");
      if (navigator.onLine) { const response = await auth("/api/v1/pos/context"); if (!response.ok) throw new Error(); value = await response.json() as Context; store.set("pos-context", value); }
      if (!value) throw new Error(); setContext(value);
      setBranchId(value.branches.find(b => b.id === store.get<string>("pos-branch"))?.id ?? value.branches[0]?.id ?? "");
      setChannelId(value.channels.find(c => c.id === store.get<string>("pos-channel"))?.id ?? value.channels[0]?.id ?? "");
    } catch { setMessage(language === "ar" ? "تعذر تحميل بيانات البيع. تحقق من الاتصال ثم حدّث الصفحة." : "Unable to load POS. Check the connection and refresh."); }
  })(); }, []);
  useEffect(() => {
    if (!branchId || !channelId) return; let live = true; setCatalogLoading(true); setCategory("");
    store.set("pos-branch", branchId); store.set("pos-channel", channelId);
    void (async () => {
      try {
        const key = "pos-catalog-" + branchId + "-" + channelId;
        let value = store.get<Product[]>(key);
        if (online) { const response = await auth("/api/v1/pos/catalog?branchId=" + branchId + "&salesChannelId=" + channelId); if (!response.ok) throw new Error(); value = await response.json() as Product[]; store.set(key, value); }
        if (!value) throw new Error(); if (live) setProducts(value);
      } catch { if (live) { setProducts([]); setMessage(language === "ar" ? "تعذر تحميل المنتجات لهذا الفرع. تحقق من الاتصال وتوفر المنتجات." : "Unable to load this branch’s products. Check connection and availability."); } }
      finally { if (live) setCatalogLoading(false); }
    })(); return () => { live = false; };
  }, [branchId, channelId, online]);
  // Payment methods are cached locally the moment we're online so an offline sale can still name a real
  // method and collect a tendered amount instead of skipping payment capture entirely (see buildOfflineOrder).
  useEffect(() => {
    if (!branchId) return; let live = true;
    const key = "pos-payment-methods-" + branchId;
    setOfflineMethods(store.get<Method[]>(key) ?? []);
    if (online) void (async () => { try { const response = await auth(`/api/v1/payment-methods?branchId=${branchId}`); if (!response.ok) return; const value = await response.json() as Method[]; if (live) { setOfflineMethods(value); store.set(key, value); } } catch { /* The branch-specific cache remains available offline. */ } })();
    return () => { live = false; };
  }, [branchId, online]);
  useEffect(() => { store.set("pos-cart", cart); }, [cart]);
  async function loadHeld() { if (!branchId || !online) return; try { const response = await auth(`/api/v1/orders?branchId=${branchId}`); if (response.ok) setHeldOrders((await response.json() as HeldOrder[]).filter((o) => ["Draft", "Pending", "Confirmed", "Paid"].includes(o.status))); } catch { setMessage(t.unavailable); } }
  useEffect(() => { void loadHeld(); }, [branchId, online]);
  // Realtime QR-order alerts for the cashier. SignalR is transport only — events never carry
  // authoritative state; they just refresh the REST-backed lists and raise a notification (SPA, no reload).
  function qrNotify(text: string) {
    const id = ++qrToastId.current;
    setQrToasts((prev) => [...prev, { id, text }]);
    window.setTimeout(() => setQrToasts((prev) => prev.filter((x) => x.id !== id)), 3000);
  }
  function onQrOrderReceived(payload: QrOrderReceivedEvent) {
    if (!branchId || payload.branchId !== branchId) return;
    const who = payload.approvalStatus === "Pending" ? t.qrPending : t.qrNew;
    qrNotify(`${who} · ${payload.clientRequestId.slice(0, 8)} · OMR ${Number(payload.grossAmount).toFixed(3)}`);
    void loadHeld();
  }
  function onQrOrderReviewed(payload: QrOrderReviewedEvent) {
    if (!branchId || payload.branchId !== branchId) return;
    qrNotify(`${payload.approvalStatus === "Approved" ? t.qrApproved : t.qrRejected} · ${payload.clientRequestId.slice(0, 8)}`);
    void loadHeld();
  }
  useQrOrdersLive(branchId && online ? branchId : null, onQrOrderReceived, onQrOrderReviewed);
  const qrToastsNode = qrToasts.length > 0 && (
    <div className="pointer-events-none fixed inset-x-0 top-4 z-[60] flex flex-col items-center gap-2 px-4">
      {qrToasts.map((toast) => (
        <div key={toast.id} role="status" className="pointer-events-auto flex w-full max-w-md items-start justify-between gap-3 rounded-xl border border-[#bcd8c9] bg-[#e3f4ea] px-4 py-3 text-sm font-medium text-[#0e5a4f] shadow-lg">
          <span>{toast.text}</span>
          <button onClick={() => setQrToasts((prev) => prev.filter((x) => x.id !== toast.id))} className="shrink-0 rounded-lg p-1 opacity-70 hover:opacity-100">×</button>
        </div>
      ))}
    </div>
  );
  async function dispatchOrder(orderId: string) {
    const response = await auth("/api/v1/kitchen/tickets", { method: "POST", body: JSON.stringify({ branchId, orderId, clientDispatchId: createId(), orderNumber: null, note: null, targetMinutes: null }) });
    if (!response.ok) throw new Error(language === "ar" ? "الطلب محفوظ. تعذر إرساله للمطبخ؛ أعد الإرسال من الطلبات الحالية." : "Order saved. Kitchen dispatch failed; retry from Current orders.");
    setMessage(language === "ar" ? "تم إرسال الطلب للمطبخ." : "Order sent to kitchen.");
  }
  async function resumeHeld(order: HeldOrder, kitchen = false) {
    if (busy) return; setBusy(true); setMessage("");
    try {
      if (order.status === "Draft") { const response = await auth("/api/v1/orders/" + order.id + "/status", { method: "POST", body: JSON.stringify({ status: "Pending", note: null }) }); if (!response.ok) throw new Error(t.unavailable); }
      if (kitchen) await dispatchOrder(order.id); else { setHeldOpen(false); setPayment({ orderId: order.id, total: order.grossAmount }); }
      await loadHeld();
    } catch (e) { setMessage(e instanceof Error ? e.message : t.unavailable); } finally { setBusy(false); }
  }
  const categories = Array.from(new Map(products.map((p) => [p.categoryId, { id: p.categoryId, nameAr: p.categoryNameAr, nameEn: p.categoryNameEn }])).values());
  const visible = products.filter((p) => (!category || p.categoryId === category) && `${p.nameAr} ${p.nameEn} ${p.sku} ${p.barcode ?? ""}`.toLowerCase().includes(search.toLowerCase()));
  function lineAdjustment(line: CartLine) { return Object.entries(line.selections).flatMap(([group, ids]) => ids.map((id) => line.product.selectionGroups.find((x) => x.id === group)?.options.find((x) => x.id === id)?.priceAdjustment ?? 0)).reduce((a, b) => a + b, 0); }
  const total = cart.reduce((sum, line) => sum + resolveOfflinePricing(line.product.pricing, lineAdjustment(line)).gross * line.quantity, 0);
  function add(product: Product) { if (!product.selectionGroups.length) { setCart([...cart, { key: createId(), product, quantity: 1, note: "", selections: {} }]); return; } setCustomizing(product); setSelections(Object.fromEntries(product.selectionGroups.map((group) => [group.id, group.options.filter((option) => option.isDefault).map((option) => option.id)]))); }
  function confirm() { if (!customizing) return; if (customizing.selectionGroups.some((group) => { const count = selections[group.id]?.length ?? 0; return count < group.minSelections || count > group.maxSelections; })) return; setCart([...cart, { key: createId(), product: customizing, quantity: 1, note: "", selections }]); setCustomizing(null); }
  function quantity(key: string, delta: number) { setCart(cart.flatMap((line) => line.key !== key ? [line] : line.quantity + delta < 1 ? [] : [{ ...line, quantity: line.quantity + delta }])); }
  async function submit(status: "Draft" | "Pending" | "Kitchen") {
    if (!cart.length || !branchId || !channelId || busy) return;
    if (!online) {
      if (status === "Kitchen") { setMessage(language === "ar" ? "إرسال المطبخ يحتاج اتصالًا. يمكنك تعليق الطلب أو تسجيل الدفع دون اتصال." : "Kitchen dispatch requires a connection. Hold the order or record an offline payment."); return; }
      persistBranch(branchId);
      if (status === "Draft") { enqueue({ operationType: "order.create", baseVersion: null, baseCatalogVersion: null, occurredAt: new Date().toISOString(), payload: buildOfflineOrder(channelId, cart, "Draft") }); setCart([]); setMessage(t.offline); return; }
      setOfflinePay({ methodId: offlineMethods[0]?.id ?? "", tendered: total.toFixed(3) }); return;
    }
    setBusy(true); setMessage("");
    try {
      const snapshot = JSON.stringify({ branchId, channelId, cart });
      if (requestRef.current.snapshot !== snapshot) requestRef.current = { snapshot, id: createId() };
      const body = { branchId, salesChannelId: channelId, clientRequestId: requestRef.current.id, source: "Pos", note: null, lines: cart.map(line => ({ productId: line.product.id, quantity: line.quantity, note: line.note || null, selections: Object.entries(line.selections).map(([selectionGroupId, ids]) => ({ selectionGroupId, choices: ids.map(optionId => ({ optionId, quantity: 1 })) })) })) };
      const response = await auth("/api/v1/orders", { method: "POST", body: JSON.stringify(body) });
      if (!response.ok) throw new Error(t.unavailable);
      const order = await response.json() as { id: string; grossAmount: number };
      setCart([]); requestRef.current = { snapshot: "", id: "" };
      if (status !== "Draft") {
        const changed = await auth("/api/v1/orders/" + order.id + "/status", { method: "POST", body: JSON.stringify({ status: "Pending", note: null }) });
        if (!changed.ok) throw new Error(language === "ar" ? "تم حفظ الطلب. أكمل من الطلبات الحالية." : "Order saved. Continue from Current orders.");
        if (status === "Kitchen") await dispatchOrder(order.id); else { setCartOpen(false); setPayment({ orderId: order.id, total: order.grossAmount }); }
      } else setMessage(t.saved);
    } catch (e) { setMessage(e instanceof Error ? e.message : t.unavailable); }
    finally { setBusy(false); void loadHeld(); }
  }
  function confirmOfflinePayment() {
    if (!offlinePay) return;
    const method = offlineMethods.find((m) => m.id === offlinePay.methodId); if (!method) return;
    const isCash = method.kind === "Cash"; const tendered = Number(offlinePay.tendered) || 0;
    if ((isCash && tendered < total) || (!isCash && Math.abs(tendered - total) > 0.0001)) { setMessage(t.offlinePayInvalid); return; }
    persistBranch(branchId);
    enqueue({ operationType: "order.create", baseVersion: null, baseCatalogVersion: null, occurredAt: new Date().toISOString(), payload: buildOfflineOrder(channelId, cart, "Paid", { clientRequestId: createId(), paymentMethodId: method.id, amount: total, tenderedAmount: tendered }) });
    setCart([]); setOfflinePay(null); setMessage(t.offline);
  }
  const heldOrdersModal = heldOpen && <div className="fixed inset-0 z-50 grid place-items-end bg-black/35 sm:place-items-center sm:p-5"><section role="dialog" aria-modal="true" aria-label={t.heldOrders} className="max-h-[85vh] w-full max-w-md overflow-y-auto rounded-t-2xl bg-white p-5 shadow-2xl sm:rounded-2xl"><div className="flex items-center justify-between"><h2 className="text-lg font-bold">{t.heldOrders}</h2><button onClick={() => setHeldOpen(false)} className="grid size-9 place-items-center rounded-lg bg-[#f4f7f4]">×</button></div>{heldOrders.length === 0 ? <p className="mt-6 text-center text-sm text-[#69766f]">{t.noHeld}</p> : <ul className="mt-4 space-y-2">{heldOrders.map((order) => <li key={order.id} className="flex items-center justify-between gap-3 rounded-xl bg-[#f4f7f4] p-3"><div><p className="font-semibold">OMR {order.grossAmount.toFixed(3)}</p><p className="mt-0.5 text-xs text-[#69766f]">{({ Draft: language === "ar" ? "معلّق" : "Held", Pending: language === "ar" ? "بانتظار الدفع" : "Unpaid", Confirmed: language === "ar" ? "مؤكد" : "Confirmed", Paid: language === "ar" ? "مدفوع" : "Paid" } as Record<string, string>)[order.status]} · {new Date(order.createdAt).toLocaleTimeString(language)}{order.note ? ` · ${order.note}` : ""}</p></div><div className="flex flex-wrap gap-2"><button disabled={busy} onClick={() => void resumeHeld(order, true)} className="min-h-11 rounded-lg border px-3 text-sm">{language === "ar" ? "إرسال للمطبخ" : "Send to kitchen"}</button>{order.status !== "Paid" && <button disabled={busy} onClick={() => void resumeHeld(order)} className="min-h-10 shrink-0 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white">{t.send}</button>}</div></li>)}</ul>}</section></div>;
  const offlinePayModal = offlinePay && <div className="fixed inset-0 z-50 grid place-items-end bg-black/35 sm:place-items-center sm:p-5"><section role="dialog" aria-modal="true" aria-label={t.offlinePayTitle} className="w-full max-w-md rounded-t-2xl bg-[#f5f6f2] p-5 shadow-2xl sm:rounded-2xl sm:p-6"><h2 className="text-lg font-bold">{t.offlinePayTitle}</h2><p className="mt-1 text-2xl font-bold text-[#0e5a4f]">OMR {total.toFixed(3)}</p>{offlineMethods.length === 0 ? <p role="alert" className="mt-5 rounded-xl bg-[#fff5f4] p-4 text-sm text-[#9b2922]">{t.offlineNoMethods}</p> : <><label className="mt-5 block text-sm">{t.offlinePayMethod}<select value={offlinePay.methodId} onChange={(e) => setOfflinePay({ ...offlinePay, methodId: e.target.value })} className="mt-1 min-h-11 w-full rounded-lg border px-3">{offlineMethods.map((m) => <option key={m.id} value={m.id}>{name(m)}</option>)}</select></label><label className="mt-3 block text-sm">{t.offlinePayTendered}<input inputMode="decimal" value={offlinePay.tendered} onChange={(e) => setOfflinePay({ ...offlinePay, tendered: e.target.value })} className="mt-1 min-h-11 w-full rounded-lg border px-3" /></label></>}<div className="mt-5 flex gap-3"><button onClick={() => setOfflinePay(null)} className="min-h-12 flex-1 rounded-lg border">{t.offlinePayCancel}</button><button disabled={offlineMethods.length === 0} onClick={confirmOfflinePayment} className="min-h-12 flex-1 rounded-lg bg-[#0e5a4f] font-semibold text-white disabled:opacity-60">{t.offlinePayConfirm}</button></div></section></div>;
  const cartPanel = <><aside className="flex h-full min-h-0 flex-col bg-white"><div className="flex items-center justify-between border-b border-[#dfe5df] p-4"><h2 className="font-semibold">{t.cart} <span className="text-[#66736d]">{cart.length}</span></h2><button className="lg:hidden" onClick={() => setCartOpen(false)}>×</button></div><div className="min-h-0 flex-1 space-y-3 overflow-y-auto p-4">{cart.length === 0 ? <p className="py-10 text-center text-sm text-[#69766f]">{t.empty}</p> : cart.map((line) => <div key={line.key} className="rounded-xl bg-[#f4f7f4] p-3"><div className="flex justify-between gap-3"><strong>{name(line.product)}</strong><span>{(resolveOfflinePricing(line.product.pricing, lineAdjustment(line)).gross * line.quantity).toFixed(3)}</span></div><div className="mt-3 flex items-center justify-between"><div className="flex items-center gap-2"><button aria-label="Decrease" onClick={() => quantity(line.key, -1)} className="grid size-11 place-items-center rounded-lg bg-white"><Minus size={16} /></button><span className="min-w-5 text-center">{line.quantity}</span><button aria-label="Increase" onClick={() => quantity(line.key, 1)} className="grid size-9 place-items-center rounded-lg bg-white"><Plus size={16} /></button></div><input aria-label={t.notes} value={line.note} onChange={(e) => setCart(cart.map((x) => x.key === line.key ? { ...x, note: e.target.value } : x))} placeholder={t.notes} className="w-24 border-b border-[#cdd7d0] bg-transparent text-sm outline-none" /></div></div>)}</div><div className="border-t border-[#dfe5df] p-4"><div className="flex justify-between text-lg font-bold"><span>{t.total}</span><span>OMR {total.toFixed(3)}</span></div><div className="mt-4 grid grid-cols-2 gap-2"><button disabled={busy || !cart.length} onClick={() => void submit("Draft")} className="min-h-12 rounded-lg border border-[#0e5a4f] font-semibold text-[#0e5a4f]">{t.hold}</button><button disabled={busy || !cart.length} onClick={() => void submit("Pending")} className="min-h-12 rounded-lg bg-[#0e5a4f] font-semibold text-white">{t.send}</button></div><button disabled={busy || !cart.length || !online} onClick={() => void submit("Kitchen")} className="mt-2 min-h-12 w-full rounded-lg bg-[#17211f] font-semibold text-white disabled:opacity-50">{language === "ar" ? "إرسال للمطبخ" : "Send to kitchen"}</button></div></aside></>;
  return <div className="min-w-0 pb-20 lg:pb-0"><header className="flex items-center gap-2 border-b border-[#dfe5df] bg-white p-3"><div className="flex min-w-0 flex-1 items-center gap-2 overflow-x-auto"><strong className="shrink-0 text-lg sm:text-xl">{t.title}</strong><select aria-label={t.branch} disabled={cart.length > 0 || busy || !!payment} value={branchId} onChange={(e) => setBranchId(e.target.value)} className="min-h-11 shrink-0 rounded-lg border px-3 text-sm">{context?.branches.map((x) => <option key={x.id} value={x.id}>{name(x)}</option>)}</select><div role="radiogroup" aria-label={t.channel} className="flex shrink-0 gap-1.5">{context?.channels.map((x) => { const Icon = channelIcon(x.code); const selected = channelId === x.id; return <button key={x.id} type="button" role="radio" aria-checked={selected} disabled={cart.length > 0 || busy || !!payment} onClick={() => setChannelId(x.id)} className={`flex min-h-11 shrink-0 items-center gap-1.5 rounded-full px-3 text-sm font-semibold disabled:opacity-50 ${selected ? "bg-[#0e5a4f] text-white" : "bg-white"}`}><Icon size={16} />{name(x)}</button>; })}</div><button onClick={() => setHeldOpen(true)} className="min-h-9 shrink-0 rounded-full border border-[#0e5a4f] px-3 text-xs font-semibold text-[#0e5a4f]">{t.heldOrders} ({heldOrders.length})</button></div><span className={`flex shrink-0 items-center gap-1 text-xs ${online ? "text-[#137347]" : "text-[#b4322a]"}`}>{online ? t.online : <><WifiOff size={15} />{t.offline}</>}</span></header><div className={`grid min-h-[360px] lg:grid-cols-[minmax(0,1fr)_320px] lg:gap-4 xl:grid-cols-[minmax(0,1fr)_360px] ${kiosk ? "lg:h-[calc(100dvh-190px)]" : "lg:h-[calc(100dvh-260px)]"}`}><><section className="flex min-w-0 flex-col overflow-hidden"><div className="shrink-0 p-3 pb-0 sm:p-4 sm:pb-0"><label className="flex min-h-12 items-center gap-2 rounded-xl border border-[#cdd7d0] bg-white px-3"><Search size={18} /><input aria-label={t.search} onKeyDown={e => { if (e.key === "Enter") { const product = products.find(p => p.barcode === search.trim() || p.sku === search.trim()); if (product) { add(product); setSearch(""); } } }} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t.search} className="w-full bg-transparent outline-none" /></label><div className="mt-4 flex gap-2 overflow-x-auto pb-1"><button onClick={() => setCategory("")} className={`min-h-11 shrink-0 rounded-full px-4 text-sm font-semibold ${!category ? "bg-[#0e5a4f] text-white" : "bg-white"}`}>{t.all}</button>{categories.map((x) => <button key={x.id} onClick={() => setCategory(x.id)} className={`min-h-11 shrink-0 rounded-full px-4 text-sm font-semibold ${category === x.id ? "bg-[#0e5a4f] text-white" : "bg-white"}`}>{name(x)}</button>)}</div><p className="mt-3 pb-3 text-xs text-[#64716b]">{language === "ar" ? "١ اختر الأصناف · ٢ أرسل للمطبخ أو ادفع · تابع الطلب من الطلبات الحالية" : "1 Choose items · 2 Send to kitchen or pay · Follow up in Current orders"}</p></div><div className="min-h-0 flex-1 overflow-y-auto p-3 pt-0 sm:p-4 sm:pt-0">{catalogLoading && <p role="status" className="mt-4">{language === "ar" ? "جارٍ تحميل القائمة…" : "Loading menu…"}</p>}{!catalogLoading && visible.length === 0 && <p className="mt-4 rounded-xl bg-white p-6 text-sm">{language === "ar" ? "لا توجد منتجات مطابقة. جرّب بحثًا آخر، أو تحقق من تفعيل المنتجات وتوفرها في الفرع." : "No matching products. Try another search or check product availability at this branch."}</p>}<div className="mt-1 grid grid-cols-2 gap-3 sm:grid-cols-3">{visible.map((product) => <button key={product.id} disabled={busy} onClick={() => add(product)} className="min-h-36 rounded-xl border border-[#dfe5df] bg-white p-3 text-start shadow-sm transition hover:border-[#0e5a4f] hover:shadow"><ProductPhoto src={product.imageUrl} name={name(product)} className="aspect-square w-full" /><strong className="mt-3 block text-sm">{name(product)}</strong><span className="mt-1 block text-sm text-[#0e5a4f]">OMR {resolveOfflinePricing(product.pricing, 0).gross.toFixed(3)}</span></button>)}</div></div></section><div className="hidden h-full min-h-0 overflow-hidden rounded-2xl border border-[#dfe5df] shadow-sm lg:sticky lg:top-4 lg:block">{cartPanel}</div></></div><button onClick={() => setCartOpen(true)} className="fixed bottom-4 start-4 end-4 z-20 min-h-12 rounded-full bg-[#0e5a4f] px-5 font-semibold text-white shadow-lg lg:hidden">{t.viewCart} ({cart.reduce((sum, line) => sum + line.quantity, 0)}) · OMR {total.toFixed(3)}</button>{cartOpen && <div className="fixed inset-0 z-30 bg-black/35 lg:hidden"><div className="absolute inset-x-0 bottom-0 h-[82vh] rounded-t-2xl">{cartPanel}</div></div>}{customizing && <div className="fixed inset-0 z-40 grid place-items-end bg-black/35 sm:place-items-center"><section className="max-h-[85vh] w-full overflow-y-auto rounded-t-2xl bg-white p-5 sm:max-w-lg sm:rounded-2xl"><h2 className="text-lg font-bold">{name(customizing)}</h2><p className="mt-1 text-sm text-[#64716b]">{t.selections}</p>{customizing.selectionGroups.map((group) => <fieldset key={group.id} className="mt-5"><legend className="font-semibold">{name(group)} {group.isRequired ? "*" : ""}</legend><div className="mt-2 space-y-2">{group.options.map((option) => <label key={option.id} className="flex min-h-11 items-center justify-between rounded-lg bg-[#f4f7f4] px-3"><span><input type="checkbox" checked={selections[group.id]?.includes(option.id) ?? false} onChange={() => setSelections({ ...selections, [group.id]: selections[group.id]?.includes(option.id) ? selections[group.id].filter((x) => x !== option.id) : [...(selections[group.id] ?? []), option.id].slice(-group.maxSelections) })} className="me-2" />{name(option)}</span><span>+{option.priceAdjustment.toFixed(3)}</span></label>)}</div></fieldset>)}<div className="mt-6 flex gap-3"><button onClick={() => setCustomizing(null)} className="min-h-12 flex-1 rounded-lg border">×</button><button onClick={confirm} className="min-h-12 flex-1 rounded-lg bg-[#0e5a4f] font-semibold text-white">{t.confirm}</button></div></section></div>}{payment && <PaymentDialog language={language} orderId={payment.orderId} branchId={branchId} total={payment.total} onClose={() => { setPayment(null); void loadHeld(); }} />}{qrToastsNode}{offlinePayModal}{heldOrdersModal}{message && <p role="status" className="fixed bottom-20 left-1/2 z-50 w-[min(90vw,560px)] -translate-x-1/2 rounded-xl bg-[#17211f] px-4 py-3 text-sm text-white">{message}</p>}</div>;
}

// Mirrors backend PricingRules.Resolve exactly (3-decimal money, away-from-zero) so an offline sale
// carries the same tax the server would have computed online, instead of a stale/zeroed snapshot.
function roundMoney(value: number) { return Math.round((value + Number.EPSILON) * 1000) / 1000; }

function resolveOfflinePricing(pricing: Pricing, adjustment: number) {
  const listPrice = roundMoney(pricing.listPrice + adjustment);
  const discount = roundMoney(Math.min(listPrice, listPrice * pricing.discountRate));
  const taxable = roundMoney(listPrice - discount);
  const rate = pricing.taxRate;
  let net: number, gross: number;
  if (pricing.taxCalculationMode === "Inclusive") { net = roundMoney(taxable / (1 + rate / 100)); gross = taxable; }
  else { net = taxable; gross = roundMoney(net + roundMoney((net * rate) / 100)); }
  const taxAmount = roundMoney(gross - net);
  return { listPrice, discount, net, taxAmount, gross };
}

function buildOfflineOrder(channelId: string, cartLines: CartLine[], status: "Draft" | "Paid", payment?: { clientRequestId: string; paymentMethodId: string; amount: number; tenderedAmount: number }) {
  return {
    salesChannelId: channelId,
    source: "Pos",
    status,
    note: null,
    payment: status === "Paid" ? payment : null,
    lines: cartLines.map((line) => {
      const selectionsSnapshot = JSON.stringify(
        Object.entries(line.selections).map(([groupId, ids]) => ({ groupId, options: ids }))
      );
      const adjustment = Object.entries(line.selections).flatMap(([groupId, ids]) =>
        ids.map((optionId) => line.product.selectionGroups.find((group) => group.id === groupId)?.options.find((option) => option.id === optionId)?.priceAdjustment ?? 0)
      ).reduce((a, b) => a + b, 0);
      const { pricing } = line.product;
      const resolved = resolveOfflinePricing(pricing, adjustment);
      return {
        productId: line.product.id,
        productNameAr: line.product.nameAr,
        productNameEn: line.product.nameEn,
        quantity: line.quantity,
        note: line.note || null,
        selectionsSnapshot,
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
