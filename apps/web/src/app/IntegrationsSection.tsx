import { useEffect, useState } from "react";
import { Bot, Inbox, Plug, RefreshCw, Rocket, Send, ShieldCheck, ToggleLeft, ToggleRight } from "lucide-react";
import { store } from "@/lib/local-store";

type Language = "ar" | "en";
type Branch = { id: string; code: string; nameAr: string; nameEn: string; isActive: boolean };
type Preference = { kind: string; preferenceKey: string; nameAr: string; nameEn: string; enabled: boolean; sensitive: boolean; requiresReview: boolean };
type OutboxItem = { id: string; branchId: string | null; kind: string; channel: string; status: string; type: string; payload: string; correlationId: string; idempotencyKey: string; attempts: number; maxAttempts: number; lastError: string | null; createdAt: string; nextAttemptAt: string | null };
type Suggestion = { kind: string; titleAr: string; titleEn: string; detail: string | null; confidence: number; requiresReview: boolean };
type AiResult = { available: boolean; mode: string; suggestions: Suggestion[]; safeFallback: boolean; note?: string; error?: string };
type LoadState = "idle" | "loading" | "error";

const copy = {
  ar: {
    title: "التكاملات والذكاء الاصطناعي", intro: "نقاط توسّع اختيارية (الولاء، إدارة العملاء، الطلب الإلكتروني، التوصيل، الإشعارات، الفوترة) ومحفّزات ذكاء اصطناعي آمنة لا تعارض النواة.", selectBranch: "اختر الفرع", extensionPoints: "نقاط التوسّع والتفضيلات", enable: "تفعيل", disable: "تعطيل", test: "اختبار", enabled: "مفعّل", disabled: "معطّل", sensitive: "حساس (يخضع للتدقيق)", requiresReview: "مراجعة إنسانية", testDisabled: "معطّل حسب التفضيل", testOk: "يعمل في وضع الواجهة فقط", testFail: "فشل الاختبار بأمان", outbox: "صندوق الإشعارات (Outbox)", outboxIntro: "أساس الإشعارات/الويب هوك: تُعطّل الموصّلات افتراضياً ولا تمسّ عمليات النواة.", enqueue: "إضافة سجل إشعار", typePlaceholder: "نوع الحدث (مثال: order.completed)", payloadPlaceholder: "الحمولة بصيغة JSON", noOutbox: "لا توجد سجلات", queued: "قيد الانتظار", dispatched: "تم الإرسال", failed: "فشل", skipped: "تخطّي", deferred: "مؤجّل", dispatching: "جارٍ الإرسال", ai: "مساعد الذكاء الاصطناعي (آمن وغير معطِّل)", aiIntro: "لا يطبّق AI أي قرارات تلقائياً ولا يغيّر أي طلب؛ كل اقتراح يتطلب موافقة إدارية.", getSuggestions: "طلب اقتراحات", aiDisabled: "مساعد AI معطّل. فعّله من التفضيلات أو استخدم صلاحيات الإدارة.", aiDegraded: "تعذّر الاتصال بالمساعد؛ عمليات النواة غير متأثرة.", aiSafe: "اقتراحات إرشادية فقط وتتطلب المراجعة.", confidence: "ثقة", requiresHuman: "يتطلب مراجعة", samplePayload: "{\"orderId\":\"...\",\"total\":2.7}", loading: "جارٍ التحميل...", error: "تعذر تحميل بيانات التكاملات", retry: "إعادة المحاولة", saved: "تم الحفظ", language: "English", empty: "لا توجد بيانات"
  } as const,
  en: {
    title: "Integrations & AI", intro: "Optional extension points (loyalty, customer CRM, online ordering, delivery, notifications, billing) and safe, non-blocking AI assist that never conflicts with the core.", selectBranch: "Select branch", extensionPoints: "Extension points & preferences", enable: "Enable", disable: "Disable", test: "Test", enabled: "Enabled", disabled: "Disabled", sensitive: "Sensitive (audited)", requiresReview: "Human review", testDisabled: "Disabled by preference", testOk: "Works in stub mode", testFail: "Test failed safely", outbox: "Notification outbox", outboxIntro: "Webhook/notification foundation: adapters are disabled by default and never touch core operations.", enqueue: "Enqueue notification", typePlaceholder: "Event type (e.g. order.completed)", payloadPlaceholder: "JSON payload", noOutbox: "No outbox records", queued: "Queued", dispatched: "Dispatched", failed: "Failed", skipped: "Skipped", deferred: "Deferred", dispatching: "Dispatching", ai: "AI assist (safe & non-blocking)", aiIntro: "AI never applies decisions automatically and never mutates an order; every suggestion needs manager approval.", getSuggestions: "Get suggestions", aiDisabled: "AI assist is disabled. Enable it in preferences or use admin permissions.", aiDegraded: "AI assist unavailable; core operations are unaffected.", aiSafe: "Advisory suggestions only and always require review.", confidence: "Confidence", requiresHuman: "Requires review", samplePayload: "{\"orderId\":\"...\",\"total\":2.7}", loading: "Loading...", error: "Unable to load integration data", retry: "Retry", saved: "Saved", language: "العربية", empty: "No data"
  } as const,
};

function nameArEn<T extends { nameAr: string; nameEn: string }>(item: T, language: Language) { return language === "ar" ? item.nameAr : item.nameEn; }

const statusKey = (status: string): keyof typeof copy.ar => {
  const map: Record<string, keyof typeof copy.ar> = { Queued: "queued", Dispatching: "dispatching", Dispatched: "dispatched", Failed: "failed", Skipped: "skipped", Deferred: "deferred" };
  return map[status] ?? "empty";
};
const statusClass = (status: string) => status === "Dispatched" ? "bg-[#e3f4ea] text-[#137347]" : status === "Failed" ? "bg-[#fbe4e2] text-[#b4322a]" : status === "Skipped" ? "bg-[#f4f1e3] text-[#8a6d1f]" : status === "Deferred" ? "bg-[#edf1ee] text-[#53615b]" : "bg-[#e6f1ec] text-[#08483f]";

export function IntegrationsSection({ language }: { language: Language }) {
  const t = copy[language];
  const token = store.get<string>("session-token") ?? "";
  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [prefs, setPrefs] = useState<Preference[]>([]);
  const [outbox, setOutbox] = useState<OutboxItem[]>([]);
  const [ai, setAi] = useState<AiResult | null>(null);
  const [state, setState] = useState<LoadState>("idle");
  const [notice, setNotice] = useState<{ text: string; error: boolean } | null>(null);
  const [sample, setSample] = useState<{ type: string; payload: string }>({ type: "order.completed", payload: copy.ar.samplePayload });
  const [aiLoading, setAiLoading] = useState(false);
  const [enqueuing, setEnqueuing] = useState(false);

  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}`, ...(init?.headers ?? {}) } });

  async function load() {
    setState("loading");
    setNotice(null);
    try {
      const contextResponse = await auth("/api/v1/pos/context");
      if (!contextResponse.ok) throw new Error();
      const context = await contextResponse.json() as { branches: Branch[] };
      setBranches(context.branches.filter((b) => b.isActive));
      const branch = context.branches.find((b) => b.isActive) ?? context.branches[0];
      if (branch) setBranchId(branch.id);
      setState("idle");
    } catch {
      setState("error");
    }
  }

  useEffect(() => { void load(); }, []);

  async function loadBranch() {
    if (!branchId) return;
    try {
      const [prefsResponse, outboxResponse] = await Promise.all([auth(`/api/v1/integrations/preferences?branchId=${branchId}`), auth(`/api/v1/integrations/outbox?branchId=${branchId}`)]);
      if (!prefsResponse.ok || !outboxResponse.ok) throw new Error();
      const prefsBody = await prefsResponse.json() as { extensionPoints: Preference[] };
      setPrefs(prefsBody.extensionPoints);
      setOutbox(await outboxResponse.json() as OutboxItem[]);
      setNotice(null);
    } catch {
      setNotice({ text: t.error, error: true });
    }
  }

  useEffect(() => { void loadBranch(); }, [branchId]);

  async function setPreference(kind: string, enabled: boolean) {
    setNotice(null);
    try {
      const response = await auth("/api/v1/integrations/preferences", { method: "POST", body: JSON.stringify({ branchId, kind, enabled }) });
      if (!response.ok) throw new Error(t.error);
      setNotice({ text: t.saved, error: false });
      void loadBranch();
    } catch (e) {
      setNotice({ text: e instanceof Error ? e.message : t.error, error: true });
    }
  }

  async function testAdapter(kind: string) {
    setNotice(null);
    try {
      const response = await auth(`/api/v1/integrations/preferences/${kind}/test?branchId=${branchId}`, { method: "POST" });
      if (!response.ok) throw new Error(t.error);
      const result = await response.json() as { success: boolean; enabled: boolean; detail: string };
      setNotice({ text: result.success ? t.testOk : result.enabled ? t.testFail : t.testDisabled, error: !result.success });
    } catch (e) {
      setNotice({ text: e instanceof Error ? e.message : t.error, error: true });
    }
  }

  async function enqueueSample(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setEnqueuing(true);
    setNotice(null);
    try {
      const body = { branchId, kind: "Notifications", channel: "Webhook", type: sample.type || "order.completed", payload: sample.payload || "{}", idempotencyKey: crypto.randomUUID() };
      const response = await auth("/api/v1/integrations/outbox", { method: "POST", body: JSON.stringify(body) });
      if (!response.ok) throw new Error(t.error);
      setNotice({ text: t.saved, error: false });
      setSample({ type: "order.completed", payload: copy.ar.samplePayload });
      void loadBranch();
    } catch (e) {
      setNotice({ text: e instanceof Error ? e.message : t.error, error: true });
    } finally {
      setEnqueuing(false);
    }
  }

  async function dispatch(id: string) {
    setNotice(null);
    try {
      const response = await auth(`/api/v1/integrations/outbox/${id}/dispatch`, { method: "POST" });
      if (!response.ok) throw new Error(t.error);
      void loadBranch();
    } catch (e) {
      setNotice({ text: e instanceof Error ? e.message : t.error, error: true });
    }
  }

  async function suggest() {
    setAiLoading(true);
    setNotice(null);
    try {
      const response = await auth("/api/v1/integrations/ai/suggest", { method: "POST", body: JSON.stringify({ branchId, context: "upsell" }) });
      if (!response.ok) throw new Error(t.error);
      setAi(await response.json() as AiResult);
    } catch (e) {
      setNotice({ text: e instanceof Error ? e.message : t.error, error: true });
    } finally {
      setAiLoading(false);
    }
  }

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]"><Plug className="inline" size={16} /> {t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <p className="mt-3 max-w-3xl text-[#64716b]">{t.intro}</p>

      {state === "loading" && <div className="mt-8 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div>}
      {state === "error" && <div role="alert" className="mt-8 rounded-xl border border-[#efc5c1] bg-[#fff5f4] p-5 text-[#9b2922]"><p>{t.error}</p><button onClick={() => void load()} className="mt-3 font-semibold underline">{t.retry}</button></div>}
      {notice && <p role={notice.error ? "alert" : "status"} className={`mt-4 text-sm ${notice.error ? "text-[#b4322a]" : "text-[#137347]"}`}>{notice.text}</p>}

      {state === "idle" && (
        <div className="mt-6 grid gap-5 lg:grid-cols-[260px_1fr]">
          <aside className="space-y-5">
            <label className="block text-sm font-medium">{t.selectBranch}<select value={branchId} onChange={(e) => setBranchId(e.target.value)} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f]">{branches.length === 0 && <option value="">{t.empty}</option>}{branches.map((b) => <option key={b.id} value={b.id}>{nameArEn(b, language)}</option>)}</select></label>

            <section className="rounded-xl border border-[#dfe5df] bg-white p-4">
              <h2 className="flex items-center gap-2 font-semibold"><Bot size={16} />{t.ai}</h2>
              <p className="mt-2 text-sm text-[#69766f]">{t.aiIntro}</p>
              <button onClick={() => void suggest()} disabled={aiLoading} className="mt-4 inline-flex min-h-11 w-full items-center justify-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Rocket size={16} />{aiLoading ? t.loading : t.getSuggestions}</button>
              {ai && (
                <div className="mt-4 space-y-2">
                  <p className={`text-xs font-semibold ${ai.mode === "disabled" ? "text-[#8a6d1f]" : ai.mode === "degraded" ? "text-[#b4322a]" : "text-[#137347]"}`}>{ai.mode === "disabled" ? t.aiDisabled : ai.mode === "degraded" ? (ai.error ?? t.aiDegraded) : t.aiSafe}</p>
                  {ai.suggestions.map((s, i) => (
                    <div key={i} className="rounded-lg border border-[#e3e9e4] bg-[#f8faf8] p-3">
                      <p className="text-sm font-medium">{language === "ar" ? s.titleAr : s.titleEn}</p>
                      {s.detail && <p className="mt-1 text-xs text-[#69766f]">{s.detail}</p>}
                      <div className="mt-2 flex flex-wrap items-center gap-2 text-xs"><span className="rounded-full bg-[#e6f1ec] px-2 py-0.5 font-semibold text-[#08483f]">{t.confidence}: {(s.confidence * 100).toFixed(0)}%</span><span className="flex items-center gap-1 rounded-full bg-[#f4f1e3] px-2 py-0.5 font-semibold text-[#8a6d1f]"><ShieldCheck size={12} />{t.requiresHuman}</span></div>
                    </div>
                  ))}
                </div>
              )}
            </section>
          </aside>

          <section className="space-y-5">
            <div className="rounded-xl border border-[#dfe5df] bg-white">
              <div className="flex items-center justify-between border-b border-[#e8ece8] px-5 py-4"><h2 className="font-semibold">{t.extensionPoints} ({prefs.length})</h2></div>
              {prefs.length === 0 ? <p className="p-8 text-center text-sm text-[#69766f]">{t.empty}</p> : (
                <ul className="divide-y divide-[#e8ece8]">{prefs.map((p) => (
                  <li key={p.kind} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                    <div className="min-w-0"><p className="font-medium">{language === "ar" ? p.nameAr : p.nameEn}</p><p className="mt-1 text-xs text-[#69766f]">{p.preferenceKey}{p.sensitive && <span className="ms-2 rounded-full bg-[#fbe4e2] px-2 py-0.5 font-semibold text-[#b4322a]">{t.sensitive}</span>}{p.requiresReview && <span className="ms-2 rounded-full bg-[#f4f1e3] px-2 py-0.5 font-semibold text-[#8a6d1f]">{t.requiresReview}</span>}</p></div>
                    <div className="flex items-center gap-2">
                      <span className={`rounded-full px-3 py-1 text-xs font-semibold ${p.enabled ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#e8ece8] text-[#53615b]"}`}>{p.enabled ? t.enabled : t.disabled}</span>
                      <button onClick={() => void setPreference(p.kind, !p.enabled)} className={`inline-flex min-h-9 items-center gap-1.5 rounded-lg border px-3 text-xs font-semibold ${p.enabled ? "border-[#b4322a] text-[#b4322a]" : "border-[#0e5a4f] text-[#0e5a4f]"}`}>{p.enabled ? <ToggleLeft size={14} /> : <ToggleRight size={14} />}{p.enabled ? t.disable : t.enable}</button>
                      <button onClick={() => void testAdapter(p.kind)} className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#cdd7d0] px-3 text-xs font-semibold text-[#53615b]">{t.test}</button>
                    </div>
                  </li>
                ))}</ul>
              )}
            </div>

            <div className="rounded-xl border border-[#dfe5df] bg-white">
              <div className="flex items-center justify-between border-b border-[#e8ece8] px-5 py-4"><h2 className="flex items-center gap-2 font-semibold"><Inbox size={16} />{t.outbox} ({outbox.length})</h2></div>
              <form onSubmit={enqueueSample} className="grid gap-3 border-b border-[#e8ece8] p-4 sm:grid-cols-[1fr_1fr_auto]">
                <input value={sample.type} onChange={(e) => setSample({ ...sample, type: e.target.value })} placeholder={t.typePlaceholder} maxLength={80} className="min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 text-sm outline-none focus:border-[#0e5a4f]" />
                <input value={sample.payload} onChange={(e) => setSample({ ...sample, payload: e.target.value })} placeholder={t.payloadPlaceholder} maxLength={16000} className="min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 text-sm outline-none focus:border-[#0e5a4f]" />
                <button disabled={enqueuing} className="inline-flex min-h-11 items-center justify-center gap-2 rounded-lg bg-[#0e5a4f] px-4 text-sm font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Send size={16} />{enqueuing ? t.loading : t.enqueue}</button>
              </form>
              {outbox.length === 0 ? <p className="p-8 text-center text-sm text-[#69766f]">{t.noOutbox}</p> : (
                <ul className="divide-y divide-[#e8ece8]">{outbox.map((o) => (
                  <li key={o.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                    <div className="min-w-0"><p className="text-sm font-medium">{o.kind} · {o.type} · {o.channel}</p><p className="mt-1 truncate text-xs text-[#69766f]">{o.payload}</p><p className="mt-1 text-xs text-[#69766f]">{o.attempts}/{o.maxAttempts} · {(o.lastError ?? o.correlationId)}</p></div>
                    <div className="flex items-center gap-2"><span className={`rounded-full px-3 py-1 text-xs font-semibold ${statusClass(o.status)}`}>{t[statusKey(o.status)]}</span><button onClick={() => void dispatch(o.id)} disabled={o.status === "Dispatched"} className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#0e5a4f] px-3 text-xs font-semibold text-[#0e5a4f] disabled:opacity-40">{t.test}</button></div>
                  </li>
                ))}</ul>
              )}
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
