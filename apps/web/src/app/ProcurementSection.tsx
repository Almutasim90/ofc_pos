import { useEffect, useState } from "react";
import { Plus, RefreshCw, Save, Truck, Recycle, BadgeCheck, XCircle, Eye } from "lucide-react";
import { createId, store } from "@/lib/local-store";

type Language = "ar" | "en";
type Branch = { id: string; nameAr: string; nameEn: string };
type Supplier = { id: string; code: string; nameAr: string; nameEn: string; vatNumber: string | null; phone: string | null; email: string | null; contactPerson: string | null; address: string | null; notes: string | null; isActive: boolean; createdAt: string };
type Uom = { id: string; code: string; nameAr: string; nameEn: string };
type Item = { id: string; sku: string; nameAr: string; nameEn: string; baseUnitId: string };
type PurchaseOrder = { id: string; number: string; supplierId: string; supplierCode: string | null; supplierNameAr: string | null; supplierNameEn: string | null; branchId: string; status: "Draft" | "Submitted" | "Approved" | "Rejected" | "Cancelled" | "Received"; expectedDate: string | null; reference: string | null; totalAmount: number; lineCount: number; createdAt: string; receivedAt: string | null };
type GoodsReceipt = { id: string; number: string; supplierId: string; supplierCode: string | null; supplierNameAr: string | null; supplierNameEn: string | null; branchId: string; purchaseOrderId: string | null; status: "Draft" | "Posted"; reference: string | null; postedAt: string | null; totalAmount: number; lineCount: number; createdAt: string };
type HistoryRow = { receiptNumber: string; postedAt: string; branchId: string; inventoryItemId: string; itemNameAr: string | null; itemNameEn: string | null; quantity: number; unitCode: string | null; unitCost: number; totalAmount: number };
type CostSummary = { inventoryItemId: string; latestUnitCost: number; lastReceivedAt: string; unitCode: string | null; receivedQuantity: number; totalAmount: number };
type SupplierHistory = { supplier: Supplier; summary: { receiptCount: number; lineCount: number; totalAmount: number }; history: HistoryRow[]; itemCostSummary: CostSummary[] };

function LineDef(quantity?: string, unitCost?: string) { return { inventoryItemId: "", unitId: "", quantity: quantity ?? "", unitCost: unitCost ?? "" }; }

const copy = {
  ar: {
    title: "المشتريات والموردون", intro: "ربط دخول المخزون بالموردين عبر الموردين وطلبات الشراء وسندات الاستلام، مع تتبع تاريخ تكلفة كل مورد.", intro2: "استلام البضاعة يسجّل حركة مخزنية فورية ويرفع الرصيد ويحدّث متوسط التكلفة.", loading: "جارٍ التحميل", reload: "تحديث", saved: "تم الحفظ بنجاح.", failed: "تعذر تنفيذ العملية.", empty: "لا توجد بيانات بعد.", none: "لا يوجد", branch: "الفرع / المستودع",
    suppliers: "الموردون", addSupplier: "إضافة مورد", code: "الرمز", nameAr: "الاسم بالعربية", nameEn: "الاسم بالإنجليزية", contactPerson: "جهة الاتصال", phone: "الهاتف", email: "البريد الإلكتروني", vatNumber: "رقم الضريبة", address: "العنوان", notes: "ملاحظات", active: "نشط", inactive: "غير نشط", viewHistory: "سجل المورد", add: "إضافة",
    purchaseOrders: "أوامر الشراء", addPo: "إضافة أمر شراء", supplier: "المورد", product: "الصنف", unit: "الوحدة", quantity: "الكمية", unitCost: "تكلفة الوحدة", addLine: "إضافة سطر", remove: "إزالة", create: "إنشاء", submit: "تقديم", approve: "اعتماد", reject: "رفض", cancelOrder: "إلغاء", receive: "استلام", lines: "أسطر", total: "الإجمالي", expectedDate: "التاريخ المتوقع", status: "الحالة",
    poDraft: "مسودة", poSubmitted: "مقدم", poApproved: "معتمد", poRejected: "مرفوض", poCancelled: "ملغي", poReceived: "مستلم",
    goodsReceipts: "سندات الاستلام", addGr: "إضافة سند استلام", reference: "مرجع", post: "ترحيل الاستلام", posted: "مرحّل", draft: "مسودة",
    history: "سجل المشتريات وتكلفة المورد", receiptsCount: "سندات", linesCount: "أسطر", totalAmount: "إجمالي المبلغ", latestCosts: "أحدث التكاليف", latestCost: "أحدث تكلفة", receivedQty: "كمية مستقبلة", lastReceived: "آخر استلام"
  } as const,
  en: {
    title: "Procurement & suppliers", intro: "Link stock intake to suppliers through vendors, purchase orders, and goods receipts, while tracing each supplier's cost history.", intro2: "Receiving goods posts an instant inventory movement, raises balance, and refreshs the weighted average cost.", loading: "Loading", reload: "Refresh", saved: "Saved successfully.", failed: "Unable to complete the operation.", empty: "No data yet.", none: "None", branch: "Branch / warehouse",
    suppliers: "Suppliers", addSupplier: "Add supplier", code: "Code", nameAr: "Arabic name", nameEn: "English name", contactPerson: "Contact person", phone: "Phone", email: "Email", vatNumber: "VAT number", address: "Address", notes: "Notes", active: "Active", inactive: "Inactive", viewHistory: "Supplier history", add: "Add",
    purchaseOrders: "Purchase orders", addPo: "Add purchase order", supplier: "Supplier", product: "Item", unit: "Unit", quantity: "Quantity", unitCost: "Unit cost", addLine: "Add line", remove: "Remove", create: "Create", submit: "Submit", approve: "Approve", reject: "Reject", cancelOrder: "Cancel", receive: "Receive", lines: "Lines", total: "Total", expectedDate: "Expected date", status: "Status",
    poDraft: "Draft", poSubmitted: "Submitted", poApproved: "Approved", poRejected: "Rejected", poCancelled: "Cancelled", poReceived: "Received",
    goodsReceipts: "Goods receipts", addGr: "Add goods receipt", reference: "Reference", post: "Post receipt", posted: "Posted", draft: "Draft",
    history: "Purchase history & supplier cost", receiptsCount: "receipts", linesCount: "lines", totalAmount: "Total amount", latestCosts: "Latest costs", latestCost: "Latest cost", receivedQty: "Received qty", lastReceived: "Last received"
  } as const,
} as const;

export function ProcurementSection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) => (language === "ar" ? x.nameAr : x.nameEn);
  const fmt = (v: number | null | undefined) => v === null || v === undefined ? "—" : new Intl.NumberFormat(language, { maximumFractionDigits: 4 }).format(v);
  const qty = (v: number | null | undefined) => v === null || v === undefined ? "—" : new Intl.NumberFormat(language, { maximumFractionDigits: 6 }).format(v);

  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [uoms, setUoms] = useState<Uom[]>([]);
  const [orders, setOrders] = useState<PurchaseOrder[]>([]);
  const [receipts, setReceipts] = useState<GoodsReceipt[]>([]);
  const [history, setHistory] = useState<SupplierHistory | null>(null);
  const [historySupplierId, setHistorySupplierId] = useState("");
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);

  const [supplierForm, setSupplierForm] = useState({ code: "", nameAr: "", nameEn: "", contactPerson: "", phone: "", email: "", vatNumber: "", address: "", notes: "" });
  const [poForm, setPoForm] = useState({ supplierId: "", expectedDate: "", reference: "" });
  const [poLines, setPoLines] = useState<{ inventoryItemId: string; unitId: string; quantity: string; unitCost: string }[]>([LineDef()]);
  const [grForm, setGrForm] = useState({ supplierId: "", purchaseOrderId: "", reference: "", notes: "" });
  const [grLines, setGrLines] = useState<{ inventoryItemId: string; unitId: string; quantity: string; unitCost: string }[]>([LineDef()]);

  const setMsg = (value: string, error = false) => { setMessage(value); setIsError(error); };
  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });
  const supplierOptions = suppliers.filter((s) => s.isActive).map((s) => <option key={s.id} value={s.id}>{s.code} · {name(s)}</option>);
  const itemOptions = items.map((i) => <option key={i.id} value={i.id}>{i.sku} · {name(i)}</option>);
  const unitOptions = uoms.map((u) => <option key={u.id} value={u.id}>{u.code} · {name(u)}</option>);

  async function loadContext() {
    const response = await auth("/api/v1/pos/context");
    if (response.ok) {
      const value = await response.json() as { branches: Branch[] };
      setBranches(value.branches);
      if (value.branches[0]) setBranchId(value.branches[0].id);
    }
  }
  async function loadSuppliers() { const r = await auth("/api/v1/procurement/suppliers"); if (r.ok) setSuppliers(await r.json() as Supplier[]); }
  async function loadItems() { const r = await auth("/api/v1/inventory/items"); if (r.ok) setItems(await r.json() as Item[]); }
  async function loadUoms() { const r = await auth("/api/v1/inventory/uoms"); if (r.ok) { const v = await r.json() as { units: Uom[] }; setUoms(v.units); } }
  async function loadOrders() { if (!branchId) return; const r = await auth(`/api/v1/procurement/purchase-orders?branchId=${branchId}`); if (r.ok) setOrders(await r.json() as PurchaseOrder[]); }
  async function loadReceipts() { if (!branchId) return; const r = await auth(`/api/v1/procurement/goods-receipts?branchId=${branchId}`); if (r.ok) setReceipts(await r.json() as GoodsReceipt[]); }

  useEffect(() => { void loadContext(); }, []);
  useEffect(() => { void loadSuppliers(); void loadItems(); void loadUoms(); }, []);
  useEffect(() => { if (branchId) { void loadOrders(); void loadReceipts(); } }, [branchId]);

  async function refreshAll() {
    setMsg(""); setLoading(true);
    try {
      await Promise.all([loadSuppliers(), loadItems(), loadUoms(), branchId ? loadOrders() : Promise.resolve(), branchId ? loadReceipts() : Promise.resolve()]);
    } finally { setLoading(false); }
  }

  async function createSupplier(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    try {
      const body = { code: supplierForm.code.trim() || null, nameAr: supplierForm.nameAr, nameEn: supplierForm.nameEn, contactPerson: supplierForm.contactPerson.trim() || null, phone: supplierForm.phone.trim() || null, email: supplierForm.email.trim() || null, vatNumber: supplierForm.vatNumber.trim() || null, address: supplierForm.address.trim() || null, notes: supplierForm.notes.trim() || null };
      const r = await auth("/api/v1/procurement/suppliers", { method: "POST", body: JSON.stringify(body) });
      if (!r.ok) { const p = await r.json().catch(() => null); throw new Error(p?.errors?.supplier?.[0] ?? p?.errors?.code?.[0] ?? p?.errors?.vatNumber?.[0] ?? t.failed); }
      setMsg(t.saved); setSupplierForm({ code: "", nameAr: "", nameEn: "", contactPerson: "", phone: "", email: "", vatNumber: "", address: "", notes: "" }); await loadSuppliers();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function createPurchaseOrder(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (!poForm.supplierId || poLines.some((l) => !l.inventoryItemId || !l.unitId || !l.quantity || !l.unitCost)) { setMsg(t.failed, true); return; }
    try {
      const lines = poLines.map((l) => ({ inventoryItemId: l.inventoryItemId, unitId: l.unitId, quantity: Number(l.quantity), unitCost: Number(l.unitCost) }));
      const r = await auth("/api/v1/procurement/purchase-orders", { method: "POST", body: JSON.stringify({ supplierId: poForm.supplierId, branchId, expectedDate: poForm.expectedDate ? new Date(poForm.expectedDate).toISOString() : null, notes: null, reference: poForm.reference.trim() || null, lines }) });
      if (!r.ok) { const p = await r.json().catch(() => null); throw new Error(p?.errors?.lines?.[0] ?? p?.errors?.supplierId?.[0] ?? t.failed); }
      setMsg(t.saved); setPoForm({ supplierId: "", expectedDate: "", reference: "" }); setPoLines([LineDef()]); await loadOrders();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function createGoodsReceipt(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (!grForm.supplierId || grLines.some((l) => !l.inventoryItemId || !l.unitId || !l.quantity || !l.unitCost)) { setMsg(t.failed, true); return; }
    try {
      const lines = grLines.map((l) => ({ inventoryItemId: l.inventoryItemId, unitId: l.unitId, quantity: Number(l.quantity), unitCost: Number(l.unitCost) }));
      const r = await auth("/api/v1/procurement/goods-receipts", { method: "POST", body: JSON.stringify({ supplierId: grForm.supplierId, branchId, purchaseOrderId: grForm.purchaseOrderId || null, reference: grForm.reference.trim() || null, notes: grForm.notes.trim() || null, clientReceiptId: createId(), lines }) });
      if (!r.ok) { const p = await r.json().catch(() => null); throw new Error(p?.errors?.lines?.[0] ?? p?.errors?.supplierId?.[0] ?? p?.errors?.purchaseOrderId?.[0] ?? t.failed); }
      setMsg(t.saved); setGrForm({ supplierId: "", purchaseOrderId: "", reference: "", notes: "" }); setGrLines([LineDef()]); await loadReceipts();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function actionPurchaseOrder(id: string, action: string) {
    setMsg("");
    try {
      const r = await auth(`/api/v1/procurement/purchase-orders/${id}/${action}`, { method: "POST" });
      if (!r.ok) { const p = await r.json().catch(() => null); throw new Error(p?.errors?.status?.[0] ?? t.failed); }
      setMsg(t.saved); await loadOrders(); await loadReceipts();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function postGoodsReceipt(id: string) {
    setMsg("");
    try {
      const r = await auth(`/api/v1/procurement/goods-receipts/${id}/post`, { method: "POST" });
      if (!r.ok) { const p = await r.json().catch(() => null); throw new Error(p?.errors?.status?.[0] ?? t.failed); }
      setMsg(t.saved); await loadReceipts(); await loadOrders();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function viewHistory(id: string) {
    setMsg(""); setHistory(null);
    const r = await auth(`/api/v1/procurement/suppliers/${id}/history`);
    if (!r.ok) { setMsg(t.failed, true); return; }
    setHistory(await r.json() as SupplierHistory); setHistorySupplierId(id);
  }

  const poStatusLabel = (v: string) => (v === "Draft" ? t.poDraft : v === "Submitted" ? t.poSubmitted : v === "Approved" ? t.poApproved : v === "Rejected" ? t.poRejected : v === "Cancelled" ? t.poCancelled : t.poReceived);
  const poStatusClass = (v: string) => (v === "Approved" || v === "Received" ? "bg-[#e3f4ea] text-[#137347]" : v === "Rejected" || v === "Cancelled" ? "bg-[#fbe4e2] text-[#b4322a]" : v === "Submitted" ? "bg-[#f4f1e3] text-[#8a6d1f]" : "bg-[#e8ece8] text-[#53615b]");
  const grStatus = (v: string) => v === "Posted" ? t.posted : t.draft;

  const editableOrder = (o: PurchaseOrder) => o.status === "Draft";

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <p className="mt-3 max-w-3xl text-[#64716b]">{t.intro}</p>
      <div className="mt-4 flex flex-wrap items-center gap-3 rounded-xl border border-[#d9dfd7] bg-[#edf5f1] p-3 text-sm text-[#08483f]"><Truck size={18} /><span>{t.intro2}</span><button onClick={() => void refreshAll()} className="ml-auto inline-flex min-h-9 items-center gap-2 rounded-lg bg-[#0e5a4f] px-3 text-xs font-semibold text-white"><RefreshCw size={15} />{t.reload}</button></div>

      <div className="mt-5 max-w-md">
        <label className="block text-sm font-medium">{t.branch}
          <select value={branchId} onChange={(e) => setBranchId(e.target.value)} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20">{branches.map((b) => <option key={b.id} value={b.id}>{name(b)}</option>)}</select>
        </label>
      </div>

      {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}
      {loading && <div className="mt-4 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div>}

      <div className="mt-6 grid gap-5 xl:grid-cols-2">
        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.suppliers}</h2>
          {suppliers.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 divide-y divide-[#e8ece8]">{suppliers.map((s) => <li key={s.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm"><div><span className="font-medium">{s.code}</span><span className="text-[#69766f]"> · {name(s)}</span>{s.vatNumber && <span className="text-[#69766f]"> · VAT {s.vatNumber}</span>}</div><button onClick={() => void viewHistory(s.id)} className="inline-flex min-h-8 items-center gap-1.5 rounded-lg border border-[#0e5a4f] px-2.5 text-xs font-semibold text-[#0e5a4f]"><Eye size={14} />{t.viewHistory}</button></li>)}</ul>
          )}
          <form onSubmit={createSupplier} className="mt-5 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <h3 className="font-semibold">{t.addSupplier}</h3>
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <Field label={t.code} value={supplierForm.code} onChange={(v) => setSupplierForm({ ...supplierForm, code: v })} max={50} />
              <Field label={t.vatNumber} value={supplierForm.vatNumber} onChange={(v) => setSupplierForm({ ...supplierForm, vatNumber: v })} max={50} />
              <Field label={t.nameAr} value={supplierForm.nameAr} onChange={(v) => setSupplierForm({ ...supplierForm, nameAr: v })} max={160} />
              <Field label={t.nameEn} value={supplierForm.nameEn} onChange={(v) => setSupplierForm({ ...supplierForm, nameEn: v })} max={160} />
              <Field label={t.contactPerson} value={supplierForm.contactPerson} onChange={(v) => setSupplierForm({ ...supplierForm, contactPerson: v })} max={160} />
              <Field label={t.phone} value={supplierForm.phone} onChange={(v) => setSupplierForm({ ...supplierForm, phone: v })} max={50} />
              <Field label={t.email} value={supplierForm.email} onChange={(v) => setSupplierForm({ ...supplierForm, email: v })} max={320} />
              <Field label={t.address} value={supplierForm.address} onChange={(v) => setSupplierForm({ ...supplierForm, address: v })} max={500} />
            </div>
            <Field label={t.notes} value={supplierForm.notes} onChange={(v) => setSupplierForm({ ...supplierForm, notes: v })} max={2000} />
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.add}</button>
          </form>
        </section>

        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.purchaseOrders}</h2>
          {orders.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 space-y-2">{orders.map((o) => (
              <li key={o.id} className="rounded-lg border border-[#e8ece8] px-3 py-2 text-sm">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="min-w-0 font-medium">{o.number}</span>
                  <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${poStatusClass(o.status)}`}>{poStatusLabel(o.status)}</span>
                </div>
                <p className="mt-1 text-[#69766f]">{language === "ar" ? o.supplierNameAr : o.supplierNameEn ?? ""} · {o.lineCount} {t.lines} · {fmt(o.totalAmount)}</p>
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {o.status === "Draft" && <button onClick={() => void actionPurchaseOrder(o.id, "submit")} className="min-h-8 rounded-lg border border-[#0e5a4f] px-2.5 text-xs font-semibold text-[#0e5a4f]">{t.submit}</button>}
                  {o.status === "Submitted" && <button onClick={() => void actionPurchaseOrder(o.id, "approve")} className="inline-flex min-h-8 items-center gap-1 rounded-lg bg-[#137347] px-2.5 text-xs font-semibold text-white"><BadgeCheck size={14} />{t.approve}</button>}
                  {o.status === "Submitted" && <button onClick={() => void actionPurchaseOrder(o.id, "reject")} className="inline-flex min-h-8 items-center gap-1 rounded-lg bg-[#b4322a] px-2.5 text-xs font-semibold text-white"><XCircle size={14} />{t.reject}</button>}
                  {editableOrder(o) && <button onClick={() => void actionPurchaseOrder(o.id, "cancel")} className="min-h-8 rounded-lg border border-[#b4322a] px-2.5 text-xs font-semibold text-[#b4322a]">{t.cancelOrder}</button>}
                  {o.status === "Approved" && <button onClick={() => void actionPurchaseOrder(o.id, "receive")} className="inline-flex min-h-8 items-center gap-1 rounded-lg bg-[#0e5a4f] px-2.5 text-xs font-semibold text-white"><Recycle size={14} />{t.receive}</button>}
                </div>
              </li>
            ))}</ul>
          )}
          <form onSubmit={createPurchaseOrder} className="mt-5 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <h3 className="font-semibold">{t.addPo}</h3>
            <div className="mt-3 grid gap-3 sm:grid-cols-3">
              <Select label={t.supplier} value={poForm.supplierId} onChange={(v) => setPoForm({ ...poForm, supplierId: v })}>{supplierOptions}</Select>
              <Field label={t.expectedDate} value={poForm.expectedDate} onChange={(v) => setPoForm({ ...poForm, expectedDate: v })} type="date" />
              <Field label={t.reference} value={poForm.reference} onChange={(v) => setPoForm({ ...poForm, reference: v })} max={200} />
            </div>
            <div className="mt-4 space-y-2">
              {poLines.map((l, index) => (
                <div key={index} className="grid gap-2 sm:grid-cols-[1fr_1fr_5rem_5rem_auto]">
                  <Select label={t.product} value={l.inventoryItemId} onChange={(v) => setPoLines(poLines.map((row, i) => i === index ? { ...row, inventoryItemId: v } : row))}>{itemOptions}</Select>
                  <Select label={t.unit} value={l.unitId} onChange={(v) => setPoLines(poLines.map((row, i) => i === index ? { ...row, unitId: v } : row))}>{unitOptions}</Select>
                  <Field label={t.quantity} value={l.quantity} onChange={(v) => setPoLines(poLines.map((row, i) => i === index ? { ...row, quantity: v } : row))} type="number" />
                  <Field label={t.unitCost} value={l.unitCost} onChange={(v) => setPoLines(poLines.map((row, i) => i === index ? { ...row, unitCost: v } : row))} type="number" />
                  <button type="button" onClick={() => setPoLines(poLines.filter((_, i) => i !== index))} className="min-h-10 rounded-lg border border-[#b4322a] px-2 text-sm text-[#b4322a]">{t.remove}</button>
                </div>
              ))}
            </div>
            <button type="button" onClick={() => setPoLines([...poLines, LineDef()])} className="mt-3 inline-flex min-h-10 items-center gap-2 rounded-lg border border-[#0e5a4f] px-3 text-sm font-semibold text-[#0e5a4f]"><Plus size={16} />{t.addLine}</button>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Save size={18} />{t.create}</button>
          </form>
        </section>

        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.goodsReceipts}</h2>
          {receipts.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 space-y-2">{receipts.map((g) => (
              <li key={g.id} className="rounded-lg border border-[#e8ece8] px-3 py-2 text-sm">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="min-w-0 font-medium">{g.number}</span>
                  <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${g.status === "Posted" ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#f4f1e3] text-[#8a6d1f]"}`}>{grStatus(g.status)}</span>
                </div>
                <p className="mt-1 text-[#69766f]">{language === "ar" ? g.supplierNameAr : g.supplierNameEn ?? ""} · {g.lineCount} {t.lines} · {fmt(g.totalAmount)}</p>
                {g.status === "Draft" && <div className="mt-2"><button onClick={() => void postGoodsReceipt(g.id)} className="inline-flex min-h-8 items-center gap-1 rounded-lg bg-[#0e5a4f] px-2.5 text-xs font-semibold text-white"><Recycle size={14} />{t.post}</button></div>}
              </li>
            ))}</ul>
          )}
          <form onSubmit={createGoodsReceipt} className="mt-5 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <h3 className="font-semibold">{t.addGr}</h3>
            <div className="mt-3 grid gap-3 sm:grid-cols-3">
              <Select label={t.supplier} value={grForm.supplierId} onChange={(v) => setGrForm({ ...grForm, supplierId: v })}>{supplierOptions}</Select>
              <Field label={t.reference} value={grForm.reference} onChange={(v) => setGrForm({ ...grForm, reference: v })} max={200} />
              <Field label={t.notes} value={grForm.notes} onChange={(v) => setGrForm({ ...grForm, notes: v })} max={2000} />
            </div>
            <div className="mt-4 space-y-2">
              {grLines.map((l, index) => (
                <div key={index} className="grid gap-2 sm:grid-cols-[1fr_1fr_5rem_5rem_auto]">
                  <Select label={t.product} value={l.inventoryItemId} onChange={(v) => setGrLines(grLines.map((row, i) => i === index ? { ...row, inventoryItemId: v } : row))}>{itemOptions}</Select>
                  <Select label={t.unit} value={l.unitId} onChange={(v) => setGrLines(grLines.map((row, i) => i === index ? { ...row, unitId: v } : row))}>{unitOptions}</Select>
                  <Field label={t.quantity} value={l.quantity} onChange={(v) => setGrLines(grLines.map((row, i) => i === index ? { ...row, quantity: v } : row))} type="number" />
                  <Field label={t.unitCost} value={l.unitCost} onChange={(v) => setGrLines(grLines.map((row, i) => i === index ? { ...row, unitCost: v } : row))} type="number" />
                  <button type="button" onClick={() => setGrLines(grLines.filter((_, i) => i !== index))} className="min-h-10 rounded-lg border border-[#b4322a] px-2 text-sm text-[#b4322a]">{t.remove}</button>
                </div>
              ))}
            </div>
            <button type="button" onClick={() => setGrLines([...grLines, LineDef()])} className="mt-3 inline-flex min-h-10 items-center gap-2 rounded-lg border border-[#0e5a4f] px-3 text-sm font-semibold text-[#0e5a4f]"><Plus size={16} />{t.addLine}</button>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Save size={18} />{t.create}</button>
          </form>
        </section>

        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.history}</h2>
          <div className="mt-3 max-w-md">
            <Select label={t.supplier} value={historySupplierId} onChange={(v) => void viewHistory(v)}><option value="">{t.none}</option>{suppliers.map((s) => <option key={s.id} value={s.id}>{s.code} · {name(s)}</option>)}</Select>
          </div>
          {!history ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <>
              <div className="mt-4 grid gap-3 sm:grid-cols-3">
                <HistMetric label={t.receiptsCount} value={String(history.summary.receiptCount)} />
                <HistMetric label={t.linesCount} value={String(history.summary.lineCount)} />
                <HistMetric label={t.totalAmount} value={fmt(history.summary.totalAmount)} />
              </div>
              <div className="mt-5">
                <h3 className="text-sm font-semibold">{t.latestCosts}</h3>
                {history.itemCostSummary.length === 0 ? <p className="mt-2 text-sm text-[#69766f]">{t.empty}</p> : (
                  <ul className="mt-2 divide-y divide-[#e8ece8]">{history.itemCostSummary.map((c) => (
                    <li key={c.inventoryItemId} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm"><span className="min-w-0">{history.history.find((h) => h.inventoryItemId === c.inventoryItemId)?.itemNameAr ?? history.history.find((h) => h.inventoryItemId === c.inventoryItemId)?.itemNameEn ?? c.inventoryItemId}</span><span className="text-[#69766f]">{t.receivedQty}: {qty(c.receivedQuantity)} {c.unitCode ?? ""} · {t.latestCost}: {fmt(c.latestUnitCost)}</span></li>
                  ))}</ul>
                )}
              </div>
              <div className="mt-4 max-h-72 overflow-y-auto rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-3">
                {history.history.length === 0 ? <p className="text-sm text-[#69766f]">{t.empty}</p> : (
                  <ol className="space-y-1 text-sm">{history.history.map((h, index) => <li key={index} className="flex flex-wrap items-center justify-between gap-2 py-0.5"><span className="min-w-0">{h.receiptNumber} · {language === "ar" ? h.itemNameAr : h.itemNameEn ?? ""}</span><span className="text-[#69766f]">{qty(h.quantity)} {h.unitCode ?? ""} · {fmt(h.unitCost)}</span></li>)}</ol>
                )}
              </div>
            </>
          )}
        </section>
      </div>
    </div>
  );
}

function HistMetric({ label, value }: { label: string; value: string }) { return <div className="rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-3 text-center"><p className="text-lg font-semibold">{value}</p><p className="text-xs text-[#69766f]">{label}</p></div>; }

function Field({ label, value, onChange, max, type = "text" }: { label: string; value: string; onChange: (value: string) => void; max?: number; type?: string }) {
  return <label className="block text-sm font-medium">{label}<input type={type} value={value} onChange={(e) => onChange(e.target.value)} maxLength={max} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20" /></label>;
}

function Select({ label, value, onChange, children }: { label: string; value: string; onChange: (value: string) => void; children: React.ReactNode }) {
  return <label className="block text-sm font-medium">{label}<select value={value} onChange={(e) => onChange(e.target.value)} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20">{children}</select></label>;
}
