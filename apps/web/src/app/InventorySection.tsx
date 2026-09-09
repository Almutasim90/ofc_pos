import { useEffect, useState } from "react";
import { Boxes, PackagePlus, RefreshCw, Save, Scale, Plus } from "lucide-react";
import { createId, store } from "@/lib/local-store";
import { FormDialog } from "@/app/FormDialog";

type Language = "ar" | "en";
type Branch = { id: string; nameAr: string; nameEn: string };
type Product = { id: string; sku: string; nameAr: string; nameEn: string };
type Uom = { id: string; code: string; nameAr: string; nameEn: string; symbol: string | null; isActive: boolean; sortOrder: number };
type Conversion = { id: string; fromUnitId: string; toUnitId: string; fromCode: string | null; toCode: string | null; factor: number; isActive: boolean };
type Item = { id: string; sku: string; barcode: string | null; nameAr: string; nameEn: string; type: "RawMaterial" | "Packaging" | "SemiFinished" | "FinishedProduct"; baseUnitId: string; unitCost: number; stockOnHand: number | null; isActive: boolean };
type Recipe = { id: string; productId: string; productSku: string | null; productNameAr: string | null; productNameEn: string | null; nameAr: string | null; nameEn: string | null; versionNumber: number; status: "Draft" | "Active" | "Archived"; effectiveFrom: string; createdAt: string; lineCount: number; createdByUserId: string };
type RecipeLine = { id: string; inventoryItemId: string; itemNameAr: string | null; itemNameEn: string | null; quantity: number; unitId: string; unitCode: string | null };
type RecipeDetail = Recipe & { lines: RecipeLine[] };
type StockRow = { id: string; sku: string; itemNameAr: string; itemNameEn: string; baseUnitId: string; baseUnitCode: string | null; balance: number; cachedStockOnHand: number | null };
type Movement = { id: string; inventoryItemId: string; itemNameAr: string | null; itemNameEn: string | null; type: string; quantity: number; unitId: string; reference: string | null; reason: string | null; recipeVersionId: string | null; orderId: string | null; occurredAt: string };
type DeductionLine = { inventoryItemId: string; itemNameAr: string; itemNameEn: string; sourceUnitId: string; sourceUnitCode: string | null; sourceQuantity: number; baseUnitId: string; baseUnitCode: string | null; baseQuantity: number; convertible: boolean };
type DeductionResult = { productId: string; productSku: string | null; productNameAr: string | null; productNameEn: string | null; quantity: number; recipeVersionId: string | null; recipeVersionNumber: number | null; note?: string; lines: DeductionLine[] };

const itemTypes = ["RawMaterial", "Packaging", "SemiFinished", "FinishedProduct"] as const;
const movementTypes = ["Opening", "Purchase", "Waste", "Adjustment", "Return", "TransferIn", "TransferOut", "CountAdjustment"] as const;

const copy = {
  ar: {
    title: "المخزون والوصفات", intro: "المواد الخام ووحدات القياس والتحويلات ووصفات المنتجات وسجل الحركات المخزنية.", isolated: "التتبع يتم بسجل حركات غير قابل للتعديل.", loading: "جارٍ التحميل", reload: "تحديث", saved: "تم الحفظ بنجاح.", failed: "تعذر تنفيذ العملية.", empty: "لا توجد بيانات بعد.", none: "لا يوجد", close: "إغلاق",
    branch: "الفرع", units: "وحدات القياس", conversions: "التحويلات", convert: "تحويل", addUnit: "إضافة وحدة", code: "الرمز", nameAr: "الاسم بالعربية", nameEn: "الاسم بالإنجليزية", symbol: "الرمز المختصر", sortOrder: "الترتيب", addConversion: "إضافة تحويل", fromUnit: "من وحدة", toUnit: "إلى وحدة", factor: "المعامل", convertTitle: "حساب تحويل", quantity: "الكمية", result: "النتيجة", convertAction: "تحويل", add: "إضافة",
    items: "المواد الخام", addItem: "إضافة مادة", sku: "رمز الصنف", barcode: "الباركود", type: "النوع", baseUnit: "الوحدة الأساسية", unitCost: "تكلفة الوحدة", active: "نشط", inactive: "غير نشط", typeRawMaterial: "مادة خام", typePackaging: "تغليف", typeSemiFinished: "نصف مصنّع", typeFinishedProduct: "منتج نهائي",
    recipes: "الوصفات (BOM)", addRecipe: "إضافة وصفة", product: "المنتج", version: "الإصدار", status: "الحالة", linesCount: "أسطر", activate: "تفعيل", revise: "نسخة جديدة", draft: "مسودة", activeRecipe: "نشطة", archived: "مؤرشفة", addLine: "إضافة سطر", ingredient: "المادة", lineQuantity: "الكمية", lineUnit: "الوحدة", remove: "إزالة", create: "إنشاء", recipeLines: "الأسطر", view: "عرض", effectiveFrom: "بدء السريان",
    stock: "المخزون والحركات", balance: "الرصيد", addMovement: "تسجيل حركة", movementType: "نوع الحركة", reference: "مرجع", reason: "السبب", record: "تسجيل", movements: "سجل الحركات", saleDeduction: "خصم المبيعات", productQty: "الكمية المباعة", preview: "معاينة الخصم", post: "تنفيذ الخصم", deductionTitle: "خصومات المكونات", noMovement: "لا توجد حركات",
    stOpening: "افتتاحي", stPurchase: "شراء", stSaleDeduction: "خصم مبيعات", stWaste: "هدر", stTransferIn: "تحويل وارد", stTransferOut: "تحويل صادر", stAdjustment: "تسوية", stReturn: "مرتجع", stCountAdjustment: "تسوية جرد"
  } as const,
  en: {
    title: "Inventory & recipes", intro: "Raw materials, units of measure and conversions, product recipes, and the stock movement ledger.", isolated: "Ledger entries are immutable; the balance is rebuilt from movements.", loading: "Loading", reload: "Refresh", saved: "Saved successfully.", failed: "Unable to complete the operation.", empty: "No data yet.", none: "None", close: "Close",
    branch: "Branch", units: "Units of measure", conversions: "Conversions", convert: "Convert", addUnit: "Add unit", code: "Code", nameAr: "Arabic name", nameEn: "English name", symbol: "Symbol", sortOrder: "Sort order", addConversion: "Add conversion", fromUnit: "From unit", toUnit: "To unit", factor: "Factor", convertTitle: "Convert quantity", quantity: "Quantity", result: "Result", convertAction: "Convert", add: "Add",
    items: "Raw materials", addItem: "Add item", sku: "SKU", barcode: "Barcode", type: "Type", baseUnit: "Base unit", unitCost: "Unit cost", active: "Active", inactive: "Inactive", typeRawMaterial: "Raw material", typePackaging: "Packaging", typeSemiFinished: "Semi-finished", typeFinishedProduct: "Finished product",
    recipes: "Recipes (BOM)", addRecipe: "Add recipe", product: "Product", version: "Version", status: "Status", linesCount: "Lines", activate: "Activate", revise: "New version", draft: "Draft", activeRecipe: "Active", archived: "Archived", addLine: "Add line", ingredient: "Ingredient", lineQuantity: "Quantity", lineUnit: "Unit", remove: "Remove", create: "Create", recipeLines: "Lines", view: "View", effectiveFrom: "Effective from",
    stock: "Stock & movements", balance: "Balance", addMovement: "Record movement", movementType: "Movement type", reference: "Reference", reason: "Reason", record: "Record", movements: "Movement ledger", saleDeduction: "Sale deduction", productQty: "Sold quantity", preview: "Preview deduction", post: "Post deduction", deductionTitle: "Ingredient deductions", noMovement: "No movements",
    stOpening: "Opening", stPurchase: "Purchase", stSaleDeduction: "Sale deduction", stWaste: "Waste", stTransferIn: "Transfer in", stTransferOut: "Transfer out", stAdjustment: "Adjustment", stReturn: "Return", stCountAdjustment: "Count adjustment"
  } as const,
} as const;

export function InventorySection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) => (language === "ar" ? x.nameAr : x.nameEn);
  const fmt = (v: number | null | undefined) => v === null || v === undefined ? "—" : new Intl.NumberFormat(language, { maximumFractionDigits: 6 }).format(v);

  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [units, setUnits] = useState<Uom[]>([]);
  const [conversions, setConversions] = useState<Conversion[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [recipes, setRecipes] = useState<Recipe[]>([]);
  const [detail, setDetail] = useState<RecipeDetail | null>(null);
  const [stock, setStock] = useState<StockRow[]>([]);
  const [movements, setMovements] = useState<Movement[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);

  const [unitForm, setUnitForm] = useState({ code: "", nameAr: "", nameEn: "", symbol: "", sortOrder: "" });
  const [convForm, setConvForm] = useState({ fromUnitId: "", toUnitId: "", factor: "" });
  const [convertForm, setConvertForm] = useState({ fromUnitId: "", toUnitId: "", quantity: "" });
  const [convertResult, setConvertResult] = useState<{ convertedQuantity: number; toUnitCode: string | null } | null>(null);
  const [itemForm, setItemForm] = useState({ sku: "", barcode: "", nameAr: "", nameEn: "", type: "RawMaterial" as (typeof itemTypes)[number], baseUnitId: "", unitCost: "" });
  const [editingItem, setEditingItem] = useState<Item | null>(null);
  const [inventoryTab, setInventoryTab] = useState("stock");
  const [recipeFilter, setRecipeFilter] = useState("");
  const [recipeForm, setRecipeForm] = useState({ productId: "", nameAr: "", nameEn: "", effectiveFrom: "" });
  const [recipeLines, setRecipeLines] = useState<RecipeLineForm[]>([]);
  const [movementForm, setMovementForm] = useState({ itemId: "", type: "Opening" as (typeof movementTypes)[number], unitId: "", quantity: "", reference: "", reason: "" });
  const [deductionForm, setDeductionForm] = useState({ productId: "", quantity: "" });
  const [deductionPreview, setDeductionPreview] = useState<DeductionResult[] | null>(null);
  const [unitDialogOpen, setUnitDialogOpen] = useState(false);
  const [conversionDialogOpen, setConversionDialogOpen] = useState(false);
  const [itemDialogOpen, setItemDialogOpen] = useState(false);
  const [recipeDialogOpen, setRecipeDialogOpen] = useState(false);
  const [movementDialogOpen, setMovementDialogOpen] = useState(false);

  const setMsg = (value: string, error = false) => { setMessage(value); setIsError(error); };
  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });
  const updateRecipeLine = (index: number, patch: Partial<RecipeLineForm>) => setRecipeLines(recipeLines.map((line, i) => i === index ? { ...line, ...patch } : line));

  async function loadContext() {
    const response = await auth("/api/v1/pos/context");
    if (!response.ok) return;
    const value = await response.json() as { branches: Branch[] };
    setBranches(value.branches);
    if (value.branches[0]) setBranchId(value.branches[0].id);
  }

  async function loadUoms() {
    const response = await auth("/api/v1/inventory/uoms");
    if (!response.ok) return;
    const value = await response.json() as { units: Uom[]; conversions: Conversion[] };
    setUnits(value.units); setConversions(value.conversions);
  }

  async function loadItems() { const response = await auth("/api/v1/inventory/items"); if (response.ok) setItems(await response.json() as Item[]); }
  async function loadProducts() { const response = await auth("/api/v1/products"); if (response.ok) setProducts(await response.json() as Product[]); }
  async function loadRecipes() { const response = await auth(recipeFilter ? `/api/v1/inventory/recipes?productId=${recipeFilter}` : "/api/v1/inventory/recipes"); if (response.ok) setRecipes(await response.json() as Recipe[]); }
  async function loadStock() { if (!branchId) return; const response = await auth(`/api/v1/inventory/stock?branchId=${branchId}`); if (response.ok) setStock(await response.json() as StockRow[]); }
  async function loadMovements() { if (!branchId) return; const response = await auth(`/api/v1/inventory/movements?branchId=${branchId}`); if (response.ok) setMovements(await response.json() as Movement[]); }

  useEffect(() => { void loadContext(); void loadProducts(); }, []);
  useEffect(() => { void loadUoms(); void loadItems(); }, []);
  useEffect(() => { void loadRecipes(); }, [recipeFilter]);
  useEffect(() => { if (branchId) { void loadStock(); void loadMovements(); } }, [branchId]);

  async function refreshAll() {
    setMsg(""); setLoading(true);
    try {
      await Promise.all([loadUoms(), loadItems(), loadRecipes(), branchId ? loadStock() : Promise.resolve(), branchId ? loadMovements() : Promise.resolve()]);
    } finally { setLoading(false); }
  }

  async function createUnit(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    try {
      const response = await auth("/api/v1/inventory/uoms", { method: "POST", body: JSON.stringify({ code: unitForm.code, nameAr: unitForm.nameAr, nameEn: unitForm.nameEn, symbol: unitForm.symbol.trim() || null, sortOrder: unitForm.sortOrder === "" ? 0 : Number(unitForm.sortOrder) }) });
      if (!response.ok) { const p = await response.json().catch(() => null); throw new Error(p?.errors?.uom?.[0] ?? p?.errors?.code?.[0] ?? t.failed); }
      setMsg(t.saved); setUnitForm({ code: "", nameAr: "", nameEn: "", symbol: "", sortOrder: "" }); setUnitDialogOpen(false); await loadUoms();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function createConversion(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (!convForm.fromUnitId || !convForm.toUnitId || !convForm.factor) { setMsg(t.failed, true); return; }
    try {
      const response = await auth("/api/v1/inventory/conversions", { method: "POST", body: JSON.stringify({ fromUnitId: convForm.fromUnitId, toUnitId: convForm.toUnitId, factor: Number(convForm.factor) }) });
      if (!response.ok) { const p = await response.json().catch(() => null); throw new Error(p?.errors?.factor?.[0] ?? p?.errors?.units?.[0] ?? t.failed); }
      setMsg(t.saved); setConvForm({ fromUnitId: "", toUnitId: "", factor: "" }); setConversionDialogOpen(false); await loadUoms();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function doConvert(event: React.FormEvent) {
    event.preventDefault(); setMsg(""); setConvertResult(null);
    if (!convertForm.fromUnitId || !convertForm.toUnitId || !convertForm.quantity) { setMsg(t.failed, true); return; }
    try {
      const response = await auth("/api/v1/inventory/conversions/convert", { method: "POST", body: JSON.stringify({ fromUnitId: convertForm.fromUnitId, toUnitId: convertForm.toUnitId, quantity: Number(convertForm.quantity) }) });
      if (!response.ok) { const p = await response.json().catch(() => null); throw new Error(p?.errors?.units?.[0] ?? t.failed); }
      setConvertResult(await response.json() as { convertedQuantity: number; toUnitCode: string | null });
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function createItem(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (!itemForm.baseUnitId) { setMsg(t.failed, true); return; }
    try {
      const response = await auth(editingItem ? `/api/v1/inventory/items/${editingItem.id}` : "/api/v1/inventory/items", { method: editingItem ? "PUT" : "POST", body: JSON.stringify({ isActive: editingItem?.isActive ?? true, sku: itemForm.sku, barcode: itemForm.barcode.trim() || null, nameAr: itemForm.nameAr, nameEn: itemForm.nameEn, type: itemForm.type, baseUnitId: itemForm.baseUnitId, unitCost: Number(itemForm.unitCost) || 0 }) });
      if (!response.ok) { const p = await response.json().catch(() => null); throw new Error(p?.errors?.sku?.[0] ?? p?.errors?.item?.[0] ?? p?.errors?.baseUnitId?.[0] ?? t.failed); }
      setEditingItem(null); setMsg(t.saved); setItemForm({ sku: "", barcode: "", nameAr: "", nameEn: "", type: "RawMaterial", baseUnitId: "", unitCost: "" }); setItemDialogOpen(false); await loadItems();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function createRecipe(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (recipeLines.length === 0 || recipeLines.some((l) => !l.inventoryItemId || !l.unitId || !l.quantity)) { setMsg(t.failed, true); return; }
    try {
      const lines = recipeLines.map((l) => ({ inventoryItemId: l.inventoryItemId, unitId: l.unitId, quantity: Number(l.quantity) }));
      const response = await auth("/api/v1/inventory/recipes", { method: "POST", body: JSON.stringify({ productId: recipeForm.productId, nameAr: recipeForm.nameAr.trim() || null, nameEn: recipeForm.nameEn.trim() || null, effectiveFrom: recipeForm.effectiveFrom ? new Date(recipeForm.effectiveFrom).toISOString() : null, lines }) });
      if (!response.ok) { const p = await response.json().catch(() => null); throw new Error(p?.errors?.lines?.[0] ?? p?.errors?.productId?.[0] ?? t.failed); }
      setMsg(t.saved); setRecipeForm({ productId: "", nameAr: "", nameEn: "", effectiveFrom: "" }); setRecipeLines([]); setDetail(null); setRecipeDialogOpen(false); await loadRecipes();
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function openRecipe(id: string) {
    const response = await auth(`/api/v1/inventory/recipes/${id}`);
    if (response.ok) setDetail(await response.json() as RecipeDetail);
  }

  async function activate(id: string) { setMsg(""); const r = await auth(`/api/v1/inventory/recipes/${id}/activate`, { method: "POST" }); if (!r.ok) { setMsg(t.failed, true); return; } setMsg(t.saved); setDetail(null); await loadRecipes(); }

  async function revise(id: string) {
    setMsg("");
    if (!detail || detail.lines.length === 0) { setMsg(t.failed, true); return; }
    const lines = detail.lines.map((l) => ({ inventoryItemId: l.inventoryItemId, unitId: l.unitId, quantity: l.quantity }));
    const r = await auth(`/api/v1/inventory/recipes/${id}/revise`, { method: "POST", body: JSON.stringify({ productId: detail.productId, nameAr: detail.nameAr, nameEn: detail.nameEn, effectiveFrom: null, lines }) });
    if (!r.ok) { setMsg(t.failed, true); return; } setMsg(t.saved); setDetail(null); await loadRecipes();
  }

  async function recordMovement(event: React.FormEvent) {
    event.preventDefault(); setMsg("");
    if (!movementForm.itemId || !movementForm.unitId) { setMsg(t.failed, true); return; }
    try {
      const response = await auth("/api/v1/inventory/movements", { method: "POST", body: JSON.stringify({ branchId, itemId: movementForm.itemId, unitId: movementForm.unitId, type: movementForm.type, quantity: Number(movementForm.quantity), reference: movementForm.reference.trim() || null, reason: movementForm.reason.trim() || null, clientMovementId: createId() }) });
      if (!response.ok) { const p = await response.json().catch(() => null); throw new Error(p?.errors?.quantity?.[0] ?? p?.errors?.unit?.[0] ?? p?.errors?.item?.[0] ?? t.failed); }
      setMsg(t.saved); setMovementForm({ itemId: "", type: "Opening", unitId: "", quantity: "", reference: "", reason: "" }); setMovementDialogOpen(false); await Promise.all([loadStock(), loadMovements()]);
    } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); }
  }

  async function previewDeduction(event: React.FormEvent) {
    event.preventDefault(); setMsg(""); setDeductionPreview(null);
    if (!deductionForm.productId || !deductionForm.quantity) { setMsg(t.failed, true); return; }
    try {
      const response = await auth("/api/v1/inventory/deductions/preview", { method: "POST", body: JSON.stringify({ branchId, items: [{ productId: deductionForm.productId, quantity: Number(deductionForm.quantity) }] }) });
      if (!response.ok) throw new Error(t.failed);
      setDeductionPreview(await response.json() as DeductionResult[]);
    } catch { setMsg(t.failed, true); }
  }

  async function postDeduction() {
    setMsg("");
    if (!deductionPreview || deductionPreview.length === 0) { setMsg(t.failed, true); return; }
    try {
      const response = await auth("/api/v1/inventory/deductions", { method: "POST", body: JSON.stringify({ branchId, orderId: null, reference: `batch-${createId()}`, items: deductionPreview.map((d) => ({ productId: d.productId, quantity: d.quantity })) }) });
      if (!response.ok) throw new Error(t.failed);
      setDeductionPreview(null); setMsg(t.saved); await Promise.all([loadStock(), loadMovements()]);
    } catch { setMsg(t.failed, true); }
  }

  const typeLabel = (v: string) => v === "RawMaterial" ? t.typeRawMaterial : v === "Packaging" ? t.typePackaging : v === "SemiFinished" ? t.typeSemiFinished : t.typeFinishedProduct;
  const statusLabel = (v: string) => v === "Draft" ? t.draft : v === "Active" ? t.activeRecipe : t.archived;
  const movementLabel = (v: string) => v === "Opening" ? t.stOpening : v === "Purchase" ? t.stPurchase : v === "SaleDeduction" ? t.stSaleDeduction : v === "Waste" ? t.stWaste : v === "TransferIn" ? t.stTransferIn : v === "TransferOut" ? t.stTransferOut : v === "Adjustment" ? t.stAdjustment : v === "Return" ? t.stReturn : v === "CountAdjustment" ? t.stCountAdjustment : v;
  const unitOptions = units.map((u) => <option key={u.id} value={u.id}>{u.code} · {name(u)}</option>);
  const itemOptions = items.map((i) => <option key={i.id} value={i.id}>{i.sku} · {name(i)}</option>);

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <p className="mt-3 max-w-3xl text-[#64716b]">{t.intro}</p>
      <div className="mt-4 flex flex-wrap items-center gap-3 rounded-xl border border-[#d9dfd7] bg-[#edf5f1] p-3 text-sm text-[#08483f]"><Boxes size={18} /><span>{t.isolated}</span><button onClick={() => void refreshAll()} className="ml-auto inline-flex min-h-9 items-center gap-2 rounded-lg bg-[#0e5a4f] px-3 text-xs font-semibold text-white"><RefreshCw size={15} />{t.reload}</button></div>

      <div className="mt-5 max-w-md">
        <label className="block text-sm font-medium">{t.branch}
          <select value={branchId} onChange={(e) => setBranchId(e.target.value)} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20">{branches.map((b) => <option key={b.id} value={b.id}>{name(b)}</option>)}</select>
        </label>
      </div>

      {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}
      {loading && <div className="mt-4 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div>}

      <nav aria-label={language === "ar" ? "أقسام المخزون" : "Inventory sections"} className="mt-5 flex flex-wrap gap-2">{[["stock", t.stock], ["items", t.items], ["recipes", t.recipes], ["units", t.units]].map(([id, label]) => <button key={id} onClick={() => setInventoryTab(id)} aria-pressed={inventoryTab === id} className={`min-h-11 rounded-lg border px-4 text-sm ${inventoryTab === id ? "bg-[#0e5a4f] text-white" : "bg-white"}`}>{label}</button>)}</nav>
      <div className="mt-6 grid gap-5">
        <section hidden={inventoryTab !== "units"} className="rounded-xl border border-[#dfe5df] bg-white p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">{t.units}</h2><button onClick={() => setUnitDialogOpen(true)} className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.addUnit}</button></div>
          {units.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 grid gap-2 sm:grid-cols-2">{units.map((u) => <li key={u.id} className="flex items-center justify-between rounded-lg bg-[#f4f7f4] px-3 py-2 text-sm"><span>{u.code} · {name(u)}</span><span className="text-xs text-[#69766f]">{u.symbol ?? ""}</span></li>)}</ul>
          )}
          {unitDialogOpen && <FormDialog title={t.addUnit} closeLabel={t.close} onClose={() => setUnitDialogOpen(false)}><form onSubmit={createUnit} className="rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <Field label={t.code} value={unitForm.code} onChange={(v) => setUnitForm({ ...unitForm, code: v })} max={50} />
              <Field label={t.symbol} value={unitForm.symbol} onChange={(v) => setUnitForm({ ...unitForm, symbol: v })} max={20} />
              <Field label={t.nameAr} value={unitForm.nameAr} onChange={(v) => setUnitForm({ ...unitForm, nameAr: v })} max={160} />
              <Field label={t.nameEn} value={unitForm.nameEn} onChange={(v) => setUnitForm({ ...unitForm, nameEn: v })} max={160} />
            </div>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.add}</button>
          </form></FormDialog>}
          <section className="mt-5 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <div className="flex flex-wrap items-center justify-between gap-3"><h3 className="font-semibold">{t.conversions}</h3><button onClick={() => setConversionDialogOpen(true)} className="inline-flex min-h-10 items-center gap-2 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white"><Plus size={16} />{t.addConversion}</button></div>
            {conversions.length === 0 ? <p className="mt-2 text-sm text-[#69766f]">{t.empty}</p> : (
              <ul className="mt-2 divide-y divide-[#e8ece8]">{conversions.map((c) => <li key={c.id} className="flex items-center justify-between gap-2 py-2 text-sm"><span>{c.fromCode} → {c.toCode}</span><span className="font-medium">{fmt(c.factor)}</span></li>)}</ul>
            )}
            {conversionDialogOpen && <FormDialog title={t.addConversion} closeLabel={t.close} onClose={() => setConversionDialogOpen(false)}><form onSubmit={createConversion} className="grid gap-3 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4 sm:grid-cols-3">
              <Select label={t.fromUnit} value={convForm.fromUnitId} onChange={(v) => setConvForm({ ...convForm, fromUnitId: v })}>{unitOptions}</Select>
              <Select label={t.toUnit} value={convForm.toUnitId} onChange={(v) => setConvForm({ ...convForm, toUnitId: v })}>{unitOptions}</Select>
              <Field label={t.factor} value={convForm.factor} onChange={(v) => setConvForm({ ...convForm, factor: v })} type="number" />
              <button className="inline-flex min-h-10 items-center justify-center gap-2 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white"><Plus size={16} />{t.addConversion}</button>
            </form></FormDialog>}
            <form onSubmit={doConvert} className="mt-4 grid gap-3 sm:grid-cols-3">
              <Select label={t.fromUnit} value={convertForm.fromUnitId} onChange={(v) => setConvertForm({ ...convertForm, fromUnitId: v })}>{unitOptions}</Select>
              <Select label={t.toUnit} value={convertForm.toUnitId} onChange={(v) => setConvertForm({ ...convertForm, toUnitId: v })}>{unitOptions}</Select>
              <Field label={t.quantity} value={convertForm.quantity} onChange={(v) => setConvertForm({ ...convertForm, quantity: v })} type="number" />
              <button type="submit" className="inline-flex min-h-10 items-center justify-center gap-2 rounded-lg bg-[#137347] px-3 text-sm font-semibold text-white sm:col-span-3">{t.convertTitle}</button>
            </form>
            {convertResult && <p className="mt-3 text-sm text-[#0e5a4f]">{fmt(convertForm.quantity === "" ? null : Number(convertForm.quantity))} → {fmt(convertResult.convertedQuantity)} {convertResult.toUnitCode ?? ""}</p>}
          </section>
        </section>

        <section hidden={inventoryTab !== "items"} className="rounded-xl border border-[#dfe5df] bg-white p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">{t.items}</h2><button onClick={() => { setEditingItem(null); setItemForm({ sku: "", barcode: "", nameAr: "", nameEn: "", type: "RawMaterial", baseUnitId: "", unitCost: "" }); setItemDialogOpen(true); }} className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.addItem}</button></div>
          {items.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 divide-y divide-[#e8ece8]">{items.map((item) => <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm"><div><span className="font-medium">{item.sku}</span><span className="text-[#69766f]"> · {name(item)}</span></div><span className="text-xs text-[#69766f]">{typeLabel(item.type)} · {fmt(item.unitCost)}</span><button onClick={() => { setEditingItem(item); setItemForm({ sku: item.sku, barcode: item.barcode ?? "", nameAr: item.nameAr, nameEn: item.nameEn, type: item.type, baseUnitId: item.baseUnitId, unitCost: item.unitCost.toString() }); setItemDialogOpen(true); }} className="min-h-11 rounded-lg border px-3 font-semibold text-[#0e5a4f]">{language === "ar" ? "تعديل" : "Edit"}</button></li>)}</ul>
          )}
          {itemDialogOpen && <FormDialog title={editingItem ? (language === "ar" ? "تعديل المادة" : "Edit item") : t.addItem} closeLabel={t.close} onClose={() => { setItemDialogOpen(false); setEditingItem(null); setItemForm({ sku: "", barcode: "", nameAr: "", nameEn: "", type: "RawMaterial", baseUnitId: "", unitCost: "" }); }}><form onSubmit={createItem} className="rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <Field required label={t.sku} value={itemForm.sku} onChange={(v) => setItemForm({ ...itemForm, sku: v })} max={64} />
              <Field label={t.barcode} value={itemForm.barcode} onChange={(v) => setItemForm({ ...itemForm, barcode: v })} max={64} />
              <Field required label={t.nameAr} value={itemForm.nameAr} onChange={(v) => setItemForm({ ...itemForm, nameAr: v })} max={160} />
              <Field required label={t.nameEn} value={itemForm.nameEn} onChange={(v) => setItemForm({ ...itemForm, nameEn: v })} max={160} />
              <label className="block text-sm font-medium">{t.type}<select disabled={!!editingItem} value={itemForm.type} onChange={(e) => setItemForm({ ...itemForm, type: e.target.value as (typeof itemTypes)[number] })} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{itemTypes.map((tt) => <option key={tt} value={tt}>{typeLabel(tt)}</option>)}</select></label>
              <Select disabled={!!editingItem} label={t.baseUnit} value={itemForm.baseUnitId} onChange={(v) => setItemForm({ ...itemForm, baseUnitId: v })}>{unitOptions}</Select>
              <Field label={t.unitCost} value={itemForm.unitCost} onChange={(v) => setItemForm({ ...itemForm, unitCost: v })} type="number" />
            </div>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{editingItem ? (language === "ar" ? "حفظ التعديلات" : "Save changes") : t.add}</button>
          {editingItem && <><label className="mt-3 flex min-h-11 items-center gap-2"><input type="checkbox" checked={editingItem.isActive} onChange={e => setEditingItem({ ...editingItem, isActive: e.target.checked })} />{t.active}</label><p className="mt-2 text-xs text-[#64716b]">{language === "ar" ? "الوحدة ونوع المادة ثابتان لحماية الحركات السابقة. تكلفة مادة لها حركات تُحدّث من المشتريات." : "Unit and type stay fixed to preserve past movements. Cost for an item with movements is updated through purchasing."}</p><button type="button" onClick={() => { setItemDialogOpen(false); setEditingItem(null); setItemForm({ sku: "", barcode: "", nameAr: "", nameEn: "", type: "RawMaterial", baseUnitId: "", unitCost: "" }); }} className="mt-3 min-h-11 rounded-lg border px-4">{language === "ar" ? "إلغاء التعديل" : "Cancel editing"}</button></>}</form></FormDialog>}
        </section>

        <section hidden={inventoryTab !== "recipes"} className="rounded-xl border border-[#dfe5df] bg-white p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">{t.recipes}</h2><button onClick={() => setRecipeDialogOpen(true)} className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.addRecipe}</button></div>
          <div className="mt-3 flex flex-col gap-3 sm:flex-row">
            <Select label={t.product} value={recipeFilter} onChange={setRecipeFilter}><option value="">{t.none}</option>{products.map((p) => <option key={p.id} value={p.id}>{p.sku} · {name(p)}</option>)}</Select>
          </div>
          {recipes.length === 0 ? <p className="mt-4 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-4 space-y-2">{recipes.map((r) => <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-[#e8ece8] px-3 py-2 text-sm"><span className="min-w-0">{r.productSku ?? "—"} · v{r.versionNumber} <span className="text-[#69766f]">({r.lineCount} {t.linesCount})</span></span><div className="flex items-center gap-1.5"><span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${r.status === "Active" ? "bg-[#e3f4ea] text-[#137347]" : r.status === "Draft" ? "bg-[#f4f1e3] text-[#8a6d1f]" : "bg-[#e8ece8] text-[#53615b]"}`}>{statusLabel(r.status)}</span>{r.status === "Active" && <button onClick={() => void revise(r.id)} className="min-h-8 rounded-lg border border-[#0e5a4f] px-2.5 text-xs font-semibold text-[#0e5a4f]">{t.revise}</button>}{r.status === "Draft" && <button onClick={() => void activate(r.id)} className="min-h-8 rounded-lg bg-[#137347] px-2.5 text-xs font-semibold text-white">{t.activate}</button>}<button onClick={() => void openRecipe(r.id)} className="min-h-8 rounded-lg bg-[#edf5f1] px-2.5 text-xs font-semibold text-[#0e5a4f]">{t.view}</button></div></li>)}</ul>
          )}
          {(detail || recipeForm.productId) && <div className="mt-4 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4"><h3 className="font-semibold">{t.recipeLines}</h3>{detail ? <ol className="mt-2 space-y-1 text-sm">{detail.lines.map((l) => <li key={l.id} className="flex items-center justify-between"><span>{l.itemNameAr ?? l.itemNameEn ?? ""} · {fmt(l.quantity)} {l.unitCode ?? ""}</span></li>)}</ol> : <p className="mt-2 text-sm text-[#69766f]">{t.empty}</p>}</div>}
          {recipeDialogOpen && <FormDialog title={t.addRecipe} closeLabel={t.close} onClose={() => setRecipeDialogOpen(false)}><form onSubmit={createRecipe} className="rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <Select label={t.product} value={recipeForm.productId} onChange={(v) => setRecipeForm({ ...recipeForm, productId: v })}>{products.map((p) => <option key={p.id} value={p.id}>{p.sku} · {name(p)}</option>)}</Select>
              <Field label={t.effectiveFrom} value={recipeForm.effectiveFrom} onChange={(v) => setRecipeForm({ ...recipeForm, effectiveFrom: v })} type="date" />
              <Field label={t.nameAr} value={recipeForm.nameAr} onChange={(v) => setRecipeForm({ ...recipeForm, nameAr: v })} max={160} />
              <Field label={t.nameEn} value={recipeForm.nameEn} onChange={(v) => setRecipeForm({ ...recipeForm, nameEn: v })} max={160} />
            </div>
            <div className="mt-4 space-y-2">
              {recipeLines.map((l, index) => (
                <div key={index} className="grid gap-2 sm:grid-cols-[1fr_1fr_5rem_auto]">
                  <Select label={t.ingredient} value={l.inventoryItemId} onChange={(v) => updateRecipeLine(index, { inventoryItemId: v })}>{itemOptions}</Select>
                  <Select label={t.lineUnit} value={l.unitId} onChange={(v) => updateRecipeLine(index, { unitId: v })}>{unitOptions}</Select>
                  <Field label={t.lineQuantity} value={l.quantity} onChange={(v) => updateRecipeLine(index, { quantity: v })} type="number" />
                  <button type="button" onClick={() => setRecipeLines(recipeLines.filter((_, i) => i !== index))} className="min-h-10 rounded-lg border border-[#b4322a] px-2 text-sm text-[#b4322a]">{t.remove}</button>
                </div>
              ))}
            </div>
            <button type="button" onClick={() => setRecipeLines([...recipeLines, { inventoryItemId: "", unitId: "", quantity: "" }])} className="mt-3 inline-flex min-h-10 items-center gap-2 rounded-lg border border-[#0e5a4f] px-3 text-sm font-semibold text-[#0e5a4f]"><Scale size={16} />{t.addLine}</button>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Save size={18} />{t.create}</button>
          </form></FormDialog>}
        </section>

        <section hidden={inventoryTab !== "stock"} className="rounded-xl border border-[#dfe5df] bg-white p-5"><div className="flex flex-wrap items-center justify-between gap-3"><h2 className="font-semibold">{t.stock}</h2><button onClick={() => setMovementDialogOpen(true)} className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><PackagePlus size={18} />{t.addMovement}</button></div>
          {!branchId ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : stock.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.empty}</p> : (
            <ul className="mt-3 divide-y divide-[#e8ece8]">{stock.map((row) => <li key={row.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm"><span className="min-w-0">{row.sku} · {language === "ar" ? row.itemNameAr : row.itemNameEn}</span><span className={`font-semibold ${(row.balance ?? 0) < 0 ? "text-[#b4322a]" : "text-[#137347]"}`}>{fmt(row.balance)} <span className="text-xs text-[#69766f]">{row.baseUnitCode ?? ""}</span></span></li>)}</ul>
          )}
          {movementDialogOpen && <FormDialog title={t.addMovement} closeLabel={t.close} onClose={() => setMovementDialogOpen(false)}><form onSubmit={recordMovement} className="rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <div className="mt-3 grid gap-3 sm:grid-cols-2">
              <Select label={t.ingredient} value={movementForm.itemId} onChange={(v) => setMovementForm({ ...movementForm, itemId: v })}>{itemOptions}</Select>
              <label className="block text-sm font-medium">{t.movementType}<select value={movementForm.type} onChange={(e) => setMovementForm({ ...movementForm, type: e.target.value as (typeof movementTypes)[number] })} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{movementTypes.map((m) => <option key={m} value={m}>{movementLabel(m)}</option>)}</select></label>
              <Select label={t.lineUnit} value={movementForm.unitId} onChange={(v) => setMovementForm({ ...movementForm, unitId: v })}>{unitOptions}</Select>
              <Field label={t.quantity} value={movementForm.quantity} onChange={(v) => setMovementForm({ ...movementForm, quantity: v })} type="number" />
              <Field label={t.reference} value={movementForm.reference} onChange={(v) => setMovementForm({ ...movementForm, reference: v })} max={200} />
              <Field label={t.reason} value={movementForm.reason} onChange={(v) => setMovementForm({ ...movementForm, reason: v })} max={500} />
            </div>
            <button className="mt-4 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><PackagePlus size={18} />{t.record}</button>
          </form></FormDialog>}
          <div className="mt-5">
            <h3 className="font-semibold">{t.movements}</h3>
            {movements.length === 0 ? <p className="mt-2 text-sm text-[#69766f]">{t.noMovement}</p> : (
              <ul className="mt-2 max-h-72 space-y-2 overflow-y-auto">{movements.map((m) => <li key={m.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-[#f4f7f4] px-3 py-2 text-sm"><span className="min-w-0">{language === "ar" ? m.itemNameAr : m.itemNameEn ?? ""}</span><span className="rounded-full bg-[#edf5f1] px-2 py-0.5 text-xs font-medium text-[#0e5a4f]">{movementLabel(m.type)}</span><span className={`font-medium ${m.quantity < 0 ? "text-[#b4322a]" : "text-[#137347]"}`}>{fmt(m.quantity)}</span></li>)}</ul>
            )}
          </div>
          <div className="mt-5 rounded-lg border border-[#e8ece8] bg-[#fafbfa] p-4">
            <h3 className="font-semibold">{t.saleDeduction}</h3>
            <form onSubmit={previewDeduction} className="mt-3 grid gap-3 sm:grid-cols-2">
              <Select label={t.product} value={deductionForm.productId} onChange={(v) => setDeductionForm({ ...deductionForm, productId: v })}>{products.map((p) => <option key={p.id} value={p.id}>{p.sku} · {name(p)}</option>)}</Select>
              <Field label={t.productQty} value={deductionForm.quantity} onChange={(v) => setDeductionForm({ ...deductionForm, quantity: v })} type="number" />
              <button type="submit" className="inline-flex min-h-10 items-center justify-center gap-2 rounded-lg border border-[#0e5a4f] px-3 text-sm font-semibold text-[#0e5a4f] sm:col-span-2">{t.preview}</button>
            </form>
            {deductionPreview && (
              <div className="mt-4">
                <h4 className="text-sm font-semibold">{t.deductionTitle}</h4>
                <ul className="mt-2 space-y-1 text-sm">{deductionPreview.flatMap((d) => d.lines.map((l) => <li key={`${d.productId}-${l.inventoryItemId}`} className="flex items-center justify-between"><span>{l.itemNameAr ?? l.itemNameEn}</span><span className="text-[#69766f]">{fmt(l.sourceQuantity)} {l.sourceUnitCode ?? ""}</span></li>))}</ul>
                <button onClick={() => void postDeduction()} className="mt-3 inline-flex min-h-10 items-center gap-2 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white">{t.post}</button>
              </div>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}

type RecipeLineForm = { inventoryItemId: string; unitId: string; quantity: string };

function Field({ label, value, onChange, max, type = "text", required = false }: { label: string; value: string; onChange: (value: string) => void; max?: number; type?: string; required?: boolean }) {
  return <label className="block text-sm font-medium">{label}{required && " *"}<input required={required} type={type} step={type === "number" ? "any" : undefined} value={value} onChange={(e) => onChange(e.target.value)} maxLength={max} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20" /></label>;
}

function Select({ label, value, onChange, children, disabled = false }: { label: string; value: string; onChange: (value: string) => void; children: React.ReactNode; disabled?: boolean }) {
  return <label className="block text-sm font-medium">{label}<select disabled={disabled} value={value} onChange={(e) => onChange(e.target.value)} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20">{children}</select></label>;
}
