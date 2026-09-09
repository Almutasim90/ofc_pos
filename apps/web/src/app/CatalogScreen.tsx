import { useEffect, useState } from "react";
import { Plus, Search, Pencil, Package } from "lucide-react";
import { FormDialog } from "@/app/FormDialog";
import { store } from "@/lib/local-store";

type Named = { id: string; nameAr: string; nameEn: string };
type Category = Named & { parentId: string | null; sortOrder: number; imageUrl: string | null; isActive: boolean };
type Product = Named & { sku: string; barcode: string | null; categoryId: string; type: string; basePrice: number | null; descriptionAr: string | null; descriptionEn: string | null; preparationStationId: string | null; taxCategoryId: string | null; isActive: boolean; images: { url: string; sortOrder: number }[]; availability: { branchId: string; isAvailable: boolean }[] };
const blank = { nameAr: "", nameEn: "", sku: "", barcode: "", categoryId: "", type: "Simple", basePrice: "", descriptionAr: "", descriptionEn: "", preparationStationId: "", taxCategoryId: "", isActive: true, parentId: "", sortOrder: "0", imageUrl: "" };
const input = "mt-1 min-h-11 min-w-0 w-full rounded-lg border border-[#cdd7d0] bg-white px-3";
const button = "inline-flex min-h-11 items-center justify-center gap-2 rounded-lg border border-[#cdd7d0] px-4 text-sm font-semibold";

export function ProductPhoto({ src, name, className = "h-16 w-16" }: { src?: string | null; name: string; className?: string }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [src]);
  return <div className={`${className} shrink-0 overflow-hidden rounded-xl bg-[#edf5f1] text-[#0e5a4f]`}>
    {src && !failed ? <img src={src} alt={name} loading="lazy" onError={() => setFailed(true)} className="h-full w-full object-contain" /> : <span className="flex h-full items-center justify-center" title={name}><Package size={28} /></span>}
  </div>;
}

export function CatalogScreen({ language, mode }: { language: "ar" | "en"; mode: "products" | "categories" }) {
  const ar = language === "ar"; const isProduct = mode === "products";
  const tr = (a: string, e: string) => ar ? a : e;
  const name = (x: Named) => ar ? x.nameAr : x.nameEn;
  const [products, setProducts] = useState<Product[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [branches, setBranches] = useState<Named[]>([]);
  const [stations, setStations] = useState<Named[]>([]);
  const [groups, setGroups] = useState<Named[]>([]);
  const [groupIds, setGroupIds] = useState<string[]>([]);
  const [groupsFor, setGroupsFor] = useState<Product | null>(null);
  const [editing, setEditing] = useState<string | null>(null);
  const [form, setForm] = useState(blank);
  const [images, setImages] = useState<string[]>([""]);
  const [available, setAvailable] = useState<Record<string, boolean>>({});
  const [search, setSearch] = useState("");
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [stationDialogOpen, setStationDialogOpen] = useState(false);
  const [stationForm, setStationForm] = useState({ code: "", nameAr: "", nameEn: "" });
  const auth = (path: string, init?: RequestInit) => fetch(`/api/v1${path}`, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}` } });
  const failure = tr("تعذر الحفظ. تحقق من الحقول المطلوبة وعدم تكرار الرمز أو الباركود، ثم حاول مجددًا.", "Unable to save. Check required fields and duplicate codes or barcodes, then retry.");
  async function load() {
    setLoading(true);
    try {
      const paths = isProduct ? ["/categories", "/products", "/branches", "/preparation-stations", "/selection-groups"] : ["/categories"];
      const responses = await Promise.all(paths.map(p => auth(p)));
      if (!responses[0].ok || (isProduct && (!responses[1].ok || !responses[2].ok))) throw new Error();
      setCategories(await responses[0].json());
      if (isProduct) { setProducts(await responses[1].json()); setBranches(await responses[2].json()); if (responses[3].ok) setStations(await responses[3].json()); if (responses[4].ok) setGroups(await responses[4].json()); }
    } catch { setMessage(tr("تعذر تحميل القائمة. أعد المحاولة.", "Unable to load the list. Please retry.")); }
    finally { setLoading(false); }
  }
  useEffect(() => { setEditing(null); setSearch(""); setMessage(""); void load(); }, [mode]);
  function open(item?: Product | Category) {
    setMessage(""); setGroupsFor(null); setEditing(item?.id ?? "new");
    if (!item) { setForm({ ...blank, categoryId: categories.find(c => c.isActive)?.id ?? "" }); setImages([""]); setAvailable(Object.fromEntries(branches.map(b => [b.id, true]))); return; }
    if ("sku" in item) {
      setForm({ ...blank, ...item, barcode: item.barcode ?? "", basePrice: item.basePrice?.toString() ?? "", descriptionAr: item.descriptionAr ?? "", descriptionEn: item.descriptionEn ?? "", preparationStationId: item.preparationStationId ?? "", taxCategoryId: item.taxCategoryId ?? "" });
      setImages(item.images.length ? item.images.map(i => i.url) : [""]); setAvailable(Object.fromEntries(item.availability.map(b => [b.branchId, b.isAvailable])));
    } else setForm({ ...blank, ...item, parentId: item.parentId ?? "", imageUrl: item.imageUrl ?? "", sortOrder: item.sortOrder.toString() });
  }
  async function save(event: React.FormEvent) {
    event.preventDefault(); if (saving) return; setSaving(true); setMessage("");
    const payload = isProduct ? { ...form, sku: form.sku.trim() || `OFC-${crypto.randomUUID().slice(0, 12)}`, barcode: form.barcode.trim() || null, categoryId: form.categoryId, basePrice: form.basePrice === "" ? null : Number(form.basePrice), preparationStationId: form.preparationStationId || null, taxCategoryId: form.taxCategoryId || null, images: images.filter(x => x.trim()).map((url, sortOrder) => ({ url: url.trim(), sortOrder })), availability: branches.map(b => ({ branchId: b.id, isAvailable: available[b.id] ?? false })) } : { nameAr: form.nameAr, nameEn: form.nameEn, parentId: form.parentId || null, sortOrder: Number(form.sortOrder), imageUrl: form.imageUrl.trim() || null, isActive: form.isActive };
    try {
      const response = await auth(`/${mode}${editing === "new" ? "" : `/${editing}`}`, { method: editing === "new" ? "POST" : "PUT", body: JSON.stringify(payload) });
      if (!response.ok) throw new Error();
      setEditing(null); await load(); setMessage(tr("تم حفظ التعديلات.", "Changes saved."));
    } catch { setMessage(failure); } finally { setSaving(false); }
  }
  async function openGroups(product: Product) {
    setMessage("");
    try { const r = await auth(`/products/${product.id}/selection-groups`); if (!r.ok) throw new Error(); const value = await r.json() as Array<{ group: { id: string } }>; setGroupIds(value.map(g => g.group.id)); setGroupsFor(product); }
    catch { setMessage(failure); }
  }
  async function saveGroups() {
    if (!groupsFor || saving) return; setSaving(true);
    try { const r = await auth(`/products/${groupsFor.id}/selection-groups`, { method: "PUT", body: JSON.stringify(groupIds.map((selectionGroupId, sortOrder) => ({ selectionGroupId, sortOrder }))) }); if (!r.ok) throw new Error(); setGroupsFor(null); setMessage(tr("تم حفظ الإضافات.", "Modifiers saved.")); } catch { setMessage(failure); } finally { setSaving(false); }
  }
  const field = (key: keyof typeof blank, label: string, required = false, type = "text", hint?: string) => <label className="block text-sm font-medium">{label}{required && " *"}<input className={input} required={required} type={type} min={type === "number" ? 0 : undefined} step={type === "number" ? "any" : undefined} maxLength={key.startsWith("description") ? 2000 : key === "imageUrl" ? 500 : 160} value={String(form[key])} onChange={e => setForm({ ...form, [key]: e.target.value })} />{hint && <span className="mt-1 block text-xs font-normal text-[#64716b]">{hint}</span>}</label>;
  const rows = (isProduct ? products : categories).filter(x => `${x.nameAr} ${x.nameEn} ${"sku" in x ? x.sku : ""}`.toLowerCase().includes(search.toLowerCase()));
  return <div className="space-y-5">
    <div className="flex flex-wrap items-center justify-between gap-3"><div><h1 className="text-2xl font-bold">{isProduct ? tr("قائمة المنتجات", "Products") : tr("تصنيفات القائمة", "Menu categories")}</h1><p className="mt-2 text-sm text-[#64716b]">{tr("ابحث عن السجل واضغط تعديل. الحقول ذات النجمة مطلوبة.", "Find a record and choose Edit. Fields marked * are required.")}</p></div><button className={`${button} bg-[#0e5a4f] text-white`} onClick={() => open()}><Plus size={18} />{tr("إضافة جديد", "Add new")}</button></div>
    {message && <p role="status" className="rounded-xl border bg-white p-4 text-sm">{message}</p>}
    <label className="flex items-center gap-2 rounded-xl border bg-white px-3"><Search size={18} /><input aria-label={tr("البحث في القائمة", "Search list")} placeholder={tr("ابحث بالاسم أو الرمز", "Search by name or code")} value={search} onChange={e => setSearch(e.target.value)} className="min-h-12 min-w-0 flex-1 outline-none" /></label>
    {loading ? <p role="status">{tr("جارٍ التحميل…", "Loading…")}</p> : rows.length === 0 ? <p className="rounded-xl border bg-white p-8 text-center">{tr("لا توجد نتائج. أضف سجلًا أو غيّر البحث.", "No results. Add a record or change your search.")}</p> : <ul className="grid gap-3 xl:grid-cols-2">{rows.map(item => <li key={item.id} className="flex min-w-0 flex-wrap items-center gap-3 rounded-xl border bg-white p-4"><ProductPhoto src={"images" in item ? item.images[0]?.url : item.imageUrl} name={name(item)} /><div className="min-w-0 flex-1"><h2 className="font-semibold break-words">{name(item)}</h2><p className="mt-1 text-sm text-[#64716b]">{"sku" in item ? `${item.sku} · ${item.basePrice?.toFixed(3) ?? "—"} OMR` : tr("تصنيف القائمة", "Menu category")}</p><p className="text-xs">{item.isActive ? tr("نشط", "Active") : tr("متوقف", "Inactive")}</p></div><div className="flex flex-wrap gap-2"><button className={button} onClick={() => open(item)}><Pencil size={16} />{tr("تعديل", "Edit")}</button>{"sku" in item && <button className={button} onClick={() => void openGroups(item)}>{tr("الإضافات", "Modifiers")}</button>}</div></li>)}</ul>}
    <button className={button} onClick={() => void load()}>{tr("تحديث القائمة", "Refresh list")}</button>
    {isProduct && <button className={`${button} bg-[#0e5a4f] text-white`} onClick={() => setStationDialogOpen(true)}><Plus size={18} />{tr("إضافة مكان تحضير", "Add preparation area")}</button>}
    {editing && <FormDialog title={editing === "new" ? tr("إضافة جديد", "Add new") : tr("تعديل البيانات", "Edit details")} closeLabel={tr("إلغاء", "Cancel")} onClose={() => setEditing(null)}>
      <form onSubmit={save} className="space-y-5">
        <div className="grid gap-4 sm:grid-cols-2">{field("nameAr", tr("الاسم بالعربية", "Arabic name"), true)}{field("nameEn", tr("الاسم بالإنجليزية", "English name"), true)}
          {isProduct ? <><label className="text-sm font-medium">{tr("التصنيف *", "Category *")}<select required className={input} value={form.categoryId} onChange={e => setForm({ ...form, categoryId: e.target.value })}><option value="">{tr("اختر التصنيف", "Choose category")}</option>{categories.map(c => <option key={c.id} value={c.id}>{name(c)}</option>)}</select></label>{field("basePrice", tr("سعر البيع (ر.ع)", "Selling price (OMR)"), editing === "new", "number", tr("السعر قبل تطبيق قواعد التسعير الخاصة بالقناة.", "Before channel-specific pricing rules."))}</> : <label className="text-sm">{tr("التصنيف الرئيسي", "Parent category")}<select className={input} value={form.parentId} onChange={e => setForm({ ...form, parentId: e.target.value })}><option value="">{tr("تصنيف مستقل", "Top-level category")}</option>{categories.filter(c => c.id !== editing && !c.parentId).map(c => <option key={c.id} value={c.id}>{name(c)}</option>)}</select></label>}
        </div>
        {isProduct && <><fieldset><legend className="font-medium">{tr("صور المنتج (اختياري)", "Product photos (optional)")}</legend><p className="mt-1 text-xs text-[#64716b]">{tr("ضع رابط الصورة أو اختر صورة من قائمة المطعم. الصورة الأولى تظهر للكاشير والعميل.", "Paste an image link or choose a menu photo. The first image appears to cashiers and customers.")}</p>{images.map((url, i) => <div key={i} className="mt-3 flex items-center gap-3"><ProductPhoto src={url} name={form.nameAr || form.nameEn} /><input aria-label={tr("رابط الصورة", "Image URL")} className={input} value={url} maxLength={500} onChange={e => setImages(images.map((x, j) => j === i ? e.target.value : x))} list="menu-photos" /><button type="button" className={button} onClick={() => setImages(images.length > 1 ? images.filter((_, j) => j !== i) : [""])} aria-label={tr("إزالة الصورة", "Remove image")}>×</button></div>)}<datalist id="menu-photos">{Object.keys(import.meta.glob("/public/menu/**/*.jpg")).map(p => <option key={p} value={p.replace("/public", "")} />)}</datalist><button type="button" className={`${button} mt-3`} onClick={() => setImages([...images, ""])}>{tr("إضافة صورة أخرى", "Add another photo")}</button></fieldset>
        <label className="block text-sm font-medium">{tr("مكان تحضير المنتج", "Where is this product prepared?")}<select className={input} value={form.preparationStationId} onChange={e => setForm({ ...form, preparationStationId: e.target.value })}><option value="">{tr("المطبخ العام", "General kitchen")}</option>{stations.map(s => <option key={s.id} value={s.id}>{name(s)}</option>)}</select><span className="mt-1 block text-xs font-normal text-[#64716b]">{tr("مثال: البرجر إلى الشواية، والعصير إلى المشروبات. إن لم تحدد مكانًا يظهر المنتج في قائمة المطبخ العامة.", "For example: burgers go to the grill, juice to drinks. Leave blank for the general kitchen queue.")}</span></label></>}
        <details className="rounded-xl border p-4"><summary className="cursor-pointer font-semibold">{tr("تفاصيل إضافية (اختيارية)", "Additional details (optional)")}</summary><div className="mt-4 grid gap-4 sm:grid-cols-2">{isProduct ? <>{field("sku", tr("رمز الصنف", "SKU"), false, "text", tr("يُنشأ تلقائيًا عند تركه فارغًا.", "Generated automatically when blank."))}{field("barcode", tr("الباركود", "Barcode"), false, "text", tr("يُنشأ تلقائيًا. أدخل باركود العبوة إن كان موجودًا.", "Generated automatically. Enter the package barcode if one exists."))}<label className="text-sm">{tr("نوع المنتج", "Product type")}<select className={input} value={form.type} onChange={e => setForm({ ...form, type: e.target.value })}><option value="Simple">{tr("منتج عادي", "Standard product")}</option><option value="Combo">{tr("وجبة مركبة", "Combo meal")}</option><option value="Service">{tr("خدمة", "Service")}</option></select></label>{field("descriptionAr", tr("وصف بالعربية", "Arabic description"))}{field("descriptionEn", tr("وصف بالإنجليزية", "English description"))}</> : <>{field("sortOrder", tr("ترتيب العرض", "Display order"), false, "number")}{field("imageUrl", tr("رابط الصورة", "Image URL"))}</>}</div></details>
        {isProduct && <fieldset><legend className="font-medium">{tr("متاح للبيع في", "Available for sale at")}</legend><div className="mt-2 flex flex-wrap gap-4">{branches.map(b => <label key={b.id} className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={available[b.id] ?? false} onChange={e => setAvailable({ ...available, [b.id]: e.target.checked })} />{name(b)}</label>)}</div></fieldset>}
        <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} />{tr("نشط — ألغِ التحديد لإيقافه دون حذف السجلات السابقة", "Active — uncheck to deactivate while keeping past records")}</label>
        <button disabled={saving} className={`${button} w-full bg-[#0e5a4f] text-white sm:w-auto`}>{saving ? tr("جارٍ الحفظ…", "Saving…") : tr("حفظ", "Save")}</button>
      </form>
    </FormDialog>}
    {stationDialogOpen && <FormDialog title={tr("إعداد أماكن التحضير", "Set up preparation areas")} closeLabel={tr("إغلاق", "Close")} onClose={() => setStationDialogOpen(false)}><form className="grid gap-3 sm:grid-cols-3" onSubmit={async e => { e.preventDefault(); if (saving) return; setSaving(true); try { const r = await auth("/preparation-stations", { method: "POST", body: JSON.stringify(stationForm) }); if (!r.ok) throw new Error(); setStationForm({ code: "", nameAr: "", nameEn: "" }); await load(); setStationDialogOpen(false); } catch { setMessage(failure); } finally { setSaving(false); } }}>{(["code", "nameAr", "nameEn"] as const).map(k => <label key={k} className="text-sm">{k === "code" ? tr("رمز المكان *", "Area code *") : k === "nameAr" ? tr("الاسم بالعربية *", "Arabic name *") : tr("الاسم بالإنجليزية *", "English name *")}<input required className={input} maxLength={k === "code" ? 50 : 160} value={stationForm[k]} onChange={e => setStationForm({ ...stationForm, [k]: e.target.value })} /></label>)}<button disabled={saving} className={button}>{tr("إضافة مكان تحضير", "Add preparation area")}</button></form></FormDialog>}
    {groupsFor && <FormDialog title={`${name(groupsFor)} — ${tr("الإضافات", "Modifiers")}`} closeLabel={tr("إغلاق", "Close")} onClose={() => setGroupsFor(null)}>{groups.map(g => <label key={g.id} className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={groupIds.includes(g.id)} onChange={e => setGroupIds(e.target.checked ? [...groupIds, g.id] : groupIds.filter(id => id !== g.id))} />{name(g)}</label>)}<button className={`${button} mt-4`} disabled={saving} onClick={() => void saveGroups()}>{tr("حفظ الإضافات", "Save modifiers")}</button></FormDialog>}
  </div>;
}
