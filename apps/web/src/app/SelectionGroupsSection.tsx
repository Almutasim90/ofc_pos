import { useEffect, useState } from "react";
import { Plus, RefreshCw } from "lucide-react";

import { store } from "@/lib/local-store";

type Language = "ar" | "en";
type Product = { id: string; nameAr: string; nameEn: string; isActive: boolean };
type Branch = { id: string; nameAr: string; nameEn: string; isActive: boolean };
type Group = { id: string; kind: "Combo" | "Modifier"; nameAr: string; nameEn: string; isRequired: boolean; minSelections: number; maxSelections: number; isActive: boolean; options: Array<{ id: string; productId: string; product: { nameAr: string; nameEn: string } | null; priceAdjustment: number; isDefault: boolean; maxQuantity: number }>; availability: Array<{ branchId: string; isAvailable: boolean }> };

const copy = {
  ar: { title: "تكوين الوجبات والإضافات", description: "أنشئ مجموعات قابلة لإعادة الاستخدام، وحدد الخيارات والأسعار والتوفر حسب الفرع.", combo: "وجبة مركبة", modifier: "إضافات", nameAr: "الاسم بالعربية", nameEn: "الاسم بالإنجليزية", required: "اختيار إلزامي", min: "الحد الأدنى", max: "الحد الأقصى", options: "الخيارات", addOption: "إضافة خيار", product: "المنتج", adjustment: "تعديل السعر (OMR)", default: "افتراضي", quantity: "حد الكمية", branches: "الفروع المتاحة", save: "حفظ المجموعة", saved: "تم حفظ المجموعة.", loading: "جارٍ تحميل التكوين", retry: "إعادة المحاولة", error: "تعذر حفظ أو تحميل التكوين. تحقق من الصلاحيات والاتصال.", empty: "لا توجد مجموعات مهيأة بعد.", select: "اختر منتجًا" },
  en: { title: "Combo & modifier setup", description: "Create reusable groups and define their choices, prices, and branch availability.", combo: "Combo", modifier: "Modifier", nameAr: "Arabic name", nameEn: "English name", required: "Required selection", min: "Minimum", max: "Maximum", options: "Options", addOption: "Add option", product: "Product", adjustment: "Price adjustment (OMR)", default: "Default", quantity: "Quantity limit", branches: "Available branches", save: "Save group", saved: "Group saved.", loading: "Loading configuration", retry: "Retry", error: "Unable to save or load configuration. Check permissions and connection.", empty: "No groups configured yet.", select: "Select a product" },
} as const;

const input = "mt-1 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20";

export function SelectionGroupsSection({ language }: { language: Language }) {
  const text = copy[language];
  const [groups, setGroups] = useState<Group[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [branches, setBranches] = useState<Branch[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({ kind: "Combo" as "Combo" | "Modifier", nameAr: "", nameEn: "", required: true, min: "1", max: "1" });
  const [options, setOptions] = useState([{ productId: "", priceAdjustment: "0", isDefault: true, maxQuantity: "1" }]);
  const [availability, setAvailability] = useState<Record<string, boolean>>({});

  async function load() {
    setLoading(true); setError("");
    try {
      const token = store.get<string>("session-token") ?? "";
      const headers = { Authorization: `Bearer ${token}` };
      const [groupsResponse, productsResponse, branchesResponse] = await Promise.all([fetch("/api/v1/selection-groups", { headers }), fetch("/api/v1/products", { headers }), fetch("/api/v1/branches", { headers })]);
      if (!groupsResponse.ok || !productsResponse.ok || !branchesResponse.ok) throw new Error();
      setGroups(await groupsResponse.json() as Group[]); setProducts((await productsResponse.json() as Product[]).filter((product) => product.isActive)); setBranches((await branchesResponse.json() as Branch[]).filter((branch) => branch.isActive));
    } catch { setError(text.error); } finally { setLoading(false); }
  }

  useEffect(() => { void load(); }, [language]);

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(""); setNotice("");
    if (options.some((option) => !option.productId)) { setError(text.error); return; }
    setSaving(true);
    try {
      const response = await fetch("/api/v1/selection-groups", {
        method: "POST", headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}` },
        body: JSON.stringify({ kind: form.kind, nameAr: form.nameAr, nameEn: form.nameEn, isRequired: form.required, minSelections: Number(form.min), maxSelections: Number(form.max), isActive: true, options: options.map((option, index) => ({ productId: option.productId, priceAdjustment: Number(option.priceAdjustment), isDefault: option.isDefault, maxQuantity: Number(option.maxQuantity), sortOrder: index })), availability: branches.map((branch) => ({ branchId: branch.id, isAvailable: availability[branch.id] ?? true })) }),
      });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.selectionRange?.[0] ?? problem?.errors?.options?.[0] ?? text.error); }
      setNotice(text.saved); setForm({ kind: "Combo", nameAr: "", nameEn: "", required: true, min: "1", max: "1" }); setOptions([{ productId: "", priceAdjustment: "0", isDefault: true, maxQuantity: "1" }]); setAvailability({}); await load();
    } catch (cause) { setError(cause instanceof Error ? cause.message : text.error); } finally { setSaving(false); }
  }

  const name = (item: { nameAr: string; nameEn: string }) => language === "ar" ? item.nameAr : item.nameEn;
  return <><p className="text-sm font-semibold text-[#0e5a4f]">{text.description}</p><h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{text.title}</h1><form onSubmit={submit} className="mt-6 rounded-xl border border-[#dfe5df] bg-white p-5"><div className="grid gap-4 sm:grid-cols-2"><label className="text-sm font-medium">{language === "ar" ? "النوع" : "Kind"}<select className={input} value={form.kind} onChange={(event) => setForm({ ...form, kind: event.target.value as "Combo" | "Modifier" })}><option value="Combo">{text.combo}</option><option value="Modifier">{text.modifier}</option></select></label><label className="flex min-h-11 items-end gap-2 text-sm font-medium"><input type="checkbox" checked={form.required} onChange={(event) => setForm({ ...form, required: event.target.checked, min: event.target.checked && form.min === "0" ? "1" : form.min })} />{text.required}</label></div><div className="mt-4 grid gap-4 sm:grid-cols-2"><Label text={text.nameAr}><input required maxLength={160} className={input} value={form.nameAr} onChange={(event) => setForm({ ...form, nameAr: event.target.value })} /></Label><Label text={text.nameEn}><input required maxLength={160} className={input} value={form.nameEn} onChange={(event) => setForm({ ...form, nameEn: event.target.value })} /></Label></div><div className="mt-4 grid grid-cols-2 gap-4 sm:max-w-md"><Label text={text.min}><input required min={form.required ? 1 : 0} max={99} type="number" className={input} value={form.min} onChange={(event) => setForm({ ...form, min: event.target.value })} /></Label><Label text={text.max}><input required min={0} max={99} type="number" className={input} value={form.max} onChange={(event) => setForm({ ...form, max: event.target.value })} /></Label></div><h2 className="mt-7 font-semibold">{text.options}</h2><div className="mt-3 space-y-3">{options.map((option, index) => <div key={index} className="grid gap-3 rounded-lg bg-[#f5f8f6] p-3 sm:grid-cols-[minmax(0,1fr)_130px_110px_auto]"><label className="text-sm font-medium">{text.product}<select required className={input} value={option.productId} onChange={(event) => setOptions(options.map((item, itemIndex) => itemIndex === index ? { ...item, productId: event.target.value } : item))}><option value="">{text.select}</option>{products.map((product) => <option key={product.id} value={product.id}>{name(product)}</option>)}</select></label><Label text={text.adjustment}><input type="number" step="0.001" className={input} value={option.priceAdjustment} onChange={(event) => setOptions(options.map((item, itemIndex) => itemIndex === index ? { ...item, priceAdjustment: event.target.value } : item))} /></Label><Label text={text.quantity}><input type="number" min={1} max={99} className={input} value={option.maxQuantity} onChange={(event) => setOptions(options.map((item, itemIndex) => itemIndex === index ? { ...item, maxQuantity: event.target.value } : item))} /></Label><label className="flex min-h-11 items-end gap-2 text-sm font-medium"><input type="checkbox" checked={option.isDefault} onChange={(event) => setOptions(options.map((item, itemIndex) => itemIndex === index ? { ...item, isDefault: event.target.checked } : item))} />{text.default}</label></div>)}</div><button type="button" onClick={() => setOptions([...options, { productId: "", priceAdjustment: "0", isDefault: false, maxQuantity: "1" }])} className="mt-3 min-h-11 rounded-lg px-3 text-sm font-semibold text-[#0e5a4f] hover:bg-[#edf5f1]"><Plus className="inline" size={17} /> {text.addOption}</button><h2 className="mt-7 font-semibold">{text.branches}</h2><div className="mt-3 grid gap-2 sm:grid-cols-2">{branches.map((branch) => <label key={branch.id} className="flex min-h-11 items-center gap-2 rounded-lg border border-[#dfe5df] px-3 text-sm"><input type="checkbox" checked={availability[branch.id] ?? true} onChange={(event) => setAvailability({ ...availability, [branch.id]: event.target.checked })} />{name(branch)}</label>)}</div>{error && <p role="alert" className="mt-4 text-sm text-[#b4322a]">{error}</p>}{notice && <p role="status" className="mt-4 text-sm text-[#137347]">{notice}</p>}<button disabled={saving} className="mt-6 min-h-11 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60">{saving ? text.loading : text.save}</button></form>{loading ? <div className="mt-7 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{text.loading}</div> : error ? <button onClick={() => void load()} className="mt-6 min-h-11 rounded-lg border border-[#cdd7d0] px-4 font-semibold text-[#0e5a4f]">{text.retry}</button> : <section className="mt-7"><h2 className="font-semibold">{text.title}</h2>{groups.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{text.empty}</p> : <div className="mt-3 grid gap-3 md:grid-cols-2">{groups.map((group) => <article key={group.id} className="rounded-xl border border-[#dfe5df] bg-white p-4"><div className="flex items-center justify-between gap-3"><h3 className="font-semibold">{name(group)}</h3><span className="rounded-full bg-[#e6f1ec] px-3 py-1 text-xs font-semibold text-[#08483f]">{group.kind === "Combo" ? text.combo : text.modifier}</span></div><p className="mt-2 text-sm text-[#69766f]">{group.minSelections}–{group.maxSelections} {language === "ar" ? "اختيارات" : "selections"}</p><ul className="mt-3 space-y-1 text-sm text-[#53615b]">{group.options.map((option) => <li key={option.id}>{option.product ? name(option.product) : "-"} {option.priceAdjustment !== 0 ? `+${option.priceAdjustment.toFixed(3)} OMR` : ""}</li>)}</ul></article>)}</div>}</section>}</>;
}

function Label({ text, children }: { text: string; children: React.ReactNode }) { return <label className="block text-sm font-medium">{text}{children}</label>; }
