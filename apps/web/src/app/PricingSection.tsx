import { useEffect, useState } from "react";
import { Copy, Plus, RefreshCw } from "lucide-react";
import { FormDialog } from "@/app/FormDialog";
import { SearchableSelect } from "@/app/SearchableSelect";
import { store } from "@/lib/local-store";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

type Language = "ar" | "en";
type Named = { id: string; nameAr: string; nameEn: string };
type Product = Named & { basePrice?: number | null };
type SalesChannelKind = "InStore" | "DineIn" | "Takeaway" | "Qr" | "Electronic";
type Channel = Named & {
  code: string;
  kind?: SalesChannelKind;
  isActive: boolean;
};
type Tab = "channels" | "prices" | "taxes" | "promos";
const localCodes = new Set(["POS", "WEBQR", "DINEIN", "TAKEAWAY"]);
const isElectronicChannel = (channel: Channel) =>
  channel.kind
    ? channel.kind === "Electronic"
    : !localCodes.has(channel.code.toUpperCase());
const date = () => new Date().toISOString().slice(0, 16);
const text = {
  ar: {
    title: "التسعير والضريبة",
    intro:
      "ابدأ بسعر الفرع، ثم انسخه لشركة الطلب الإلكتروني وعدّل الاستثناءات فقط.",
    channels: "قنوات البيع",
    prices: "قوائم الأسعار",
    taxes: "الضريبة",
    promos: "العروض",
    add: "إضافة",
    copy: "إنشاء قائمة من أسعار الفرع",
    code: "الرمز",
    nameAr: "الاسم بالعربية",
    nameEn: "الاسم بالإنجليزية",
    product: "الصنف",
    branch: "الفرع",
    channel: "القناة أو الشركة",
    price: "السعر",
    from: "تاريخ البدء",
    to: "تاريخ الانتهاء",
    adjustment: "نسبة التعديل على أسعار الفرع",
    adjustmentHint: "مثال: 15 لرفع جميع الأسعار 15%، أو 0 لنسخها كما هي.",
    copied: "تم إنشاء قائمة الأسعار",
    save: "حفظ",
    loading: "جارٍ التحميل",
    empty: "لا توجد بيانات بعد",
    error: "تعذر حفظ البيانات.",
    taxCategory: "فئة الضريبة",
    rate: "النسبة %",
    inclusive: "شاملة",
    exclusive: "غير شاملة",
    promoCode: "رمز العرض",
    discount: "الخصم",
    percentage: "نسبة مئوية",
    fixed: "مبلغ ثابت",
    priority: "الأولوية",
    close: "إغلاق",
    individual: "تعديل سعر صنف واحد",
  },
  en: {
    title: "Pricing & tax",
    intro:
      "Start with branch prices, copy them to an electronic-order company, then edit exceptions only.",
    channels: "Sales channels",
    prices: "Price lists",
    taxes: "Tax",
    promos: "Promotions",
    add: "Add",
    copy: "Create list from branch prices",
    code: "Code",
    nameAr: "Arabic name",
    nameEn: "English name",
    product: "Product",
    branch: "Branch",
    channel: "Channel or company",
    price: "Price",
    from: "Starts",
    to: "Ends",
    adjustment: "Branch-price adjustment %",
    adjustmentHint:
      "Example: 15 raises every price by 15%; 0 copies prices unchanged.",
    copied: "Price list created",
    save: "Save",
    loading: "Loading",
    empty: "No data yet",
    error: "Unable to save data.",
    taxCategory: "Tax category",
    rate: "Rate %",
    inclusive: "Inclusive",
    exclusive: "Exclusive",
    promoCode: "Promotion code",
    discount: "Discount",
    percentage: "Percentage",
    fixed: "Fixed amount",
    priority: "Priority",
    close: "Close",
    individual: "Edit one product price",
  },
} as const;

export function PricingSection({ language }: { language: Language }) {
  const t = text[language];
  const name = (x: { nameAr: string; nameEn: string }) =>
    language === "ar" ? x.nameAr : x.nameEn;
  const [tab, setTab] = useState<Tab>("prices");
  const [items, setItems] = useState<any[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [branches, setBranches] = useState<Named[]>([]);
  const [channels, setChannels] = useState<Channel[]>([]);
  const [taxes, setTaxes] = useState<Array<Named & { code: string }>>([]);
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState<"single" | "copy" | null>(null);
  const [notice, setNotice] = useState<{
    message: string;
    error: boolean;
  } | null>(null);
  const auth = (path: string, init?: RequestInit) =>
    fetch(`/api/v1${path}`, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`,
        ...(init?.headers ?? {}),
      },
    });
  async function load() {
    setLoading(true);
    try {
      const endpoint =
        tab === "channels"
          ? "/sales-channels"
          : tab === "prices"
            ? "/price-rules"
            : tab === "taxes"
              ? "/tax-rules"
              : "/promotions";
      const responses = await Promise.all([
        auth(endpoint),
        auth("/products"),
        auth("/branches"),
        auth("/sales-channels"),
        auth("/tax-categories"),
      ]);
      if (responses.some((r) => !r.ok)) throw new Error();
      const data = await Promise.all(responses.map((r) => r.json()));
      setItems(data[0]);
      setProducts(data[1]);
      setBranches(data[2]);
      setChannels(data[3]);
      setTaxes(data[4]);
    } catch {
      setNotice({ message: t.error, error: true });
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    void load();
  }, [tab]);
  async function save(path: string, body: unknown) {
    setNotice(null);
    const response = await auth(path, {
      method: "POST",
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      setNotice({ message: t.error, error: true });
      return false;
    }
    setNotice({
      message: path.includes("copy-branch-menu") ? t.copied : t.save,
      error: false,
    });
    setDialog(null);
    await load();
    return true;
  }
  return (
    <div>
      <p className="text-sm font-semibold text-primary">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold sm:text-3xl">{t.title}</h1>
      <p className="mt-2 max-w-3xl text-muted-foreground">{t.intro}</p>
      <div className="mt-6 flex gap-2 overflow-x-auto border-b">
        {(["prices", "channels", "taxes", "promos"] as Tab[]).map((key) => (
          <Button
            key={key}
            onClick={() => {
              setTab(key);
              setDialog(null);
            }}
            className={`min-h-11 shrink-0 border-b-2 px-4 font-semibold ${tab === key ? "border-primary text-primary" : "border-transparent"}`}
          >
            {t[key]}
          </Button>
        ))}
      </div>
      <div className="mt-5 flex flex-wrap gap-2">
        {tab === "prices" && (
          <Button
            onClick={() => setDialog("copy")}
            className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 font-semibold text-primary-foreground"
          >
            <Copy size={17} />
            {t.copy}
          </Button>
        )}
        <Button
          onClick={() => setDialog("single")}
          className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-primary px-4 font-semibold text-primary"
        >
          <Plus size={17} />
          {tab === "prices" ? t.individual : t.add}
        </Button>
      </div>
      {notice && (
        <p
          role={notice.error ? "alert" : "status"}
          className={`mt-4 rounded-xl border p-3 text-sm ${notice.error ? "border-red-200 bg-red-50 text-red-800" : "border-emerald-200 bg-emerald-50 text-emerald-800"}`}
        >
          {notice.message}
        </p>
      )}
      {loading ? (
        <p className="mt-8 flex items-center gap-2">
          <RefreshCw className="animate-spin" size={18} />
          {t.loading}
        </p>
      ) : (
        <ItemList
          items={items}
          tab={tab}
          language={language}
          empty={t.empty}
          products={products}
          branches={branches}
          channels={channels}
        />
      )}
      {dialog === "copy" && (
        <FormDialog
          title={t.copy}
          closeLabel={t.close}
          onClose={() => setDialog(null)}
        >
          <CopyForm
            t={t}
            branches={branches}
            channels={channels.filter(isElectronicChannel)}
            name={name}
            onSave={(body: unknown) =>
              save("/price-rules/copy-branch-menu", body)
            }
          />
        </FormDialog>
      )}
      {dialog === "single" && (
        <FormDialog
          title={`${t.add} ${t[tab]}`}
          closeLabel={t.close}
          onClose={() => setDialog(null)}
        >
          {tab === "channels" ? (
            <ChannelForm
              t={t}
              onSave={(body: unknown) => save("/sales-channels", body)}
            />
          ) : tab === "prices" ? (
            <PriceForm
              t={t}
              products={products}
              branches={branches}
              channels={channels}
              name={name}
              onSave={(body: unknown) => save("/price-rules", body)}
            />
          ) : tab === "taxes" ? (
            <TaxForm
              t={t}
              categories={taxes}
              branches={branches}
              name={name}
              onSave={(body: unknown) => save("/tax-rules", body)}
            />
          ) : (
            <PromoForm
              t={t}
              products={products}
              branches={branches}
              channels={channels}
              name={name}
              onSave={(body: unknown) => save("/promotions", body)}
            />
          )}
        </FormDialog>
      )}
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  type = "text",
  required = true,
  hint,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  type?: string;
  required?: boolean;
  hint?: string;
}) {
  return (
    <label className="block text-sm font-medium">
      {label}
      <Input
        required={required}
        type={type}
        step={type === "number" ? "any" : undefined}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="mt-2 min-h-11"
      />
      {hint && (
        <span className="mt-1 block text-xs text-muted-foreground">{hint}</span>
      )}
    </label>
  );
}
function Select({
  label,
  value,
  onChange,
  values,
  optional = false,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  values: Array<{ id: string; label: string }>;
  optional?: boolean;
}) {
  return (
    <SearchableSelect label={label} value={value} onChange={onChange}>
      {optional && <option value="">—</option>}
      {values.map((x) => (
        <option key={x.id} value={x.id}>
          {x.label}
        </option>
      ))}
    </SearchableSelect>
  );
}
function Submit({ t }: { t: any }) {
  return (
    <Button className="mt-5 min-h-11 rounded-lg bg-primary px-5 font-semibold text-primary-foreground">
      {t.save}
    </Button>
  );
}
function options(list: Named[], name: (x: Named) => string) {
  return list.map((x) => ({ id: x.id, label: name(x) }));
}
function CopyForm({ t, branches, channels, name, onSave }: any) {
  const [f, setF] = useState({
    branchId: "",
    salesChannelId: "",
    adjustmentPercentage: "0",
  });
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void onSave({
          ...f,
          adjustmentPercentage: Number(f.adjustmentPercentage),
        });
      }}
      className="grid gap-4 sm:grid-cols-2"
    >
      <Select
        label={t.branch}
        value={f.branchId}
        onChange={(branchId) => setF({ ...f, branchId })}
        values={options(branches, name)}
      />
      <Select
        label={t.channel}
        value={f.salesChannelId}
        onChange={(salesChannelId) => setF({ ...f, salesChannelId })}
        values={options(channels, name)}
      />
      <div className="sm:col-span-2">
        <Field
          label={t.adjustment}
          hint={t.adjustmentHint}
          type="number"
          value={f.adjustmentPercentage}
          onChange={(adjustmentPercentage) =>
            setF({ ...f, adjustmentPercentage })
          }
        />
      </div>
      <Submit t={t} />
    </form>
  );
}
function ChannelForm({ t, onSave }: any) {
  const [f, setF] = useState({
    code: "",
    nameAr: "",
    nameEn: "",
    kind: "Electronic",
    isActive: true,
  });
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void onSave(f);
      }}
      className="grid gap-4 sm:grid-cols-3"
    >
      <Field
        label={t.code}
        value={f.code}
        onChange={(code) => setF({ ...f, code })}
      />
      <Field
        label={t.nameAr}
        value={f.nameAr}
        onChange={(nameAr) => setF({ ...f, nameAr })}
      />
      <Field
        label={t.nameEn}
        value={f.nameEn}
        onChange={(nameEn) => setF({ ...f, nameEn })}
      />
      <Select
        label={t.channel}
        value={f.kind}
        onChange={(kind) => setF({ ...f, kind })}
        values={[
          { id: "Electronic", label: languageLabel(t, "Electronic") },
          { id: "InStore", label: languageLabel(t, "InStore") },
          { id: "DineIn", label: languageLabel(t, "DineIn") },
          { id: "Takeaway", label: languageLabel(t, "Takeaway") },
          { id: "Qr", label: "QR" },
        ]}
      />
      <Submit t={t} />
    </form>
  );
}
function languageLabel(t: any, kind: SalesChannelKind) {
  const labels: Record<SalesChannelKind, [string, string]> = {
    Electronic: ["طلب إلكتروني", "Electronic order"],
    InStore: ["داخل الفرع", "In store"],
    DineIn: ["محلي", "Dine in"],
    Takeaway: ["سفري", "Takeaway"],
    Qr: ["QR", "QR"],
  };
  return /[\u0600-\u06FF]/.test(t.title) ? labels[kind][0] : labels[kind][1];
}
function PriceForm({ t, products, branches, channels, name, onSave }: any) {
  const [f, setF] = useState({
    productId: "",
    branchId: "",
    salesChannelId: "",
    price: "",
    effectiveFrom: date(),
    effectiveTo: "",
  });
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void onSave({
          ...f,
          branchId: f.branchId || null,
          salesChannelId: f.salesChannelId || null,
          price: Number(f.price),
          effectiveFrom: new Date(f.effectiveFrom).toISOString(),
          effectiveTo: f.effectiveTo
            ? new Date(f.effectiveTo).toISOString()
            : null,
        });
      }}
      className="grid gap-4 sm:grid-cols-2"
    >
      <Select
        label={t.product}
        value={f.productId}
        onChange={(productId) => setF({ ...f, productId })}
        values={options(products, name)}
      />
      <Select
        optional
        label={t.branch}
        value={f.branchId}
        onChange={(branchId) => setF({ ...f, branchId })}
        values={options(branches, name)}
      />
      <Select
        optional
        label={t.channel}
        value={f.salesChannelId}
        onChange={(salesChannelId) => setF({ ...f, salesChannelId })}
        values={options(channels, name)}
      />
      <Field
        label={t.price}
        type="number"
        value={f.price}
        onChange={(price) => setF({ ...f, price })}
      />
      <Field
        label={t.from}
        type="datetime-local"
        value={f.effectiveFrom}
        onChange={(effectiveFrom) => setF({ ...f, effectiveFrom })}
      />
      <Field
        label={t.to}
        required={false}
        type="datetime-local"
        value={f.effectiveTo}
        onChange={(effectiveTo) => setF({ ...f, effectiveTo })}
      />
      <Submit t={t} />
    </form>
  );
}
function TaxForm({ t, categories, branches, name, onSave }: any) {
  const [f, setF] = useState({
    taxCategoryId: "",
    branchId: "",
    rate: "",
    calculationMode: "Exclusive",
    effectiveFrom: date(),
    effectiveTo: "",
  });
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void onSave({
          ...f,
          branchId: f.branchId || null,
          rate: Number(f.rate),
          effectiveFrom: new Date(f.effectiveFrom).toISOString(),
          effectiveTo: f.effectiveTo
            ? new Date(f.effectiveTo).toISOString()
            : null,
        });
      }}
      className="grid gap-4 sm:grid-cols-2"
    >
      <Select
        label={t.taxCategory}
        value={f.taxCategoryId}
        onChange={(taxCategoryId) => setF({ ...f, taxCategoryId })}
        values={options(categories, name)}
      />
      <Select
        optional
        label={t.branch}
        value={f.branchId}
        onChange={(branchId) => setF({ ...f, branchId })}
        values={options(branches, name)}
      />
      <Field
        label={t.rate}
        type="number"
        value={f.rate}
        onChange={(rate) => setF({ ...f, rate })}
      />
      <Select
        label={t.taxCategory}
        value={f.calculationMode}
        onChange={(calculationMode) => setF({ ...f, calculationMode })}
        values={[
          { id: "Exclusive", label: t.exclusive },
          { id: "Inclusive", label: t.inclusive },
        ]}
      />
      <Submit t={t} />
    </form>
  );
}
function PromoForm({ t, products, branches, channels, name, onSave }: any) {
  const [f, setF] = useState({
    code: "",
    nameAr: "",
    nameEn: "",
    productId: "",
    branchId: "",
    salesChannelId: "",
    discountType: "Percentage",
    discountValue: "",
    priority: "0",
    effectiveFrom: date(),
    effectiveTo: "",
  });
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void onSave({
          ...f,
          productId: f.productId || null,
          branchId: f.branchId || null,
          salesChannelId: f.salesChannelId || null,
          discountValue: Number(f.discountValue),
          priority: Number(f.priority),
          effectiveFrom: new Date(f.effectiveFrom).toISOString(),
          effectiveTo: f.effectiveTo
            ? new Date(f.effectiveTo).toISOString()
            : null,
        });
      }}
      className="grid gap-4 sm:grid-cols-2"
    >
      <Field
        label={t.promoCode}
        value={f.code}
        onChange={(code) => setF({ ...f, code })}
      />
      <Field
        label={t.nameAr}
        value={f.nameAr}
        onChange={(nameAr) => setF({ ...f, nameAr })}
      />
      <Field
        label={t.nameEn}
        value={f.nameEn}
        onChange={(nameEn) => setF({ ...f, nameEn })}
      />
      <Select
        optional
        label={t.product}
        value={f.productId}
        onChange={(productId) => setF({ ...f, productId })}
        values={options(products, name)}
      />
      <Select
        optional
        label={t.branch}
        value={f.branchId}
        onChange={(branchId) => setF({ ...f, branchId })}
        values={options(branches, name)}
      />
      <Select
        optional
        label={t.channel}
        value={f.salesChannelId}
        onChange={(salesChannelId) => setF({ ...f, salesChannelId })}
        values={options(channels, name)}
      />
      <Select
        label={t.discount}
        value={f.discountType}
        onChange={(discountType) => setF({ ...f, discountType })}
        values={[
          { id: "Percentage", label: t.percentage },
          { id: "FixedAmount", label: t.fixed },
        ]}
      />
      <Field
        label={t.discount}
        type="number"
        value={f.discountValue}
        onChange={(discountValue) => setF({ ...f, discountValue })}
      />
      <Field
        label={t.priority}
        type="number"
        value={f.priority}
        onChange={(priority) => setF({ ...f, priority })}
      />
      <Submit t={t} />
    </form>
  );
}
function ItemList({
  items,
  tab,
  language,
  empty,
  products,
  branches,
  channels,
}: {
  items: any[];
  tab: Tab;
  language: Language;
  empty: string;
  products: Product[];
  branches: Named[];
  channels: Channel[];
}) {
  const label = (list: Named[], id?: string | null) => {
    const x = list.find((v) => v.id === id);
    return x
      ? language === "ar"
        ? x.nameAr
        : x.nameEn
      : language === "ar"
        ? "عام"
        : "General";
  };
  return (
    <section className="mt-6 overflow-hidden rounded-xl border bg-card">
      {items.length === 0 ? (
        <p className="p-8 text-center text-sm text-muted-foreground">{empty}</p>
      ) : (
        <div className="divide-y">
          {items.map((item) => (
            <div
              key={item.id}
              className="grid gap-1 p-4 text-sm sm:grid-cols-[1fr_auto]"
            >
              <strong>
                {tab === "channels"
                  ? language === "ar"
                    ? item.nameAr
                    : item.nameEn
                  : tab === "prices"
                    ? label(products, item.productId)
                    : (item.code ?? item.rate ?? item.discountValue)}
              </strong>
              <span className="text-muted-foreground">
                {tab === "prices"
                  ? `${label(branches, item.branchId)} · ${label(channels, item.salesChannelId)} · ${Number(item.price).toFixed(3)} OMR`
                  : (item.code ?? item.nameAr)}
              </span>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
