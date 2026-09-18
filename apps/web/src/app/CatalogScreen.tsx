import { useEffect, useState } from "react";
import { Plus, Search, Pencil, Package } from "lucide-react";
import { FormDialog } from "@/app/FormDialog";
import { Pagination, PAGE_SIZE } from "@/app/Pagination";
import { SearchableSelect } from "@/app/SearchableSelect";
import { store } from "@/lib/local-store";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Named = { id: string; nameAr: string; nameEn: string };
type Category = Named & {
  parentId: string | null;
  sortOrder: number;
  imageUrl: string | null;
  isActive: boolean;
};
type Product = Named & {
  sku: string;
  barcode: string | null;
  categoryId: string;
  type: string;
  basePrice: number | null;
  descriptionAr: string | null;
  descriptionEn: string | null;
  preparationStationId: string | null;
  taxCategoryId: string | null;
  isActive: boolean;
  images: { url: string; sortOrder: number }[];
  availability: { branchId: string; isAvailable: boolean }[];
};
const blank = {
  nameAr: "",
  nameEn: "",
  sku: "",
  barcode: "",
  categoryId: "",
  type: "Simple",
  basePrice: "",
  descriptionAr: "",
  descriptionEn: "",
  preparationStationId: "",
  taxCategoryId: "",
  isActive: true,
  parentId: "",
  sortOrder: "0",
  imageUrl: "",
};
const input =
  "mt-1 min-h-11 min-w-0 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0e5a4f] focus-visible:ring-offset-1";
const button =
  "inline-flex min-h-11 items-center justify-center gap-2 rounded-lg border border-[#cdd7d0] px-4 text-sm font-semibold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0e5a4f] focus-visible:ring-offset-1 disabled:cursor-not-allowed disabled:opacity-50";

export function ProductPhoto({
  src,
  name,
  className = "h-16 w-16",
}: {
  src?: string | null;
  name: string;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [src]);
  return (
    <div
      className={`${className} shrink-0 overflow-hidden rounded-xl bg-[#edf5f1] text-[#0e5a4f]`}
    >
      {src && !failed ? (
        <img
          src={src}
          alt={name}
          loading="lazy"
          onError={() => setFailed(true)}
          className="h-full w-full object-contain"
        />
      ) : (
        <span className="flex h-full items-center justify-center" title={name}>
          <Package size={28} />
        </span>
      )}
    </div>
  );
}

export function CatalogScreen({
  language,
  mode,
}: {
  language: "ar" | "en";
  mode: "products" | "categories";
}) {
  const ar = language === "ar";
  const isProduct = mode === "products";
  const tr = (a: string, e: string) => (ar ? a : e);
  const name = (x: Named) => (ar ? x.nameAr : x.nameEn);
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
  const [availabilityBranchId, setAvailabilityBranchId] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [message, setMessage] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [stationDialogOpen, setStationDialogOpen] = useState(false);
  const [stationForm, setStationForm] = useState({
    code: "",
    nameAr: "",
    nameEn: "",
  });
  const auth = (path: string, init?: RequestInit) =>
    fetch(`/api/v1${path}`, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`,
      },
    });
  const failure = tr(
    "تعذر الحفظ. تحقق من الحقول المطلوبة وعدم تكرار الرمز أو الباركود، ثم حاول مجددًا.",
    "Unable to save. Check required fields and duplicate codes or barcodes, then retry.",
  );
  async function load() {
    setLoading(true);
    try {
      const paths = isProduct
        ? [
            "/categories",
            "/products",
            "/branches",
            "/preparation-stations",
            "/selection-groups",
          ]
        : ["/categories"];
      const responses = await Promise.all(paths.map((p) => auth(p)));
      if (
        !responses[0].ok ||
        (isProduct && (!responses[1].ok || !responses[2].ok))
      )
        throw new Error();
      setCategories(await responses[0].json());
      if (isProduct) {
        setProducts(await responses[1].json());
        const branchList = await responses[2].json() as Named[];
        setBranches(branchList);
        setAvailabilityBranchId((current) => current || branchList[0]?.id || "");
        if (responses[3].ok) setStations(await responses[3].json());
        if (responses[4].ok) setGroups(await responses[4].json());
      }
    } catch {
      setMessage(
        tr(
          "تعذر تحميل القائمة. أعد المحاولة.",
          "Unable to load the list. Please retry.",
        ),
      );
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    setEditing(null);
    setSearch("");
    setMessage("");
    setPage(1);
    void load();
  }, [mode]);
  useEffect(() => {
    setPage(1);
  }, [search]);
  function open(item?: Product | Category) {
    setMessage("");
    setGroupsFor(null);
    setEditing(item?.id ?? "new");
    if (!item) {
      setForm({
        ...blank,
        categoryId: categories.find((c) => c.isActive)?.id ?? "",
      });
      setImages([""]);
      setAvailable(Object.fromEntries(branches.map((b) => [b.id, true])));
      return;
    }
    if ("sku" in item) {
      setForm({
        ...blank,
        ...item,
        barcode: item.barcode ?? "",
        basePrice: item.basePrice?.toString() ?? "",
        descriptionAr: item.descriptionAr ?? "",
        descriptionEn: item.descriptionEn ?? "",
        preparationStationId: item.preparationStationId ?? "",
        taxCategoryId: item.taxCategoryId ?? "",
      });
      setImages(item.images.length ? item.images.map((i) => i.url) : [""]);
      setAvailable(
        Object.fromEntries(
          item.availability.map((b) => [b.branchId, b.isAvailable]),
        ),
      );
    } else
      setForm({
        ...blank,
        ...item,
        parentId: item.parentId ?? "",
        imageUrl: item.imageUrl ?? "",
        sortOrder: item.sortOrder.toString(),
      });
  }
  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (saving) return;
    setSaving(true);
    setMessage("");
    const payload = isProduct
      ? {
          ...form,
          sku: form.sku.trim() || `OFC-${crypto.randomUUID().slice(0, 12)}`,
          barcode: form.barcode.trim() || null,
          categoryId: form.categoryId,
          basePrice: form.basePrice === "" ? null : Number(form.basePrice),
          preparationStationId: form.preparationStationId || null,
          taxCategoryId: form.taxCategoryId || null,
          images: images
            .filter((x) => x.trim())
            .map((url, sortOrder) => ({ url: url.trim(), sortOrder })),
          availability: branches.map((b) => ({
            branchId: b.id,
            isAvailable: available[b.id] ?? false,
          })),
        }
      : {
          nameAr: form.nameAr,
          nameEn: form.nameEn,
          parentId: form.parentId || null,
          sortOrder: Number(form.sortOrder),
          imageUrl: form.imageUrl.trim() || null,
          isActive: form.isActive,
        };
    try {
      const response = await auth(
        `/${mode}${editing === "new" ? "" : `/${editing}`}`,
        {
          method: editing === "new" ? "POST" : "PUT",
          body: JSON.stringify(payload),
        },
      );
      if (!response.ok) throw new Error();
      setEditing(null);
      await load();
      setMessage(tr("تم حفظ التعديلات.", "Changes saved."));
    } catch {
      setMessage(failure);
    } finally {
      setSaving(false);
    }
  }
  async function openGroups(product: Product) {
    setMessage("");
    try {
      const r = await auth(`/products/${product.id}/selection-groups`);
      if (!r.ok) throw new Error();
      const value = (await r.json()) as Array<{ group: { id: string } }>;
      setGroupIds(value.map((g) => g.group.id));
      setGroupsFor(product);
    } catch {
      setMessage(failure);
    }
  }
  async function saveGroups() {
    if (!groupsFor || saving) return;
    setSaving(true);
    try {
      const r = await auth(`/products/${groupsFor.id}/selection-groups`, {
        method: "PUT",
        body: JSON.stringify(
          groupIds.map((selectionGroupId, sortOrder) => ({
            selectionGroupId,
            sortOrder,
          })),
        ),
      });
      if (!r.ok) throw new Error();
      setGroupsFor(null);
      setMessage(tr("تم حفظ الإضافات.", "Modifiers saved."));
    } catch {
      setMessage(failure);
    } finally {
      setSaving(false);
    }
  }
  async function toggleAvailability(product: Product) {
    if (!availabilityBranchId || saving) return;
    setSaving(true); setMessage("");
    const current = product.availability.find((item) => item.branchId === availabilityBranchId)?.isAvailable ?? false;
    const availability = branches.map((branch) => ({ branchId: branch.id, isAvailable: branch.id === availabilityBranchId ? !current : product.availability.find((item) => item.branchId === branch.id)?.isAvailable ?? false }));
    try {
      const response = await auth(`/products/${product.id}/availability`, { method: "PUT", body: JSON.stringify(availability) });
      if (!response.ok) throw new Error();
      await load();
      setMessage(!current ? tr("تم توفير الصنف في الفرع.", "Item is now available at the branch.") : tr("تم إيقاف الصنف مؤقتًا في الفرع وسيظهر غير متوفر في QR.", "Item is temporarily unavailable and will be marked unavailable in QR."));
    } catch { setMessage(failure); }
    finally { setSaving(false); }
  }
  const field = (
    key: keyof typeof blank,
    label: string,
    required = false,
    type = "text",
    hint?: string,
  ) => (
    <label className="block text-sm font-medium">
      {label}
      {required && " *"}
      <Input
        className={input}
        required={required}
        type={type}
        min={type === "number" ? 0 : undefined}
        step={type === "number" ? "any" : undefined}
        maxLength={
          key.startsWith("description") ? 2000 : key === "imageUrl" ? 500 : 160
        }
        value={String(form[key])}
        onChange={(e) => setForm({ ...form, [key]: e.target.value })}
      />
      {hint && (
        <span className="mt-1 block text-xs font-normal text-[#000000]">
          {hint}
        </span>
      )}
    </label>
  );
  const rows = (isProduct ? products : categories).filter((x) =>
    `${x.nameAr} ${x.nameEn} ${"sku" in x ? x.sku : ""}`
      .toLowerCase()
      .includes(search.toLowerCase()),
  );
  const pageRows = rows.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">
            {isProduct
              ? tr("قائمة المنتجات", "Products")
              : tr("تصنيفات القائمة", "Menu categories")}
          </h1>
          <p className="mt-2 text-sm text-[#000000]">
            {tr(
              "ابحث عن السجل واضغط تعديل. الحقول ذات النجمة مطلوبة.",
              "Find a record and choose Edit. Fields marked * are required.",
            )}
          </p>
        </div>
        <Button
          className={`${button} bg-[#0e5a4f] text-white`}
          onClick={() => open()}
        >
          <Plus size={18} />
          {tr("إضافة جديد", "Add new")}
        </Button>
      </div>
      {message && (
        <p role="status" className="rounded-xl border bg-white p-4 text-sm">
          {message}
        </p>
      )}
      <label
        htmlFor="catalog-search"
        className="flex items-center gap-2 rounded-xl border border-[#cdd7d0] bg-white px-3 focus-within:ring-2 focus-within:ring-[#0e5a4f] focus-within:ring-offset-1"
      >
        <Search size={18} aria-hidden="true" />
        <Input
          id="catalog-search"
          aria-describedby="catalog-search-hint"
          placeholder={tr("ابحث بالاسم أو الرمز", "Search by name or code")}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="min-h-12 min-w-0 flex-1 bg-transparent outline-none"
        />
      </label>
    <p id="catalog-search-hint" className="sr-only">
        {tr(
          "اكتب للتصفية حسب الاسم أو الرمز",
          "Type to filter by name or code",
        )}
    </p>
    {isProduct && branches.length > 0 && <div className="flex flex-wrap items-end gap-3 rounded-xl border border-[#dfe5df] bg-[#f7f9f7] p-4"><div className="w-full max-w-xs"><SearchableSelect label={tr("الفرع للتحكم السريع بالتوفر", "Branch for quick availability")} value={availabilityBranchId} onChange={setAvailabilityBranchId}>{branches.map((branch) => <option key={branch.id} value={branch.id}>{name(branch)}</option>)}</SearchableSelect></div><p className="pb-2 text-xs text-[#66736d]">{tr("أوقف الصنف أو أعد توفيره مباشرة دون فتح شاشة التعديل.", "Mark an item sold out or available without opening its edit form.")}</p></div>}
      {loading ? (
        <ul
          aria-busy="true"
          role="status"
          aria-label={tr("جارٍ التحميل…", "Loading…")}
          className="grid gap-3 xl:grid-cols-2"
        >
          {Array.from({ length: 6 }).map((_, i) => (
            <li
              key={i}
              className="flex h-24 animate-pulse items-center gap-3 rounded-xl border bg-white p-4"
            >
              <div className="h-16 w-16 shrink-0 rounded-xl bg-[#edf5f1]" />
              <div className="min-w-0 flex-1 space-y-2">
                <div className="h-4 w-2/3 rounded bg-[#edf5f1]" />
                <div className="h-3 w-1/3 rounded bg-[#edf5f1]" />
              </div>
            </li>
          ))}
        </ul>
      ) : rows.length === 0 ? (
        <p className="rounded-xl border bg-white p-8 text-center">
          {tr(
            "لا توجد نتائج. أضف سجلًا أو غيّر البحث.",
            "No results. Add a record or change your search.",
          )}
        </p>
      ) : (
        <>
          <ul className="grid gap-3 xl:grid-cols-2">
            {pageRows.map((item) => (
              <li
                key={item.id}
                className="flex min-w-0 flex-wrap items-center gap-3 rounded-xl border bg-white p-4 transition-colors hover:border-[#0e5a4f]/40"
              >
                <ProductPhoto
                  src={"images" in item ? item.images[0]?.url : item.imageUrl}
                  name={name(item)}
                />
                <div className="min-w-0 flex-1">
                  <h2 className="font-semibold break-words">{name(item)}</h2>
                  <p className="mt-1 text-sm text-[#000000]">
                    {"sku" in item
                      ? `${item.sku} · ${item.basePrice?.toFixed(3) ?? "—"} OMR`
                      : tr("تصنيف القائمة", "Menu category")}
                  </p>
                  <p className="text-xs">
                    {item.isActive
                      ? tr("نشط", "Active")
                      : tr("متوقف", "Inactive")}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button className={button} onClick={() => open(item)}>
                    <Pencil size={16} />
                    {tr("تعديل", "Edit")}
                  </Button>
                  {"sku" in item && (
                    <Button
                      disabled={!availabilityBranchId || saving}
                      className={`${button} ${item.availability.find((entry) => entry.branchId === availabilityBranchId)?.isAvailable ? "border-[#b4322a] text-[#b4322a]" : "border-[#137347] text-[#137347]"}`}
                      onClick={() => void toggleAvailability(item)}
                    >
                      {item.availability.find((entry) => entry.branchId === availabilityBranchId)?.isAvailable ? tr("غير متوفر مؤقتًا", "Mark sold out") : tr("إعادة التوفير", "Make available")}
                    </Button>
                  )}
                  {"sku" in item && (
                    <Button
                      className={button}
                      onClick={() => void openGroups(item)}
                    >
                      {item.type === "Combo" ? tr("تكوين الوجبة", "Build meal") : tr("الإضافات", "Modifiers")}
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            total={rows.length}
            onPageChange={setPage}
            language={language}
          />
        </>
      )}
      <Button className={button} onClick={() => void load()}>
        {tr("تحديث القائمة", "Refresh list")}
      </Button>
      {isProduct && (
        <Button
          className={`${button} bg-[#0e5a4f] text-white`}
          onClick={() => setStationDialogOpen(true)}
        >
          <Plus size={18} />
          {tr("إضافة مكان تحضير", "Add preparation area")}
        </Button>
      )}
      {editing && (
        <FormDialog
          title={
            editing === "new"
              ? tr("إضافة جديد", "Add new")
              : tr("تعديل البيانات", "Edit details")
          }
          closeLabel={tr("إلغاء", "Cancel")}
          onClose={() => setEditing(null)}
        >
          <form onSubmit={save} className="space-y-5">
            <div className="grid gap-4 sm:grid-cols-2">
              {field("nameAr", tr("الاسم بالعربية", "Arabic name"), true)}
              {field("nameEn", tr("الاسم بالإنجليزية", "English name"), true)}
              {isProduct ? (
                <>
                  <SearchableSelect
                    label={tr("التصنيف *", "Category *")}
                    value={form.categoryId}
                    onChange={(v) => setForm({ ...form, categoryId: v })}
                  >
                    <option value="">
                      {tr("اختر التصنيف", "Choose category")}
                    </option>
                    {categories.map((c) => (
                      <option key={c.id} value={c.id}>
                        {name(c)}
                      </option>
                    ))}
                  </SearchableSelect>
                  {field(
                    "basePrice",
                    tr("سعر البيع (ر.ع)", "Selling price (OMR)"),
                    editing === "new",
                    "number",
                    tr(
                      "السعر قبل تطبيق قواعد التسعير الخاصة بالقناة.",
                      "Before channel-specific pricing rules.",
                    ),
                  )}
                </>
              ) : (
                <SearchableSelect
                  label={tr("التصنيف الرئيسي", "Parent category")}
                  value={form.parentId}
                  onChange={(v) => setForm({ ...form, parentId: v })}
                >
                  <option value="">
                    {tr("تصنيف مستقل", "Top-level category")}
                  </option>
                  {categories
                    .filter((c) => c.id !== editing && !c.parentId)
                    .map((c) => (
                      <option key={c.id} value={c.id}>
                        {name(c)}
                      </option>
                    ))}
                </SearchableSelect>
              )}
            </div>
            {isProduct && (
              <>
                <fieldset>
                  <legend className="font-medium">
                    {tr("صور المنتج (اختياري)", "Product photos (optional)")}
                  </legend>
                  <p className="mt-1 text-xs text-[#000000]">
                    {tr(
                      "ضع رابط الصورة أو اختر صورة من قائمة المطعم. الصورة الأولى تظهر للكاشير والعميل.",
                      "Paste an image link or choose a menu photo. The first image appears to cashiers and customers.",
                    )}
                  </p>
                  {images.map((url, i) => (
                    <div key={i} className="mt-3 flex items-center gap-3">
                      <ProductPhoto
                        src={url}
                        name={form.nameAr || form.nameEn}
                      />
                      <Input
                        aria-label={tr("رابط الصورة", "Image URL")}
                        className={input}
                        value={url}
                        maxLength={500}
                        onChange={(e) =>
                          setImages(
                            images.map((x, j) =>
                              j === i ? e.target.value : x,
                            ),
                          )
                        }
                        list="menu-photos"
                      />
                      <Button
                        type="button"
                        className={button}
                        onClick={() =>
                          setImages(
                            images.length > 1
                              ? images.filter((_, j) => j !== i)
                              : [""],
                          )
                        }
                        aria-label={tr("إزالة الصورة", "Remove image")}
                      >
                        ×
                      </Button>
                    </div>
                  ))}
                  <datalist id="menu-photos">
                    {Object.keys(import.meta.glob("/public/menu/**/*.jpg")).map(
                      (p) => (
                        <option key={p} value={p.replace("/public", "")} />
                      ),
                    )}
                  </datalist>
                  <Button
                    type="button"
                    className={`${button} mt-3`}
                    onClick={() => setImages([...images, ""])}
                  >
                    {tr("إضافة صورة أخرى", "Add another photo")}
                  </Button>
                </fieldset>
                <div>
                  <SearchableSelect
                    label={tr(
                      "مكان تحضير المنتج",
                      "Where is this product prepared?",
                    )}
                    value={form.preparationStationId}
                    onChange={(v) =>
                      setForm({ ...form, preparationStationId: v })
                    }
                  >
                    <option value="">
                      {tr("المطبخ العام", "General kitchen")}
                    </option>
                    {stations.map((s) => (
                      <option key={s.id} value={s.id}>
                        {name(s)}
                      </option>
                    ))}
                  </SearchableSelect>
                  <span className="mt-1 block text-xs font-normal text-[#000000]">
                    {tr(
                      "مثال: البرجر إلى الشواية، والعصير إلى المشروبات. إن لم تحدد مكانًا يظهر المنتج في قائمة المطبخ العامة.",
                      "For example: burgers go to the grill, juice to drinks. Leave blank for the general kitchen queue.",
                    )}
                  </span>
                </div>
              </>
            )}
            <details className="rounded-xl border p-4">
              <summary className="cursor-pointer font-semibold">
                {tr(
                  "تفاصيل إضافية (اختيارية)",
                  "Additional details (optional)",
                )}
              </summary>
              <div className="mt-4 grid gap-4 sm:grid-cols-2">
                {isProduct ? (
                  <>
                    {field(
                      "sku",
                      tr("رمز الصنف", "SKU"),
                      false,
                      "text",
                      tr(
                        "يُنشأ تلقائيًا عند تركه فارغًا.",
                        "Generated automatically when blank.",
                      ),
                    )}
                    {field(
                      "barcode",
                      tr("الباركود", "Barcode"),
                      false,
                      "text",
                      tr(
                        "يُنشأ تلقائيًا. أدخل باركود العبوة إن كان موجودًا.",
                        "Generated automatically. Enter the package barcode if one exists.",
                      ),
                    )}
                    <SearchableSelect
                      label={tr("نوع المنتج", "Product type")}
                      value={form.type}
                      onChange={(v) => setForm({ ...form, type: v })}
                    >
                      <option value="Simple">
                        {tr("منتج عادي", "Standard product")}
                      </option>
                      <option value="Combo">
                        {tr("وجبة مركبة", "Combo meal")}
                      </option>
                      <option value="Service">{tr("خدمة", "Service")}</option>
                    </SearchableSelect>
                    {field(
                      "descriptionAr",
                      tr("وصف بالعربية", "Arabic description"),
                    )}
                    {field(
                      "descriptionEn",
                      tr("وصف بالإنجليزية", "English description"),
                    )}
                  </>
                ) : (
                  <>
                    {field(
                      "sortOrder",
                      tr("ترتيب العرض", "Display order"),
                      false,
                      "number",
                    )}
                    {field("imageUrl", tr("رابط الصورة", "Image URL"))}
                  </>
                )}
              </div>
            </details>
            {isProduct && (
              <fieldset>
                <legend className="font-medium">
                  {tr("متاح للبيع في", "Available for sale at")}
                </legend>
                <div className="mt-2 flex flex-wrap gap-4">
                  {branches.map((b) => (
                    <label
                      key={b.id}
                      className="flex min-h-11 items-center gap-2"
                    >
                      <Input
                        type="checkbox"
                        checked={available[b.id] ?? false}
                        onChange={(e) =>
                          setAvailable({
                            ...available,
                            [b.id]: e.target.checked,
                          })
                        }
                      />
                      {name(b)}
                    </label>
                  ))}
                </div>
              </fieldset>
            )}
            <label className="flex min-h-11 items-center gap-2">
              <Input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) =>
                  setForm({ ...form, isActive: e.target.checked })
                }
              />
              {tr(
                "نشط — ألغِ التحديد لإيقافه دون حذف السجلات السابقة",
                "Active — uncheck to deactivate while keeping past records",
              )}
            </label>
            <Button
              disabled={saving}
              className={`${button} w-full bg-[#0e5a4f] text-white sm:w-auto`}
            >
              {saving ? tr("جارٍ الحفظ…", "Saving…") : tr("حفظ", "Save")}
            </Button>
          </form>
        </FormDialog>
      )}
      {stationDialogOpen && (
        <FormDialog
          title={tr("إعداد أماكن التحضير", "Set up preparation areas")}
          closeLabel={tr("إغلاق", "Close")}
          onClose={() => setStationDialogOpen(false)}
        >
          <form
            className="grid gap-3 sm:grid-cols-3"
            onSubmit={async (e) => {
              e.preventDefault();
              if (saving) return;
              setSaving(true);
              try {
                const r = await auth("/preparation-stations", {
                  method: "POST",
                  body: JSON.stringify(stationForm),
                });
                if (!r.ok) throw new Error();
                setStationForm({ code: "", nameAr: "", nameEn: "" });
                await load();
                setStationDialogOpen(false);
              } catch {
                setMessage(failure);
              } finally {
                setSaving(false);
              }
            }}
          >
            {(["code", "nameAr", "nameEn"] as const).map((k) => (
              <label key={k} className="text-sm">
                {k === "code"
                  ? tr("رمز المكان *", "Area code *")
                  : k === "nameAr"
                    ? tr("الاسم بالعربية *", "Arabic name *")
                    : tr("الاسم بالإنجليزية *", "English name *")}
                <Input
                  required
                  className={input}
                  maxLength={k === "code" ? 50 : 160}
                  value={stationForm[k]}
                  onChange={(e) =>
                    setStationForm({ ...stationForm, [k]: e.target.value })
                  }
                />
              </label>
            ))}
            <Button disabled={saving} className={button}>
              {tr("إضافة مكان تحضير", "Add preparation area")}
            </Button>
          </form>
        </FormDialog>
      )}
      {groupsFor && (
        <FormDialog
          title={`${name(groupsFor)} — ${groupsFor.type === "Combo" ? tr("تكوين الوجبة", "Build meal") : tr("الإضافات", "Modifiers")}`}
          closeLabel={tr("إغلاق", "Close")}
          onClose={() => setGroupsFor(null)}
        >
          <p className="mb-4 rounded-xl bg-[#f4f7f4] p-3 text-sm text-[#59655f]">{groupsFor.type === "Combo" ? tr("اختر مجموعات الوجبة القابلة لإعادة الاستخدام، مثل المشروبات والبطاطس. تظهر للعميل كوجبة واحدة مرتبة.", "Choose reusable meal groups such as drinks and sides. Customers see one organized meal.") : tr("اختر مجموعات الإضافات التي تنطبق على هذا الصنف.", "Choose the modifier groups that apply to this product.")}</p>
          {groups.map((g) => (
            <label key={g.id} className="flex min-h-11 items-center gap-2">
              <Input
                type="checkbox"
                checked={groupIds.includes(g.id)}
                onChange={(e) =>
                  setGroupIds(
                    e.target.checked
                      ? [...groupIds, g.id]
                      : groupIds.filter((id) => id !== g.id),
                  )
                }
              />
              {name(g)}
            </label>
          ))}
          <Button
            className={`${button} mt-4`}
            disabled={saving}
            onClick={() => void saveGroups()}
          >
            {groupsFor.type === "Combo" ? tr("حفظ تكوين الوجبة", "Save meal") : tr("حفظ الإضافات", "Save modifiers")}
          </Button>
        </FormDialog>
      )}
    </div>
  );
}
