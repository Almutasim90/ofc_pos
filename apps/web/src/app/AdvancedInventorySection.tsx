import { useEffect, useState } from "react";
import { ClipboardCheck, RefreshCw, Trash2, Save, Plus } from "lucide-react";
import { createId, store } from "@/lib/local-store";

type Language = "ar" | "en";
type Branch = { id: string; code: string; nameAr: string; nameEn: string };
type Item = { id: string; sku: string; barcode: string | null; nameAr: string; nameEn: string; type: string; baseUnitId: string; unitCost: number; stockOnHand: number | null; isActive: boolean };
type Uom = { id: string; code: string; nameAr: string; nameEn: string; symbol: string | null; isActive: boolean; sortOrder: number };
type CountRow = { id: string; number: string; branchId: string; status: "Draft" | "Posted" | "Cancelled"; approvedAt: string | null; postedAt: string | null; createdAt: string; lineCount: number; varianceLineCount: number };
type CountLine = { id: string; inventoryItemId: string; itemNameAr: string | null; itemNameEn: string | null; systemQuantity: number; countedQuantity: number; variance: number; reason: string | null; unitId: string; adjusted: boolean };
type CountDetail = CountRow & { note: string | null; lines: CountLine[] };
type TransferRow = { id: string; number: string; sourceBranchId: string; destinationBranchId: string; status: "Draft" | "InTransit" | "Received" | "Cancelled"; reference: string | null; shippedAt: string | null; receivedAt: string | null; createdAt: string; lineCount: number; totalQuantity: number };
type TransferLine = { id: string; inventoryItemId: string; itemNameAr: string | null; itemNameEn: string | null; quantity: number; unitId: string; unitCost: number };
type TransferDetail = TransferRow & { note: string | null; lines: TransferLine[] };
type WasteRow = { id: string; number: string; branchId: string; inventoryItemId: string; itemNameAr: string | null; itemNameEn: string | null; category: string; quantity: number; reason: string | null; reference: string | null; occurredAt: string };
type ValuationRow = { id: string; sku: string; itemNameAr: string; itemNameEn: string; balance: number; unitCost: number; value: number };
type CostRow = { productSku: string; productNameAr: string; productNameEn: string; recipeCost: number; reproducible: boolean; sellingPrice: number; foodCostPercent: number; grossMargin: number; grossMarginPercent: number };

const wasteCategories = ["Expired", "Damaged", "PreparationWaste", "FinishedProductWaste", "CancelledOrderWaste"] as const;

const copy = {
  ar: {
    title: "الجرد والتحويل والهدر والتكلفة", intro: "دورات الجرد والتسوية، التحويل بين الفروع، تسجيل الهدر وأسبابه، وحساب تكلفة الوصفة والهامش.", loading: "جارٍ التحميل", reload: "تحديث", saved: "تم الحفظ بنجاح.", failed: "تعذر تنفيذ العملية.", empty: "لا توجد بيانات بعد.", branch: "الفرع", view: "عرض",
    counts: "عدّ المخزون الفعلي", addCount: "إنشاء جرد", note: "ملاحظة", countItem: "المادة", countedQty: "الكمية الفعلية", reason: "السبب", systemQty: "كمية النظام", variance: "الفرق", approve: "اعتماد", post: "تحديث الرصيد بالكمية المعتمدة", cancel: "إلغاء", addCountLine: "إضافة سطر", createCount: "إنشاء", statusDraft: "مسودة", statusPosted: "مرحّل", statusCancelled: "ملغي", countLines: "أسطر الجرد",
    transfers: "التحويل بين الفروع", addTransfer: "إنشاء تحويل", sourceBranch: "فرع المصدر", destinationBranch: "الفرع الوجهة", transferItem: "المادة", quantity: "الكمية", reference: "مرجع", ship: "شحن", receive: "استلام", cancelTransfer: "إلغاء", addTransferLine: "إضافة سطر", createTransfer: "إنشاء", statusInTransit: "قيد النقل", statusReceived: "مستلم", statusCancelledTransfer: "ملغي",
    waste: "الهدر", addWaste: "تسجيل هدر", category: "نوع الهدر", unit: "الوحدة", photoUrl: "رابط الصورة", recordWaste: "تسجيل", catExpired: "منتهي الصلاحية", catDamaged: "تالف", catPreparationWaste: "هدر التحضير", catFinishedProductWaste: "هدر المنتج النهائي", catCancelledOrderWaste: "هدر طلب ملغي",
    costing: "التكلفة والتسعير", viewValuation: "قيمة المخزون", valuation: "قيمة المخزون", totalValue: "إجمالي قيمة المخزون", itemsLineCount: "منتجات", balance: "الرصيد", unitCost: "تكلفة الوحدة", value: "القيمة", recipeCost: "تكلفة الوصفة", reproduced: "التكلفة قابلة لإعادة الحساب", notReproduced: "التكلفة غير قابلة للاحتساب", sellingPrice: "سعر البيع", foodCostPercent: "نسبة تكلفة الطعام", grossMargin: "الهامش الإجمالي", grossMarginPercent: "نسبة الهامش", viewSummary: "ملخص التكلفة"
  } as const,
  en: {
    title: "Advanced inventory, waste & costing", intro: "Count and adjustment cycles, inter-branch transfers, auditable waste with reasons, and recipe cost/COGS valuation.", loading: "Loading", reload: "Refresh", saved: "Saved successfully.", failed: "Unable to complete the operation.", empty: "No data yet.", branch: "Branch", view: "View",
    counts: "Physical counts", addCount: "Start a count", note: "Note", countItem: "Item", countedQty: "Counted quantity", reason: "Reason", systemQty: "System quantity", variance: "Variance", approve: "Approve", post: "Post adjustment", cancel: "Cancel", addCountLine: "Add line", createCount: "Create", statusDraft: "Draft", statusPosted: "Posted", statusCancelled: "Cancelled", countLines: "Count lines",
    transfers: "Stock transfers", addTransfer: "New transfer", sourceBranch: "Source branch", destinationBranch: "Destination branch", transferItem: "Item", quantity: "Quantity", reference: "Reference", ship: "Ship", receive: "Receive", cancelTransfer: "Cancel", addTransferLine: "Add line", createTransfer: "Create", statusInTransit: "In transit", statusReceived: "Received", statusCancelledTransfer: "Cancelled",
    waste: "Waste", addWaste: "Record waste", category: "Waste category", unit: "Unit", photoUrl: "Photo URL", recordWaste: "Record", catExpired: "Expired", catDamaged: "Damaged", catPreparationWaste: "Preparation waste", catFinishedProductWaste: "Finished product waste", catCancelledOrderWaste: "Cancelled order waste",
    costing: "Costing & pricing", viewValuation: "Inventory value", valuation: "Inventory value", totalValue: "Total inventory value", itemsLineCount: "lines", balance: "Balance", unitCost: "Unit cost", value: "Value", recipeCost: "Recipe cost", reproduced: "Reproducible", notReproduced: "Not reproducible", sellingPrice: "Selling price", foodCostPercent: "Food cost %", grossMargin: "Gross margin", grossMarginPercent: "Gross margin %", viewSummary: "Costing summary"
  } as const,
} as const;

export function AdvancedInventorySection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr?: string; nameEn?: string }) => (language === "ar" ? x.nameAr ?? "" : x.nameEn ?? "");
  const fmt = (v: number | null | undefined) => v === null || v === undefined ? "—" : new Intl.NumberFormat(language, { maximumFractionDigits: 6 }).format(v);
  const money = (v: number | null | undefined) => v === null || v === undefined ? "—" : new Intl.NumberFormat(language, { maximumFractionDigits: 4 }).format(v);

  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [items, setItems] = useState<Item[]>([]);
  const [units, setUnits] = useState<Uom[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);

  const [counts, setCounts] = useState<CountRow[]>([]);
  const [countDetail, setCountDetail] = useState<CountDetail | null>(null);
  const [countForm, setCountForm] = useState({ note: "", reason: "" });
  const [countLines, setCountLines] = useState<CountLineForm[]>([]);

  const [transfers, setTransfers] = useState<TransferRow[]>([]);
  const [transferDetail, setTransferDetail] = useState<TransferDetail | null>(null);
  const [transferForm, setTransferForm] = useState({ sourceBranchId: "", destinationBranchId: "", note: "", reference: "" });
  const [transferLines, setTransferLines] = useState<TransferLineForm[]>([]);

  const [waste, setWaste] = useState<WasteRow[]>([]);
  const [wasteForm, setWasteForm] = useState({ inventoryItemId: "", category: "Expired" as (typeof wasteCategories)[number], quantity: "", unitId: "", reason: "", note: "", photoUrl: "", reference: "" });

  const [valuation, setValuation] = useState<{ totalValue: number; rows: ValuationRow[] } | null>(null);
  const [costRows, setCostRows] = useState<CostRow[] | null>(null);

  const setMsg = (value: string, error = false) => { setMessage(value); setIsError(error); };
  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });

  async function loadContext() {
    const response = await auth("/api/v1/pos/context");
    if (!response.ok) return;
    const value = await response.json() as { branches: Branch[] };
    setBranches(value.branches);
    if (value.branches[0]) setBranchId((prev) => prev || value.branches[0].id);
  }
  async function loadItems() { const r = await auth("/api/v1/inventory/items"); if (r.ok) setItems(await r.json() as Item[]); }
  async function loadUoms() { const r = await auth("/api/v1/inventory/uoms"); if (r.ok) setUnits((await r.json() as { units: Uom[] }).units); }
  async function loadCounts() { if (!branchId) return; const r = await auth(`/api/v1/inventory/counts?branchId=${branchId}`); if (r.ok) setCounts(await r.json() as CountRow[]); }
  async function loadTransfers() { if (!branchId) return; const r = await auth(`/api/v1/inventory/transfers?branchId=${branchId}`); if (r.ok) setTransfers(await r.json() as TransferRow[]); }
  async function loadWaste() { if (!branchId) return; const r = await auth(`/api/v1/inventory/waste?branchId=${branchId}`); if (r.ok) setWaste(await r.json() as WasteRow[]); }

  useEffect(() => { void loadContext(); void loadItems(); void loadUoms(); }, []);
  useEffect(() => { if (branchId) { void loadCounts(); void loadTransfers(); void loadWaste(); } }, [branchId]);

  async function refreshAll() {
    setMsg(""); setLoading(true);
    try { await Promise.all([loadCounts(), loadTransfers(), loadWaste()]); } finally { setLoading(false); }
  }

  async function openCount(id: string) { const r = await auth(`/api/v1/inventory/counts/${id}`); if (r.ok) setCountDetail(await r.json() as CountDetail); }
  async function actCount(id: string, action: string) {
    setMsg("");
    const r = await auth(`/api/v1/inventory/counts/${id}/${action}`, { method: "POST" });
    if (!r.ok) { const p = await r.json().catch(() => null); setMsg(p?.errors?.reason?.[0] ?? p?.errors?.status?.[0] ?? t.failed, true); return; }
    setMsg(t.saved); setCountDetail(null); await loadCounts();
  }

  async function createCount(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (countLines.length === 0 || countLines.some((l) => !l.inventoryItemId || l.countedQuantity.trim() === "" || Number(l.countedQuantity) < 0)) { setMsg(t.failed, true); return; }
    try {
      const lines = countLines.map((l) => ({ inventoryItemId: l.inventoryItemId, countedQuantity: l.countedQuantity === "" ? 0 : Number(l.countedQuantity), reason: countForm.reason.trim() || null }));
      const r = await auth("/api/v1/inventory/counts", { method: "POST", body: JSON.stringify({ branchId, clientCountId: createId(), note: countForm.note.trim() || null, lines }) });
      if (!r.ok) { const p = await r.json().catch(() => null); throw new Error(p?.errors?.lines?.[0] ?? p?.errors?.count?.[0] ?? t.failed); }
      setMsg(t.saved); setCountForm({ note: "", reason: "" }); setCountLines([]); await loadCounts();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function openTransfer(id: string) { const r = await auth(`/api/v1/inventory/transfers/${id}`); if (r.ok) setTransferDetail(await r.json() as TransferDetail); }
  async function actTransfer(id: string, action: string, manageError: string) {
    setMsg("");
    const r = await auth(`/api/v1/inventory/transfers/${id}/${action}`, { method: "POST" });
    if (!r.ok) { const p = await r.json().catch(() => null); setMsg(p?.errors?.lines?.[0] ?? p?.errors?.status?.[0] ?? (manageError ?? t.failed), true); return; }
    setMsg(t.saved); setTransferDetail(null); await loadTransfers();
  }

  async function createTransfer(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (!transferForm.sourceBranchId || !transferForm.destinationBranchId || transferLines.length === 0 || transferLines.some((l) => !l.inventoryItemId || !l.quantity)) { setMsg(t.failed, true); return; }
    try {
      const lines = transferLines.map((l) => ({ inventoryItemId: l.inventoryItemId, quantity: Number(l.quantity) }));
      const r = await auth("/api/v1/inventory/transfers", { method: "POST", body: JSON.stringify({ sourceBranchId: transferForm.sourceBranchId, destinationBranchId: transferForm.destinationBranchId, clientTransferId: createId(), note: transferForm.note.trim() || null, reference: transferForm.reference.trim() || null, lines }) });
      if (!r.ok) { const p = await r.json().catch(() => null); throw new Error(p?.errors?.lines?.[0] ?? p?.errors?.destinationBranchId?.[0] ?? t.failed); }
      setMsg(t.saved); setTransferForm({ sourceBranchId: branchId, destinationBranchId: "", note: "", reference: "" }); setTransferLines([]); await loadTransfers();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function createWaste(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (!wasteForm.inventoryItemId || !wasteForm.unitId || !wasteForm.quantity || !wasteForm.reason.trim()) { setMsg(t.failed, true); return; }
    try {
      const r = await auth("/api/v1/inventory/waste", { method: "POST", body: JSON.stringify({ branchId, inventoryItemId: wasteForm.inventoryItemId, category: wasteForm.category, quantity: Number(wasteForm.quantity), unitId: wasteForm.unitId, reason: wasteForm.reason.trim(), note: wasteForm.note.trim() || null, photoUrl: wasteForm.photoUrl.trim() || null, reference: wasteForm.reference.trim() || null, orderId: null, orderLineId: null, shiftId: null, clientRecordId: createId(), occurredAt: null }) });
      if (!r.ok) { const p = await r.json().catch(() => null); throw new Error(p?.errors?.reason?.[0] ?? p?.errors?.quantity?.[0] ?? t.failed); }
      setMsg(t.saved); setWasteForm({ inventoryItemId: "", category: "Expired", quantity: "", unitId: "", reason: "", note: "", photoUrl: "", reference: "" }); await loadWaste();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function loadValuation() {
    setMsg(""); if (!branchId) return;
    const r = await auth(`/api/v1/inventory/costing/value?branchId=${branchId}`);
    if (!r.ok) { setMsg(t.failed, true); return; }
    const value = await r.json() as { totalValue: number; rows: ValuationRow[] };
    setValuation(value); setCostRows(null);
  }
  async function loadCosting() {
    setMsg(""); if (!branchId) return;
    const r = await auth(`/api/v1/inventory/costing/summary?branchId=${branchId}`);
    if (!r.ok) { setMsg(t.failed, true); return; }
    const value = await r.json() as { rows: CostRow[] };
    setCostRows(value.rows); setValuation(null);
  }

  const updateCountLine = (index: number, patch: Partial<CountLineForm>) => setCountLines(countLines.map((line, i) => i === index ? { ...line, ...patch } : line));
  const updateTransferLine = (index: number, patch: Partial<TransferLineForm>) => setTransferLines(transferLines.map((line, i) => i === index ? { ...line, ...patch } : line));

  const itemOptions = items.map((i) => <option key={i.id} value={i.id}>{i.sku} · {name(i)}</option>);
  const unitOptions = units.map((u) => <option key={u.id} value={u.id}>{u.code} · {name(u)}</option>);
  const branchOptions = branches.map((b) => <option key={b.id} value={b.id}>{b.code} · {name(b)}</option>);
  const countStatus = (s: string) => s === "Draft" ? t.statusDraft : s === "Posted" ? t.statusPosted : t.statusCancelled;
  const transferStatus = (s: string) => s === "Draft" ? t.statusDraft : s === "InTransit" ? t.statusInTransit : s === "Received" ? t.statusReceived : t.statusCancelledTransfer;
  const categoryLabel = (s: string) => s === "Expired" ? t.catExpired : s === "Damaged" ? t.catDamaged : s === "PreparationWaste" ? t.catPreparationWaste : s === "FinishedProductWaste" ? t.catFinishedProductWaste : t.catCancelledOrderWaste;

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <p className="mt-3 max-w-3xl text-[#64716b]">{t.intro}</p>
      <div className="mt-4 flex flex-wrap items-center gap-3 rounded-xl border border-[#d9dfd7] bg-[#edf5f1] p-3 text-sm text-[#08483f]"><ClipboardCheck size={18} /><span>{t.intro}</span><button onClick={() => void refreshAll()} className="ml-auto inline-flex min-h-9 items-center gap-2 rounded-lg bg-[#0e5a4f] px-3 text-xs font-semibold text-white"><RefreshCw size={15} />{t.reload}</button></div>

      <div className="mt-5 max-w-md">
        <Select label={t.branch} value={branchId} onChange={setBranchId}>{branchOptions}</Select>
      </div>

      {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}
      {loading && <div className="mt-4 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div>}

      <div className="mt-6 grid gap-5 xl:grid-cols-2">
        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.counts}</h2><p className="mt-3 rounded-lg bg-[#edf5f1] p-4 text-sm leading-7">{language === "ar" ? "١. عُدّ الكمية الموجودة فعليًا في المخزن. ٢. أدخل الكمية بوحدة المادة. ٣. راجع الفرق وسببه، ثم اعتمد الجرد وحدّث الرصيد. مثال: النظام يعرض 10 كجم دجاج، والموجود 8 كجم؛ الفرق ناقص 2 كجم. إنشاء الجرد وحده لا يغيّر الرصيد." : "1. Count the stock physically present. 2. Enter the quantity in the item’s unit. 3. Review the difference and its reason, approve, then update stock. Example: system 10 kg of chicken, counted 8 kg, difference −2 kg. Creating a count alone does not change stock."}</p>
          {counts.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 divide-y divide-[#e8ece8]">{counts.map((c) => <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm"><span className="min-w-0 font-medium">{c.number}</span><div className="flex items-center gap-1.5"><span className="text-xs text-[#69766f]">{c.lineCount} {t.countLines} · {c.varianceLineCount} {t.variance}</span><span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${c.status === "Posted" ? "bg-[#e3f4ea] text-[#137347]" : c.status === "Draft" ? "bg-[#f4f1e3] text-[#8a6d1f]" : "bg-[#e8ece8] text-[#53615b]"}`}>{countStatus(c.status)}</span><button onClick={() => void openCount(c.id)} className="min-h-8 rounded-lg bg-[#edf5f1] px-2.5 text-xs font-semibold text-[#0e5a4f]">{t.view}</button></div></li>)}</ul>
          )}
          {countDetail && (
            <div className="mt-4 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
              <h3 className="font-semibold">{countDetail.number}</h3>
              <ol className="mt-2 space-y-1 text-sm">{countDetail.lines.map((l) => <li key={l.id} className="flex flex-wrap items-center justify-between gap-2"><span className="min-w-0">{l.itemNameAr ?? l.itemNameEn ?? ""}</span><span className={`font-medium ${l.variance < 0 ? "text-[#b4322a]" : l.variance > 0 ? "text-[#137347]" : "text-[#69766f]"}`}>{t.systemQty}: {fmt(l.systemQuantity)} → {t.countedQty}: {fmt(l.countedQuantity)} ({l.variance > 0 ? "+" : ""}{fmt(l.variance)})</span></li>)}</ol>
              <div className="mt-3 flex flex-wrap gap-2">{(countDetail.status === "Draft" && !countDetail.approvedAt) && <button onClick={() => void actCount(countDetail.id, "approve")} className="min-h-9 rounded-lg bg-[#137347] px-3 text-xs font-semibold text-white">{t.approve}</button>}{countDetail.status === "Draft" && countDetail.approvedAt && <button onClick={() => void actCount(countDetail.id, "post")} className="min-h-9 rounded-lg bg-[#0e5a4f] px-3 text-xs font-semibold text-white">{t.post}</button>}{countDetail.status === "Draft" && <button onClick={() => void actCount(countDetail.id, "cancel")} className="min-h-9 rounded-lg border border-[#b4322a] px-3 text-xs font-semibold text-[#b4322a]">{t.cancel}</button>}</div>
            </div>
          )}
          <form onSubmit={createCount} className="mt-5 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <h3 className="font-semibold">{t.addCount}</h3>
            <div className="mt-3"><Field label={t.note} value={countForm.note} onChange={(v) => setCountForm({ ...countForm, note: v })} max={500} /></div>
            <div className="mt-3"><Field label={t.reason} value={countForm.reason} onChange={(v) => setCountForm({ ...countForm, reason: v })} max={500} /></div>
            <div className="mt-3 space-y-2">
              {countLines.map((l, index) => (
                <div key={index} className="grid gap-2 sm:grid-cols-[1fr_5rem_auto]">
                  <Select label={t.countItem} value={l.inventoryItemId} onChange={(v) => updateCountLine(index, { inventoryItemId: v })}>{itemOptions}</Select>
                  <Field required label={t.countedQty} value={l.countedQuantity} onChange={(v) => updateCountLine(index, { countedQuantity: v })} type="number" />
                  <button type="button" onClick={() => setCountLines(countLines.filter((_, i) => i !== index))} className="min-h-10 rounded-lg border border-[#b4322a] px-2 text-sm text-[#b4322a]"><Trash2 size={15} /></button>
                </div>
              ))}
            </div>

            <button type="button" onClick={() => setCountLines([...countLines, { inventoryItemId: "", countedQuantity: "", reason: "" }])} className="mt-3 inline-flex min-h-10 items-center gap-2 rounded-lg border border-[#0e5a4f] px-3 text-sm font-semibold text-[#0e5a4f]"><Plus size={16} />{t.addCountLine}</button>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white"><Save size={18} />{t.createCount}</button>
          </form>
        </section>

        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.transfers}</h2>
          {transfers.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 divide-y divide-[#e8ece8]">{transfers.map((x) => <li key={x.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm"><span className="min-w-0 font-medium">{x.number}</span><div className="flex items-center gap-1.5"><span className="text-xs text-[#69766f]">{fmt(x.totalQuantity)} · {x.lineCount}</span><span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${x.status === "Received" ? "bg-[#e3f4ea] text-[#137347]" : x.status === "InTransit" ? "bg-[#edf5f1] text-[#0e5a4f]" : x.status === "Draft" ? "bg-[#f4f1e3] text-[#8a6d1f]" : "bg-[#e8ece8] text-[#53615b]"}`}>{transferStatus(x.status)}</span><button onClick={() => void openTransfer(x.id)} className="min-h-8 rounded-lg bg-[#edf5f1] px-2.5 text-xs font-semibold text-[#0e5a4f]">{t.view}</button></div></li>)}</ul>
          )}
          {transferDetail && (
            <div className="mt-4 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
              <h3 className="font-semibold">{transferDetail.number}</h3>
              <ol className="mt-2 space-y-1 text-sm">{transferDetail.lines.map((l) => <li key={l.id} className="flex items-center justify-between"><span className="min-w-0">{l.itemNameAr ?? l.itemNameEn ?? ""}</span><span className="text-[#69766f]">{fmt(l.quantity)}</span></li>)}</ol>
              <div className="mt-3 flex flex-wrap gap-2">{transferDetail.status === "Draft" && <button onClick={() => void actTransfer(transferDetail.id, "ship", t.failed)} className="min-h-9 rounded-lg bg-[#0e5a4f] px-3 text-xs font-semibold text-white">{t.ship}</button>}{transferDetail.status === "InTransit" && <button onClick={() => void actTransfer(transferDetail.id, "receive", t.failed)} className="min-h-9 rounded-lg bg-[#137347] px-3 text-xs font-semibold text-white">{t.receive}</button>}{(transferDetail.status === "Draft" || transferDetail.status === "InTransit") && <button onClick={() => void actTransfer(transferDetail.id, "cancel", t.failed)} className="min-h-9 rounded-lg border border-[#b4322a] px-3 text-xs font-semibold text-[#b4322a]">{t.cancelTransfer}</button>}</div>
            </div>
          )}
          <form onSubmit={createTransfer} className="mt-5 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <h3 className="font-semibold">{t.addTransfer}</h3>
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <Select label={t.sourceBranch} value={transferForm.sourceBranchId || branchId} onChange={(v) => setTransferForm({ ...transferForm, sourceBranchId: v })}>{branchOptions}</Select>
              <Select label={t.destinationBranch} value={transferForm.destinationBranchId} onChange={(v) => setTransferForm({ ...transferForm, destinationBranchId: v })}>{branchOptions}</Select>
              <Field label={t.note} value={transferForm.note} onChange={(v) => setTransferForm({ ...transferForm, note: v })} max={500} />
              <Field label={t.reference} value={transferForm.reference} onChange={(v) => setTransferForm({ ...transferForm, reference: v })} max={200} />
            </div>
            <div className="mt-3 space-y-2">
              {transferLines.map((l, index) => (
                <div key={index} className="grid gap-2 sm:grid-cols-[1fr_5rem_auto]">
                  <Select label={t.transferItem} value={l.inventoryItemId} onChange={(v) => updateTransferLine(index, { inventoryItemId: v })}>{itemOptions}</Select>
                  <Field label={t.quantity} value={l.quantity} onChange={(v) => updateTransferLine(index, { quantity: v })} type="number" />
                  <button type="button" onClick={() => setTransferLines(transferLines.filter((_, i) => i !== index))} className="min-h-10 rounded-lg border border-[#b4322a] px-2 text-sm text-[#b4322a]"><Trash2 size={15} /></button>
                </div>
              ))}
            </div>
            <button type="button" onClick={() => setTransferLines([...transferLines, { inventoryItemId: "", quantity: "" }])} className="mt-3 inline-flex min-h-10 items-center gap-2 rounded-lg border border-[#0e5a4f] px-3 text-sm font-semibold text-[#0e5a4f]"><Plus size={16} />{t.addTransferLine}</button>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white"><Save size={18} />{t.createTransfer}</button>
          </form>
        </section>

        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.waste}</h2>
          {waste.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 divide-y divide-[#e8ece8]">{waste.map((w) => <li key={w.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm"><span className="min-w-0">{w.itemNameAr ?? w.itemNameEn ?? ""} · {categoryLabel(w.category)}</span><span className={`font-medium ${w.quantity < 0 ? "text-[#b4322a]" : "text-[#137347]"}`}>{fmt(w.quantity)}</span></li>)}</ul>
          )}
          <form onSubmit={createWaste} className="mt-5 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <h3 className="font-semibold">{t.addWaste}</h3>
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <Select label={t.countItem} value={wasteForm.inventoryItemId} onChange={(v) => setWasteForm({ ...wasteForm, inventoryItemId: v })}>{itemOptions}</Select>
              <label className="block text-sm font-medium">{t.category}<select value={wasteForm.category} onChange={(e) => setWasteForm({ ...wasteForm, category: e.target.value as (typeof wasteCategories)[number] })} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{wasteCategories.map((c) => <option key={c} value={c}>{categoryLabel(c)}</option>)}</select></label>
              <Select label={t.unit} value={wasteForm.unitId} onChange={(v) => setWasteForm({ ...wasteForm, unitId: v })}>{unitOptions}</Select>
              <Field label={t.quantity} value={wasteForm.quantity} onChange={(v) => setWasteForm({ ...wasteForm, quantity: v })} type="number" />
              <Field label={t.reason} value={wasteForm.reason} onChange={(v) => setWasteForm({ ...wasteForm, reason: v })} max={500} />
              <Field label={t.note} value={wasteForm.note} onChange={(v) => setWasteForm({ ...wasteForm, note: v })} max={500} />
              <Field label={t.photoUrl} value={wasteForm.photoUrl} onChange={(v) => setWasteForm({ ...wasteForm, photoUrl: v })} max={500} />
              <Field label={t.reference} value={wasteForm.reference} onChange={(v) => setWasteForm({ ...wasteForm, reference: v })} max={200} />
            </div>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white"><Save size={18} />{t.recordWaste}</button>
          </form>
        </section>

        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.costing}</h2>
          <div className="mt-3 flex flex-wrap gap-2">
            <button onClick={() => void loadValuation()} className="min-h-10 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white">{t.viewValuation}</button>
            <button onClick={() => void loadCosting()} className="min-h-10 rounded-lg border border-[#0e5a4f] px-3 text-sm font-semibold text-[#0e5a4f]">{t.viewSummary}</button>
          </div>
          {valuation && (
            <div className="mt-4">
              <p className="text-sm text-[#64716b]">{t.totalValue}: <span className="font-semibold text-[#0e5a4f]">{money(valuation.totalValue)}</span></p>
              <ul className="mt-3 max-h-72 space-y-1 overflow-y-auto text-sm">{valuation.rows.map((r) => <li key={r.id} className="flex items-center justify-between"><span className="min-w-0">{r.sku} · {language === "ar" ? r.itemNameAr : r.itemNameEn}</span><span className="text-[#69766f]">{fmt(r.balance)} × {money(r.unitCost)} = {money(r.value)}</span></li>)}</ul>
            </div>
          )}
          {costRows && (
            <div className="mt-4">
              <ul className="mt-2 divide-y divide-[#e8ece8] text-sm">{costRows.map((r, i) => <li key={i} className="flex flex-wrap items-center justify-between gap-2 py-2"><span className="min-w-0 font-medium">{r.productNameAr ?? r.productNameEn}</span><span className="text-[#69766f]">{t.recipeCost}: {money(r.recipeCost)} · {t.sellingPrice}: {money(r.sellingPrice)}<br />{t.foodCostPercent}: {money(r.foodCostPercent)}% · {t.grossMargin}: {money(r.grossMargin)} ({money(r.grossMarginPercent)}%)</span></li>)}</ul>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}

type CountLineForm = { inventoryItemId: string; countedQuantity: string; reason: string };
type TransferLineForm = { inventoryItemId: string; quantity: string };

function Field({ label, value, onChange, max, type = "text", required = false }: { label: string; value: string; onChange: (value: string) => void; max?: number; type?: string; required?: boolean }) {
  return <label className="block text-sm font-medium">{label}{required && " *"}<input required={required} type={type} step={type === "number" ? "any" : undefined} min={type === "number" ? 0 : undefined} value={value} onChange={(e) => onChange(e.target.value)} maxLength={max} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20" /></label>;
}

function Select({ label, value, onChange, children }: { label: string; value: string; onChange: (value: string) => void; children: React.ReactNode }) {
  return <label className="block text-sm font-medium">{label}<select value={value} onChange={(e) => onChange(e.target.value)} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20">{children}</select></label>;
}
