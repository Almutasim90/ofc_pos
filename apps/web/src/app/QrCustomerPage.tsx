import { useEffect, useMemo, useRef, useState } from "react";
import { Check, ChefHat, Clock, Languages, Minus, Plus, RefreshCw, ShoppingBag, Trash2, X } from "lucide-react";
import { createId, store } from "@/lib/local-store";

type Language = "ar" | "en";
type Option = { id: string; productId: string; nameAr: string; nameEn: string; priceAdjustment: number; isDefault: boolean; maxQuantity: number };
type Group = { id: string; kind: "Combo" | "Modifier"; nameAr: string; nameEn: string; isRequired: boolean; minSelections: number; maxSelections: number; options: Option[] };
type MenuProduct = { id: string; categoryId: string; categoryNameAr: string; categoryNameEn: string; sku: string; nameAr: string; nameEn: string; type: "Simple" | "Combo" | "Service"; basePrice: number | null; listAmount: number; discountAmount: number; netAmount: number; taxAmount: number; grossAmount: number; imageUrl: string | null; selectionGroups: Group[] };
type Context = { code: string; kind: string; nameAr: string; nameEn: string; branchId: string; branchCode: string; branchNameAr: string; branchNameEn: string; salesChannelId: string; salesChannelCode: string; salesChannelNameAr: string; salesChannelNameEn: string; approvalMode: string; requiresApproval: boolean };
type Choice = { optionId: string; quantity: number };
type Selection = { selectionGroupId: string; choices: Choice[] };
type CartLine = { key: string; product: MenuProduct; quantity: number; note: string; selections: Selection[]; unitGrossAmount: number };
type OrderResult = { id: string; clientRequestId: string; status: string; grossAmount: number; requiresApproval: boolean; approvalStatus: string | null };

const copy = {
  ar: {
    order: "اطلب الآن", menu: "القائمة", loading: "جارٍ تحميل القائمة...", error: "تعذر تحميل القائمة. تحقق من رمز الطاولة.", retry: "إعادة المحاولة", empty: "لا توجد أصناف متاحة حاليًا", cart: "السلة", total: "الإجمالي", add: "أضف", emptyCart: "سلتك فارغة", required: "أجب عن الحقول المطلوبة", customize: "اختر المكونات", confirm: "تأكيد", note: "ملاحظة (اختياري)", name: "الاسم", close: "إغلاق", submit: "إرسال الطلب", submitting: "جارٍ الإرسال", submitted: "تم استلام طلبك", orderNo: "رقم الطلب", status: "الحالة", amount: "المبلغ", tracking: "تتبع الطلب", phone: "رقم الهاتف (اختياري)", walkIn: "زائر", back: "العودة للقائمة", approved: "مقبول", pending: "بانتظار الاعتماد", rejected: "مرفوض", waiting: "بانتظار بدء التحضير", preparing: "قيد التحضير", ready: "جاهز", completed: "مكتمل", cancelled: "ملغى", paid: "مدفوع", viewOrder: "عرض الطلب", language: "English", currency: "ر.ع", optionalSelections: "إضافات", invalid: "لا يمكنك طلب هذا الصنف الآن", contact: "سيتصل بك الموظف عند الجاهزية", quantity: "الكمية", requiredNote: "هذا اختيار إلزامي"
  } as const,
  en: {
    order: "Order now", menu: "Menu", loading: "Loading menu...", error: "Unable to load the menu. Check the table QR code.", retry: "Retry", empty: "No items available right now", cart: "Cart", total: "Total", add: "Add", emptyCart: "Your cart is empty", required: "Complete the required fields", customize: "Choose your options", confirm: "Confirm", note: "Note (optional)", name: "Name", close: "Close", submit: "Submit order", submitting: "Submitting", submitted: "Your order was received", orderNo: "Order", status: "Status", amount: "Amount", tracking: "Track order", phone: "Phone (optional)", walkIn: "Walk-in", back: "Back to menu", approved: "Approved", pending: "Awaiting approval", rejected: "Rejected", waiting: "Awaiting kitchen", preparing: "Preparing", ready: "Ready", completed: "Completed", cancelled: "Cancelled", paid: "Paid", viewOrder: "View order", language: "العربية", currency: "OMR", optionalSelections: "Extras", invalid: "This item cannot be ordered now", contact: "A staff member will call you when ready", quantity: "Qty", requiredNote: "This is a required choice"
  } as const,
};

const statusEn = { Pending: "Pending", Confirmed: "Confirmed", Paid: "Paid", SentToKitchen: "SentToKitchen", Preparing: "Preparing", Ready: "Ready", Completed: "Completed", Cancelled: "Cancelled", Rejected: "Rejected" } as const;

export function QrCustomerPage({ code }: { code: string }) {
  const [language, setLanguage] = useState<Language>(() => (store.get<Language>("qr-lang") === "en" ? "en" : "ar"));
  const [context, setContext] = useState<Context | null>(null);
  const [products, setProducts] = useState<MenuProduct[]>([]);
  const [state, setState] = useState<"loading" | "ready" | "error">("loading");
  const [category, setCategory] = useState("all");
  const [cart, setCart] = useState<CartLine[]>([]);
  const [orderNote, setOrderNote] = useState("");
  const [customerPhone, setCustomerPhone] = useState(() => store.get<string>("qr-customer-phone") ?? "");
  const [editing, setEditing] = useState<{ product: MenuProduct; choices: Record<string, Record<string, number>> } | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<{ text: string; error: boolean } | null>(null);
  const [result, setResult] = useState<OrderResult | null>(null);
  const [cartOpen, setCartOpen] = useState(false);
  const [selectedCopies, setSelectedCopies] = useState(1);
  const orderRequestId = useRef<string | null>(null);
  const t = copy[language];

  useEffect(() => { document.documentElement.lang = language; document.documentElement.dir = language === "ar" ? "rtl" : "ltr"; store.set("qr-lang", language); }, [language]);

  async function load() {
    setState("loading");
    setMessage(null);
    try {
      const [contextResponse, menuResponse] = await Promise.all([fetch(`/api/v1/qr/${code}`), fetch(`/api/v1/qr/${code}/menu`)]);
      if (!contextResponse.ok || !menuResponse.ok) throw new Error();
      const ctx = await contextResponse.json() as Context;
      const menu = await menuResponse.json() as { products: MenuProduct[] };
      setContext(ctx);
      setProducts(menu.products);
      setState("ready");
      const saved = store.get<string>("qr-" + code + "-order");
      if (saved) { const parsed = JSON.parse(saved) as { clientRequestId: string }; setResult({ id: "", clientRequestId: parsed.clientRequestId, status: "Pending", grossAmount: 0, requiresApproval: ctx.requiresApproval, approvalStatus: "Pending" }); void refreshOrder(parsed.clientRequestId); }
    } catch {
      setState("error");
    }
  }

  useEffect(() => { void load(); }, []);

  const categories = useMemo(() => {
    const seen = new Map<string, { nameAr: string; nameEn: string }>();
    products.forEach((p) => { if (!seen.has(p.categoryId)) seen.set(p.categoryId, { nameAr: p.categoryNameAr, nameEn: p.categoryNameEn }); });
    return [{ id: "all", nameAr: t.menu, nameEn: t.menu }, ...Array.from(seen.entries()).map(([id, n]) => ({ id, ...n }))];
  }, [products, t.menu]);

  const shown = useMemo(() => category === "all" ? products : products.filter((p) => p.categoryId === category), [products, category]);
  const cartCount = cart.reduce((s, l) => s + l.quantity, 0);
  const cartTotal = Math.round(cart.reduce((s, l) => s + l.unitGrossAmount * l.quantity, 0) * 1000) / 1000;

  function selectionTotal(product: MenuProduct, choices: Record<string, Record<string, number>>) {
    let total = product.grossAmount;
    for (const group of product.selectionGroups) {
      for (const choice of Object.entries(choices[group.id] ?? {})) {
        const option = group.options.find((o) => o.id === choice[0]);
        if (option) total += option.priceAdjustment * choice[1];
      }
    }
    return Math.round(total * 1000) / 1000;
  }

  function defaultChoices(product: MenuProduct): Record<string, Record<string, number>> {
    const out: Record<string, Record<string, number>> = {};
    for (const group of product.selectionGroups) {
      const selected = group.options.find((o) => o.isDefault && (group.maxSelections === 1 || group.minSelections <= 1));
      if (selected) out[group.id] = { [selected.id]: 1 };
    }
    return out;
  }

  function openProduct(product: MenuProduct) {
    if (product.selectionGroups.some((g) => g.isRequired)) {
      setEditing({ product, choices: defaultChoices(product) });
      setSelectedCopies(1);
    } else {
      addLine(product, 1, {});
    }
  }

  function addLine(product: MenuProduct, quantity: number, choices: Record<string, Record<string, number>>) {
    const selections: Selection[] = Object.entries(choices).map(([groupId, picked]) => ({ selectionGroupId: groupId, choices: Object.entries(picked).map(([optionId, qty]) => ({ optionId, quantity: qty })) }));
    const key = product.id + ":" + JSON.stringify(selections);
    const unit = selectionTotal(product, choices);
    setCart((prev) => {
      const existing = prev.find((l) => l.key === key);
      const next = existing ? prev.map((l) => (l.key === key ? { ...l, quantity: l.quantity + quantity } : l)) : [...prev, { key, product, quantity, note: "", selections, unitGrossAmount: unit }];
      return next;
    });
    setEditing(null);
    setMessage(null);
  }

  function bump(key: string, delta: number) { setCart((prev) => prev.map((l) => (l.key === key ? { ...l, quantity: Math.max(1, Math.min(99, l.quantity + delta)) } : l))); }
  function removeLine(key: string) { setCart((prev) => prev.filter((l) => l.key !== key)); }
  function setLineNote(key: string, note: string) { setCart((prev) => prev.map((l) => (l.key === key ? { ...l, note } : l))); }

  function validSelection(group: Group, picked: Record<string, number>) {
    const options = Object.entries(picked).filter(([, qty]) => qty > 0);
    const count = options.reduce((s, [, qty]) => s + qty, 0);
    if (group.isRequired && count < 1) return false;
    if (count < group.minSelections || count > group.maxSelections) return false;
    return options.every(([optionId, qty]) => { const option = group.options.find((o) => o.id === optionId)!; return qty <= option.maxQuantity; });
  }

  function toggleChoice(group: Group, optionId: string) {
    if (!editing) return;
    setEditing((current) => {
      if (!current) return current;
      const picked = { ...current.choices[group.id] ?? {} };
      const currentQty = picked[optionId] ?? 0;
      const nextQty = currentQty >= (group.options.find((o) => o.id === optionId)?.maxQuantity ?? 1) ? 0 : currentQty + 1;
      const updated = { ...picked, [optionId]: nextQty };
      if (nextQty === 0) delete updated[optionId];
      const count = Object.values(updated).reduce((s, q) => s + q, 0);
      if (group.maxSelections === 1) {
        return { ...current, choices: { ...current.choices, [group.id]: nextQty > 0 ? { [optionId]: 1 } : {} } };
      }
      if (count > group.maxSelections) return current;
      return { ...current, choices: { ...current.choices, [group.id]: updated } };
    });
  }

  const editingValid = editing ? editing.product.selectionGroups.every((group) => validSelection(group, editing.choices[group.id] ?? {})) : false;

  async function submitOrder() {
    if (cart.length === 0) { setMessage({ text: t.emptyCart, error: true }); return; }
    if (message?.error) return;
    setSubmitting(true);
    setMessage(null);
    const requestId = orderRequestId.current ?? createId();
    orderRequestId.current = requestId;
    const body = {
      clientRequestId: requestId,
      note: orderNote.trim() || null,
      customer: customerPhone.trim() ? { phone: customerPhone.trim() } : null,
      lines: cart.map((line) => ({ productId: line.product.id, quantity: line.quantity, note: line.note.trim() || null, selections: line.selections.length ? line.selections.map((s) => ({ selectionGroupId: s.selectionGroupId, choices: s.choices })) : null })),
    };
    try {
      const response = await fetch(`/api/v1/qr/${code}/orders`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response.ok) throw new Error(t.invalid);
      const value = await response.json() as { clientRequestId: string; grossAmount: number; status: string; requiresApproval?: boolean; approval?: { status: string } | null };
      orderRequestId.current = null;
      store.set("qr-" + code + "-order", { clientRequestId: value.clientRequestId });
      setResult({ id: value.clientRequestId, clientRequestId: value.clientRequestId, status: value.status, grossAmount: value.grossAmount, requiresApproval: value.requiresApproval ?? context?.requiresApproval ?? false, approvalStatus: value.approval?.status ?? null });
      setCart([]);
      setOrderNote("");
      setSubmitting(false);
    } catch (e) {
      setSubmitting(false);
      setMessage({ text: e instanceof Error ? e.message : t.error, error: true });
    }
  }

  async function refreshOrder(clientRequestId: string) {
    // Inferred status refresh from the track endpoint; kept lightweight.
    try {
      const response = await fetch(`/api/v1/qr/${code}/orders/${clientRequestId}`);
      if (response.ok) {
        const value = await response.json() as { status: string; approval: { status: string } | null };
        setResult((prev) => prev ? { ...prev, status: value.status, approvalStatus: value.approval?.status ?? null } : prev);
      }
    } catch {
      // Ignore transient polling failures.
    }
  }

  useEffect(() => {
    if (!result) return;
    const timer = window.setInterval(() => void refreshOrder(result.clientRequestId), 6000);
    return () => window.clearInterval(timer);
  }, [result?.clientRequestId]);

  const languageToggle = <button onClick={() => setLanguage(language === "ar" ? "en" : "ar")} className="inline-flex min-h-11 items-center gap-2 rounded-lg px-3 text-sm font-medium text-[#0e5a4f] hover:bg-[#edf5f1]"><Languages size={18} />{t.language}</button>;

  return (
    <main className="min-h-screen bg-[#f5f6f2] text-[#17211f]" dir={language === "ar" ? "rtl" : "ltr"}>
      <header className="sticky top-0 z-10 border-b border-[#dfe5df] bg-white/95 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-3 px-4 py-3">
          <div className="flex items-center gap-3">
            <span className="grid size-10 place-items-center rounded-xl bg-[#0e5a4f] font-bold text-white">O</span>
            <div>
              <p className="text-sm font-semibold leading-tight">{context ? (language === "ar" ? context.nameAr : context.nameEn) : "OFC"}</p>
              <p className="text-xs text-[#66736d]">{context ? (language === "ar" ? context.branchNameAr : context.branchNameEn) : ""}</p>
            </div>
          </div>
          {languageToggle}
        </div>
      </header>

      {state === "loading" && <div className="grid min-h-[70vh] place-items-center"><div className="flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div></div>}
      {state === "error" && <div role="alert" className="mx-auto max-w-md rounded-2xl border border-[#efc5c1] bg-[#fff5f4] p-6 mt-16 text-center text-[#9b2922]"><p>{t.error}</p><button onClick={() => void load()} className="mt-4 font-semibold underline">{t.retry}</button></div>}

      {state === "ready" && !result && (
        <div className="mx-auto max-w-6xl px-4 pb-28 sm:pb-10">
          {context && <div className="mt-4 flex flex-wrap items-center gap-2 rounded-xl border border-[#dfe5df] bg-white p-3 text-sm">
            <span className="rounded-full bg-[#e6f1ec] px-3 py-1 text-xs font-semibold text-[#08483f]">{language === "ar" ? context.branchNameAr : context.branchNameEn}</span>
            <span className="text-[#66736d]">{language === "ar" ? "طاولة" : "Table"} · {context.code}</span>
            {context.requiresApproval && <span className="rounded-full bg-[#f4f1e3] px-3 py-1 text-xs font-semibold text-[#8a6d1f]">{t.pending}</span>}
          </div>}

          <nav className="scrollbar-none mt-4 flex gap-2 overflow-x-auto pb-1">
            {categories.map((c) => <button key={c.id} onClick={() => setCategory(c.id)} className={`min-h-10 shrink-0 rounded-lg px-4 text-sm font-medium ${category === c.id ? "bg-[#0e5a4f] text-white" : "bg-white text-[#53615b] border border-[#dfe5df]"}`}>{language === "ar" ? c.nameAr : c.nameEn}</button>)}
          </nav>

          {shown.length === 0 ? <div className="mt-10 text-center text-[#69766f]"><ShoppingBag className="mx-auto mb-3 text-[#0e5a4f]" />{t.empty}</div> : (
            <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
              {shown.map((product) => (
                <button key={product.id} onClick={() => openProduct(product)} className="group flex flex-col rounded-2xl border border-[#dfe5df] bg-white p-3 text-start transition hover:border-[#0e5a4f]/40 hover:shadow-sm">
                  <div className="flex aspect-square w-full items-center justify-center overflow-hidden rounded-xl bg-[#edf5f1] text-[#0e5a4f]">{product.imageUrl ? <img src={product.imageUrl} alt={product.nameAr} className="h-full w-full object-cover" /> : <ChefHat size={30} />}</div>
                  <p className="mt-3 line-clamp-1 text-sm font-semibold">{language === "ar" ? product.nameAr : product.nameEn}</p>
                  <p className="mt-1 text-sm font-bold text-[#0e5a4f]">{new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(product.listAmount)} <span className="text-xs font-medium text-[#66736d]">{t.currency}</span></p>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {state === "ready" && result && (
        <div className="mx-auto max-w-xl px-4 py-10">
          <div className="rounded-2xl border border-[#dfe5df] bg-white p-6 text-center">
            <div className={`mx-auto grid size-16 place-items-center rounded-full ${result.status === "Cancelled" || result.status === "Rejected" ? "bg-[#fbe4e2] text-[#b4322a]" : "bg-[#e3f4ea] text-[#137347]"}`}><Check size={30} /></div>
            <h1 className="mt-4 text-2xl font-semibold">{t.submitted}</h1>
            <p className="mt-2 text-sm text-[#66736d]">{t.orderNo}: <strong className="text-[#17211f]">{result.clientRequestId.slice(0, 8)}</strong></p>
            <div className="mt-5 rounded-xl bg-[#f7faf7] p-4 text-start">
              <p className="text-xs font-semibold text-[#66736d]">{t.status}</p>
              <p className="mt-1 flex items-center gap-2 text-lg font-semibold"><Clock size={18} className="text-[#0e5a4f]" />{statusLabel(language, result.status, result.approvalStatus)}</p>
            </div>
            <p className="mt-4 text-sm text-[#66736d]">{t.contact}</p>
            <button onClick={() => setResult(null)} className="mt-5 min-h-11 rounded-lg bg-[#0e5a4f] px-5 font-semibold text-white">{t.back}</button>
          </div>
        </div>
      )}

      {state === "ready" && !result && cart.length > 0 && (
        <div className="fixed inset-x-0 bottom-0 z-20 border-t border-[#dfe5df] bg-white px-4 py-3 sm:hidden">
          <div className="mx-auto flex max-w-6xl items-center justify-between gap-3">
            <div><p className="text-xs text-[#66736d]">{t.cart} · {cartCount}</p><p className="text-lg font-bold">{new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(cartTotal)} {t.currency}</p></div>
            <button onClick={() => setCartOpen(true)} className="min-h-11 rounded-lg bg-[#0e5a4f] px-5 font-semibold text-white">{t.order} <ShoppingBag className="inline" size={16} /></button>
          </div>
        </div>
      )}

      {editing && (
        <div className="fixed inset-0 z-30 flex items-end justify-center bg-black/30 p-0 sm:items-center sm:p-4" onClick={() => setEditing(null)}>
          <div className="max-h-[90vh] w-full max-w-md overflow-y-auto rounded-t-2xl bg-white p-5 sm:rounded-2xl" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-start justify-between"><h2 className="text-lg font-semibold">{language === "ar" ? editing.product.nameAr : editing.product.nameEn}</h2><button onClick={() => setEditing(null)} className="min-h-10 rounded-lg p-1 text-[#66736d] hover:bg-[#f2f5f2]"><X size={20} /></button></div>
            <p className="mt-1 text-sm text-[#0e5a4f] font-bold">{new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(selectionTotal(editing.product, editing.choices))} {t.currency}</p>
            <div className="mt-4 space-y-5">
              {editing.product.selectionGroups.map((group) => (
                <div key={group.id}>
                  <div className="flex items-center justify-between"><p className="text-sm font-medium">{group.isRequired ? <span>{language === "ar" ? group.nameAr : group.nameEn} <span className="text-[#b4322a]">*</span></span> : <span>{language === "ar" ? group.nameAr : group.nameEn}</span>}</p><p className="text-xs text-[#66736d]">{group.maxSelections > 1 ? `${group.minSelections}-${group.maxSelections}` : t.required}</p></div>
                  <div className="mt-2 space-y-2">
                    {group.options.map((option) => {
                      const qty = (editing.choices[group.id] ?? {})[option.id] ?? 0;
                      return (
                        <div key={option.id} onClick={() => toggleChoice(group, option.id)} className={`flex cursor-pointer items-center justify-between rounded-xl border p-3 ${qty > 0 ? "border-[#0e5a4f] bg-[#e6f1ec]" : "border-[#dfe5df] bg-white"}`}>
                          <span className="text-sm">{language === "ar" ? option.nameAr : option.nameEn} {option.priceAdjustment > 0 && <span className="text-[#0e5a4f] font-semibold">+{option.priceAdjustment}</span>}</span>
                          <span className={`grid size-6 place-items-center rounded-full text-xs font-bold ${qty > 0 ? "bg-[#0e5a4f] text-white" : "border border-[#dfe5df] text-[#66736d]"}`}>{qty > 0 ? qty : ""}</span>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
            <div className="mt-5 flex items-center justify-between">
              <p className="text-sm text-[#66736d]">{t.quantity}</p>
              <div className="flex items-center gap-3">
                <button onClick={() => setSelectedCopies((v) => Math.max(1, v - 1))} className="grid size-9 place-items-center rounded-lg border border-[#dfe5df] text-[#0e5a4f]"><Minus size={16} /></button>
                <span className="w-6 text-center font-semibold">{selectedCopies}</span>
                <button onClick={() => setSelectedCopies((v) => Math.min(99, v + 1))} className="grid size-9 place-items-center rounded-lg border border-[#dfe5df] text-[#0e5a4f]"><Plus size={16} /></button>
              </div>
            </div>
            <button disabled={!editingValid} onClick={() => addLine(editing.product, selectedCopies, editing.choices)} className="mt-5 min-h-12 w-full rounded-lg bg-[#0e5a4f] font-semibold text-white disabled:opacity-40">{t.confirm} · {new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(selectionTotal(editing.product, editing.choices) * selectedCopies)} {t.currency}</button>
          </div>
        </div>
      )}

      {cartOpen && (
        <div className="fixed inset-0 z-30 flex items-end justify-center bg-black/30 sm:items-center sm:p-4" onClick={() => setCartOpen(false)}>
          <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-t-2xl bg-white p-5 sm:rounded-2xl" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center justify-between"><h2 className="text-lg font-semibold">{t.cart}</h2><button onClick={() => setCartOpen(false)} className="min-h-10 rounded-lg p-1 text-[#66736d] hover:bg-[#f2f5f2]"><X size={20} /></button></div>
            {cart.length === 0 ? <p className="mt-10 text-center text-[#69766f]">{t.emptyCart}</p> : (
              <ul className="mt-4 space-y-3">
                {cart.map((line) => (
                  <li key={line.key} className="rounded-xl border border-[#e8ece8] p-3">
                    <div className="flex items-start justify-between gap-3"><div className="min-w-0"><p className="text-sm font-medium">{language === "ar" ? line.product.nameAr : line.product.nameEn}</p>{line.selections.length > 0 && <p className="mt-1 text-xs text-[#66736d]">{line.selections.map((s) => s.choices.map((c) => { const grp = line.product.selectionGroups.find((g) => g.id === s.selectionGroupId); const opt = grp?.options.find((o) => o.id === c.optionId); return opt ? (language === "ar" ? opt.nameAr : opt.nameEn) : ""; }).join(", ")).join(" · ")}</p>}<input value={line.note} onChange={(e) => setLineNote(line.key, e.target.value)} placeholder={t.note} className="mt-2 w-full min-h-10 rounded-lg border border-[#cdd7d0] px-3 text-sm" /></div><div className="flex flex-col items-end gap-2"><div className="flex items-center gap-2"><button onClick={() => bump(line.key, -1)} className="grid size-8 place-items-center rounded-lg border border-[#dfe5df] text-[#0e5a4f]"><Minus size={14} /></button><span className="w-5 text-center text-sm font-semibold">{line.quantity}</span><button onClick={() => bump(line.key, 1)} className="grid size-8 place-items-center rounded-lg border border-[#dfe5df] text-[#0e5a4f]"><Plus size={14} /></button></div><button onClick={() => removeLine(line.key)} className="text-[#b4322a]"><Trash2 size={16} /></button></div></div>
                    <p className="mt-2 text-end text-sm font-semibold">{new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(line.unitGrossAmount * line.quantity)} {t.currency}</p>
                  </li>
                ))}
              </ul>
            )}
            <div className="mt-5 rounded-xl bg-[#f7faf7] p-4">
              <label className="block text-sm font-medium">{t.note}<textarea value={orderNote} onChange={(e) => setOrderNote(e.target.value)} maxLength={500} className="mt-2 min-h-16 w-full rounded-lg border border-[#cdd7d0] px-3 py-2 text-sm outline-none focus:border-[#0e5a4f]" /></label>
              <label className="mt-4 block text-sm font-medium">{t.phone}<input value={customerPhone} onChange={(e) => setCustomerPhone(e.target.value)} maxLength={30} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 text-sm outline-none focus:border-[#0e5a4f]" /></label>
              <div className="mt-4 flex items-center justify-between"><span className="text-sm text-[#66736d]">{t.total}</span><span className="text-xl font-bold">{new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(cartTotal)} {t.currency}</span></div>
              {message && <p role={message.error ? "alert" : "status"} className={`mt-3 text-sm ${message.error ? "text-[#b4322a]" : "text-[#137347]"}`}>{message.text}</p>}
              <button disabled={submitting || cart.length === 0} onClick={() => void submitOrder()} className="mt-4 min-h-12 w-full rounded-lg bg-[#0e5a4f] font-semibold text-white disabled:opacity-50">{submitting ? t.submitting : t.submit}</button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

function statusLabel(language: Language, status: string, approvalStatus: string | null) {
  const t = copy[language];
  if (status === "Pending" && approvalStatus === "Pending") return t.pending;
  const map: Record<string, string> = { Confirmed: t.approved, Paid: t.paid, SentToKitchen: t.waiting, Preparing: t.preparing, Ready: t.ready, Completed: t.completed, Cancelled: t.cancelled, Rejected: t.rejected };
  return map[status] ?? statusEn[status as keyof typeof statusEn] ?? status;
}
