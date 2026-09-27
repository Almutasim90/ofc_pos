import { useEffect, useState } from "react";
import { FileCheck2, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

// E-invoicing readiness (SRS §74–75): per-branch switch and seller VAT number, a status summary and the
// latest invoices with a resubmit action. Submission itself happens server-side through the configured
// provider; this panel never talks to a tax authority.

type Status = "Pending" | "Accepted" | "Rejected" | "Failed";
type Invoice = {
  id: string;
  invoiceNumber: string;
  issuedAt: string;
  totalAmount: number;
  vatAmount: number;
  status: Status;
  externalReference: string | null;
  lastError: string | null;
};
type Settings = {
  enabled: boolean;
  sellerVatin: string | null;
  availableProviders: string[];
};

const copy = {
  ar: {
    title: "الفوترة الإلكترونية",
    intro:
      "عند التفعيل تُصدر فاتورة إلكترونية لكل طلب مدفوع بعد لحظة التفعيل، وتُرسل عبر مزود الفوترة المعتمد.",
    enabled: "تفعيل الفوترة الإلكترونية لهذا الفرع",
    vatin: "الرقم الضريبي للبائع (VATIN)",
    save: "حفظ",
    saved: "تم الحفظ.",
    failed: "تعذر الحفظ. تحقق من الرقم الضريبي.",
    noProvider:
      "لا يوجد مزود فوترة مربوط بعد؛ ستبقى الفواتير «بانتظار الإرسال» حتى يُربط المزود.",
    provider: "المزود",
    recent: "آخر الفواتير",
    none: "لا توجد فواتير بعد.",
    retry: "إعادة الإرسال",
    statuses: {
      Pending: "بانتظار الإرسال",
      Accepted: "مقبولة",
      Rejected: "مرفوضة",
      Failed: "فشل الإرسال",
    } as Record<Status, string>,
  },
  en: {
    title: "E-invoicing",
    intro:
      "When enabled, an e-invoice is issued for every order paid after that moment and sent through the configured provider.",
    enabled: "Enable e-invoicing for this branch",
    vatin: "Seller VAT number (VATIN)",
    save: "Save",
    saved: "Saved.",
    failed: "Could not save. Check the VAT number.",
    noProvider:
      "No e-invoicing provider is connected yet; invoices stay Pending until one is.",
    provider: "Provider",
    recent: "Latest invoices",
    none: "No invoices yet.",
    retry: "Resubmit",
    statuses: {
      Pending: "Pending",
      Accepted: "Accepted",
      Rejected: "Rejected",
      Failed: "Failed",
    } as Record<Status, string>,
  },
};

const tone: Record<Status, string> = {
  Pending: "bg-warning/15 text-warning",
  Accepted: "bg-success/15 text-success",
  Rejected: "bg-destructive/15 text-destructive",
  Failed: "bg-destructive/15 text-destructive",
};

export function EInvoicingPanel({
  language,
  branchId,
  token,
}: {
  language: "ar" | "en";
  branchId: string;
  token: string;
}) {
  const t = copy[language];
  const [settings, setSettings] = useState<Settings | null>(null);
  const [vatin, setVatin] = useState("");
  const [enabled, setEnabled] = useState(false);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [counts, setCounts] = useState<
    Array<{ status: Status; count: number }>
  >([]);
  const [notice, setNotice] = useState<{ text: string; error: boolean } | null>(
    null,
  );
  const [busy, setBusy] = useState(false);

  const auth = (path: string, init?: RequestInit) =>
    fetch(path, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${token}`,
        ...(init?.headers ?? {}),
      },
    });

  async function load() {
    if (!branchId) return;
    const [settingsResponse, listResponse] = await Promise.all([
      auth(`/api/v1/einvoices/settings?branchId=${branchId}`),
      auth(`/api/v1/einvoices?branchId=${branchId}`),
    ]);
    if (settingsResponse.ok) {
      const value = (await settingsResponse.json()) as Settings;
      setSettings(value);
      setVatin(value.sellerVatin ?? "");
      setEnabled(value.enabled);
    }
    if (listResponse.ok) {
      const value = (await listResponse.json()) as {
        counts: Array<{ status: Status; count: number }>;
        items: Invoice[];
      };
      setInvoices(value.items);
      setCounts(value.counts);
    }
  }
  useEffect(() => {
    setNotice(null);
    void load().catch(() => undefined);
  }, [branchId]);

  async function save(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setNotice(null);
    try {
      const response = await auth("/api/v1/einvoices/settings", {
        method: "PUT",
        body: JSON.stringify({
          branchId,
          enabled,
          sellerVatin: vatin.trim() || null,
        }),
      });
      setNotice(
        response.ok
          ? { text: t.saved, error: false }
          : { text: t.failed, error: true },
      );
      if (response.ok) await load();
    } finally {
      setBusy(false);
    }
  }

  async function retry(id: string) {
    setBusy(true);
    try {
      await auth(`/api/v1/einvoices/${id}/retry`, { method: "POST" });
      await load();
    } finally {
      setBusy(false);
    }
  }

  const money = (value: number) =>
    new Intl.NumberFormat(language, {
      style: "currency",
      currency: "OMR",
      minimumFractionDigits: 3,
    }).format(value);

  if (!settings) return null;
  return (
    <section className="rounded-xl border border-border bg-card p-4">
      <h2 className="flex items-center gap-2 font-semibold">
        <FileCheck2 size={16} aria-hidden="true" />
        {t.title}
      </h2>
      <p className="mt-2 text-sm text-muted-foreground">{t.intro}</p>
      <form onSubmit={save} className="mt-3 grid gap-3">
        <label className="flex min-h-11 items-center gap-2 text-sm font-medium">
          <input
            type="checkbox"
            checked={enabled}
            onChange={(e) => setEnabled(e.target.checked)}
            className="size-4 accent-primary"
          />
          {t.enabled}
        </label>
        <label className="grid gap-1 text-sm font-medium">
          {t.vatin}
          <Input
            value={vatin}
            onChange={(e) => setVatin(e.target.value)}
            maxLength={30}
            dir="ltr"
            placeholder="OM1100000000"
            className="min-h-11 rounded-lg border border-border px-3"
          />
        </label>
        <Button
          type="submit"
          disabled={busy}
          className="min-h-11 justify-self-start rounded-lg bg-primary px-4 text-sm font-semibold text-primary-foreground disabled:opacity-60"
        >
          {t.save}
        </Button>
        {notice && (
          <p
            role="status"
            className={`text-sm ${notice.error ? "text-destructive" : "text-success"}`}
          >
            {notice.text}
          </p>
        )}
      </form>
      <p className="mt-3 text-xs text-muted-foreground">
        {settings.availableProviders.length
          ? `${t.provider}: ${settings.availableProviders.join(", ")}`
          : t.noProvider}
      </p>
      {counts.length > 0 && (
        <ul className="mt-3 flex flex-wrap gap-2">
          {counts.map((c) => (
            <li
              key={c.status}
              className={`rounded-full px-2.5 py-1 text-xs font-semibold ${tone[c.status]}`}
            >
              {t.statuses[c.status]}: {c.count}
            </li>
          ))}
        </ul>
      )}
      <h3 className="mt-4 text-sm font-semibold">{t.recent}</h3>
      {invoices.length === 0 ? (
        <p className="mt-2 text-sm text-muted-foreground">{t.none}</p>
      ) : (
        <ul className="mt-2 divide-y divide-border text-sm">
          {invoices.slice(0, 8).map((invoice) => (
            <li
              key={invoice.id}
              className="flex flex-wrap items-center gap-2 py-2"
            >
              <strong className="tabular-nums">#{invoice.invoiceNumber}</strong>
              <span dir="ltr" className="text-muted-foreground">
                {money(invoice.totalAmount)}
              </span>
              <span
                className={`rounded-full px-2 py-0.5 text-xs font-semibold ${tone[invoice.status]}`}
                title={
                  invoice.lastError ?? invoice.externalReference ?? undefined
                }
              >
                {t.statuses[invoice.status]}
              </span>
              {invoice.status !== "Accepted" && (
                <Button
                  type="button"
                  disabled={busy}
                  onClick={() => void retry(invoice.id)}
                  className="ms-auto inline-flex min-h-9 items-center gap-1 rounded-lg border border-border px-2 text-xs font-semibold"
                >
                  <RefreshCw size={13} aria-hidden="true" />
                  {t.retry}
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
