import { useEffect, useMemo, useState } from "react";
import { ImagePlus, Printer, Save, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { printHtml } from "@/lib/kitchen-slip";
import {
  emptyReceiptLayout,
  rememberReceiptLayout,
  receiptHtml,
  sampleReceipt,
  type ReceiptLayout,
} from "@/lib/receipt";

type Language = "ar" | "en";
type Auth = (path: string, init?: RequestInit) => Promise<Response>;

const copy = {
  ar: {
    title: "تصميم الفاتورة",
    intro:
      "اكتب ما تريد ظهوره أعلى الفاتورة وأسفلها (اسم المحل، الهاتف، الرقم الضريبي…) وأضف صورة إن أردت. كل الحقول اختيارية، والتصميم خاص بهذا الفرع.",
    header: "الترويسة (أعلى الفاتورة)",
    footer: "الذيل (أسفل الفاتورة)",
    headerPlaceholder:
      "مثال:\nدجاج عُمان المقلي\nفرع الخوض — 9911 2233\nالرقم الضريبي: OM1234567890",
    footerPlaceholder: "مثال:\nشكراً لزيارتكم!\nتابعونا على إنستغرام @ofc",
    image: "صورة",
    addImage: "رفع صورة",
    removeImage: "إزالة الصورة",
    preview: "معاينة",
    save: "حفظ التصميم",
    testPrint: "طباعة تجريبية",
    saved: "تم حفظ تصميم الفاتورة.",
    failed: "تعذر حفظ التصميم.",
    loadFailed: "تعذر تحميل تصميم الفاتورة.",
    badImage: "تعذر قراءة الصورة. استخدم PNG أو JPG.",
  },
  en: {
    title: "Receipt design",
    intro:
      "Write what should appear at the top and bottom of the receipt (shop name, phone, VAT number…) and add an image if you like. Everything is optional, and the design belongs to this branch.",
    header: "Header (top of the receipt)",
    footer: "Footer (bottom of the receipt)",
    headerPlaceholder:
      "e.g.\nOman Fried Chicken\nAl Khoudh branch — 9911 2233\nVAT no: OM1234567890",
    footerPlaceholder: "e.g.\nThank you for visiting!\nFollow us @ofc",
    image: "Image",
    addImage: "Upload image",
    removeImage: "Remove image",
    preview: "Preview",
    save: "Save design",
    testPrint: "Test print",
    saved: "Receipt design saved.",
    failed: "Could not save the design.",
    loadFailed: "Could not load the receipt design.",
    badImage: "Could not read the image. Use PNG or JPG.",
  },
};

// 80mm thermal paper prints about 576 dots across; anything wider only adds upload size.
const maxImageWidth = 576;

// Downsizes an uploaded image to receipt width and re-encodes it, keeping the stored data URL small.
function readImage(file: File) {
  return new Promise<string>((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const image = new Image();
    image.onload = () => {
      URL.revokeObjectURL(url);
      const scale = Math.min(1, maxImageWidth / image.naturalWidth);
      const canvas = document.createElement("canvas");
      canvas.width = Math.max(1, Math.round(image.naturalWidth * scale));
      canvas.height = Math.max(1, Math.round(image.naturalHeight * scale));
      const context = canvas.getContext("2d");
      if (!context) return reject(new Error("canvas"));
      // Thermal paper is white: flatten transparency onto white so it doesn't print black.
      context.fillStyle = "#fff";
      context.fillRect(0, 0, canvas.width, canvas.height);
      context.drawImage(image, 0, 0, canvas.width, canvas.height);
      const png = canvas.toDataURL("image/png");
      resolve(
        png.length <= 300_000 ? png : canvas.toDataURL("image/jpeg", 0.85),
      );
    };
    image.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error("image"));
    };
    image.src = url;
  });
}

export function ReceiptDesigner({
  branchId,
  language,
  auth,
}: {
  branchId: string;
  language: Language;
  auth: Auth;
}) {
  const t = copy[language];
  const [layout, setLayout] = useState<ReceiptLayout>(emptyReceiptLayout);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);

  useEffect(() => {
    if (!branchId) return;
    let cancelled = false;
    setMessage("");
    setLayout(emptyReceiptLayout);
    void (async () => {
      const response = await auth(
        `/api/v1/print/receipt-layout?branchId=${branchId}`,
      );
      if (cancelled) return;
      if (!response.ok) {
        setMessage(t.loadFailed);
        setIsError(true);
        return;
      }
      const value = (await response.json()) as ReceiptLayout;
      if (!cancelled) setLayout(value);
    })();
    return () => {
      cancelled = true;
    };
  }, [branchId]);

  const html = useMemo(
    () => receiptHtml(layout, sampleReceipt(language)),
    [layout, language],
  );

  async function chooseImage(
    field: "headerImage" | "footerImage",
    file: File | undefined,
  ) {
    if (!file) return;
    try {
      const image = await readImage(file);
      setLayout((value) => ({ ...value, [field]: image }));
      setMessage("");
    } catch {
      setMessage(t.badImage);
      setIsError(true);
    }
  }

  async function save() {
    setSaving(true);
    setMessage("");
    try {
      const response = await auth(
        `/api/v1/print/receipt-layout?branchId=${branchId}`,
        { method: "PUT", body: JSON.stringify(layout) },
      );
      if (!response.ok) {
        const problem = await response.json().catch(() => null);
        const errors = problem?.errors as Record<string, string[]> | undefined;
        throw new Error((errors && Object.values(errors)[0]?.[0]) ?? t.failed);
      }
      const saved = (await response.json()) as ReceiptLayout;
      setLayout(saved);
      // Refresh this device's cached copy so its next receipt uses the new design.
      rememberReceiptLayout(branchId, saved);
      setMessage(t.saved);
      setIsError(false);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : t.failed);
      setIsError(true);
    } finally {
      setSaving(false);
    }
  }

  const part = (
    title: string,
    textField: "headerText" | "footerText",
    imageField: "headerImage" | "footerImage",
    placeholder: string,
  ) => (
    <fieldset className="rounded-xl border border-border bg-card p-4">
      <legend className="px-1 font-semibold">{title}</legend>
      <textarea
        value={layout[textField] ?? ""}
        onChange={(e) =>
          setLayout({ ...layout, [textField]: e.target.value || null })
        }
        maxLength={1000}
        rows={5}
        placeholder={placeholder}
        className="min-h-28 w-full rounded-lg border border-border bg-background px-3 py-2 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
      />
      <div className="mt-3 flex flex-wrap items-center gap-3">
        {layout[imageField] && (
          <img
            src={layout[imageField]!}
            alt={t.image}
            className="h-16 max-w-40 rounded border border-border bg-white object-contain"
          />
        )}
        <label className="inline-flex min-h-10 cursor-pointer items-center gap-2 rounded-lg border border-border px-3 text-sm font-semibold hover:bg-accent">
          <ImagePlus size={16} aria-hidden="true" />
          {t.addImage}
          <input
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="sr-only"
            onChange={(e) => {
              void chooseImage(imageField, e.target.files?.[0]);
              e.target.value = "";
            }}
          />
        </label>
        {layout[imageField] && (
          <Button
            type="button"
            onClick={() => setLayout({ ...layout, [imageField]: null })}
            className="inline-flex min-h-10 items-center gap-2 rounded-lg bg-transparent px-3 text-sm font-semibold text-destructive hover:bg-destructive/10"
          >
            <Trash2 size={16} aria-hidden="true" />
            {t.removeImage}
          </Button>
        )}
      </div>
    </fieldset>
  );

  return (
    <div className="mt-6 grid gap-6 lg:grid-cols-[minmax(0,1fr)_340px]">
      <div className="space-y-4">
        <div>
          <h2 className="font-semibold">{t.title}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t.intro}</p>
        </div>
        {part(t.header, "headerText", "headerImage", t.headerPlaceholder)}
        {part(t.footer, "footerText", "footerImage", t.footerPlaceholder)}
        {message && (
          <p
            role={isError ? "alert" : "status"}
            className={`text-sm ${isError ? "text-destructive" : "text-success"}`}
          >
            {message}
          </p>
        )}
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            disabled={saving || !branchId}
            onClick={() => void save()}
            className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-primary px-4 font-semibold text-primary-foreground disabled:opacity-60"
          >
            <Save size={18} aria-hidden="true" />
            {t.save}
          </Button>
          <Button
            type="button"
            onClick={() => printHtml(html)}
            className="inline-flex min-h-11 items-center gap-2 rounded-lg border border-border bg-card px-4 font-semibold text-foreground hover:bg-accent"
          >
            <Printer size={18} aria-hidden="true" />
            {t.testPrint}
          </Button>
        </div>
      </div>
      <div>
        <h3 className="mb-2 text-sm font-semibold text-muted-foreground">
          {t.preview}
        </h3>
        {/* Paper is always white, whatever the app theme. */}
        <iframe
          title={t.preview}
          srcDoc={html.replace("<style>", "<style>html{padding:12px;}")}
          sandbox=""
          className="h-[640px] w-full max-w-[340px] rounded-xl border border-border bg-white shadow-sm"
        />
      </div>
    </div>
  );
}
