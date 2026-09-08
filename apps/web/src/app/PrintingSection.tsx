import { useEffect, useState } from "react";
import { Printer, RefreshCw, ShieldCheck } from "lucide-react";
import { createId, store } from "@/lib/local-store";

type Language = "ar" | "en";
type PrinterKind = "Receipt" | "Kitchen" | "CashDrawer";
type JobKind = "Receipt" | "Kitchen" | "CashDrawerOpen";
type JobStatus = "Pending" | "Printing" | "Printed" | "Failed" | "Retrying";
type Branch = { id: string; nameAr: string; nameEn: string };
type Station = { id: string; code: string; nameAr: string; nameEn: string };
type Config = { id: string; code: string; nameAr: string; nameEn: string; kind: PrinterKind; deviceName: string | null; isActive: boolean; sortOrder: number; online: boolean; lastSeenAt: string | null; lastHealthError: string | null };
type Template = { id: string; code: string; nameAr: string; nameEn: string; kind: PrinterKind; widthChars: number; content: string; isActive: boolean };
type Route = { id: string; branchId: string; preparationStationId: string | null; stationCode: string | null; stationNameAr: string | null; stationNameEn: string | null; printerConfigurationId: string; printerCode: string | null; printerNameAr: string | null; printerNameEn: string | null; printTemplateId: string; templateCode: string | null; priority: number; isActive: boolean };
type Job = { id: string; branchId: string; kind: JobKind; status: JobStatus; orderId: string | null; printerConfigurationId: string | null; templateCode: string | null; payload: string; attemptCount: number; maxAttempts: number; nextAttemptAt: string | null; lastError: string | null; printedAt: string | null; createdAt: string };

const copy = {
  ar: {
    title: "الطباعة والأجهزة", intro: "تُنفذ الطباعة عبر وكيل الطباعة المحلي. المتصفح لا يتواصل مباشرة مع الأجهزة بل يرسل المهام إلى الطابور.", branch: "الفرع", agent: "وكيل محلي", configs: "إعدادات الطباعة", templates: "القوالب", routes: "توجيه الطابعات", queue: "طابور الطباعة", addConfig: "إضافة إعداد طابعة", code: "الرمز", nameAr: "الاسم بالعربية", nameEn: "الاسم بالإنجليزية", kind: "النوع", deviceName: "اسم الجهاز", sortOrder: "الترتيب", active: "نشط", inactive: "غير نشط", addTemplate: "إضافة قالب", widthChars: "عرض الأحرف", content: "المحتوى", addRoute: "إضافة مسار توجيه", station: "محطة التحضير", printer: "الطابعة", template: "القالب", priority: "الأولوية", add: "إضافة", enqueue: "إرسال مهمة طباعة", orderId: "رقم الطلب", templateCode: "رمز القالب", payload: "بيانات المهمة", sendToAgent: "إرسال", jobsEmpty: "لا توجد مهام طباعة.", saved: "تم الحفظ.", failed: "تعذر تنفيذ العملية.", enqueued: "تمت إضافة المهمة.", retry: "إعادة المحاولة", fail: "إبلاغ عن فشل", complete: "تمت الطباعة", claim: "استلام المهمة", none: "لا يوجد", statusPending: "قيد الانتظار", statusPrinting: "جارٍ الطباعة", statusPrinted: "مطبوعة", statusFailed: "فاشلة", statusRetrying: "إعادة المحاولة", attempt: "محاولة", jobKindReceipt: "إيصال", jobKindKitchen: "مطبخ", jobKindCashDrawer: "فتح الدرج", printerKindReceipt: "إيصال", printerKindKitchen: "مطبخ", printerKindCashDrawer: "درج نقدي", loading: "جارٍ التحميل", noConfigs: "لا توجد إعدادات بعد", noTemplates: "لا توجد قوالب بعد", noRoutes: "لا توجد مسارات بعد", isolated: "عزل المتصفح عن الأجهزة", defaultRoute: "المسار الافتراضي", stationOptional: "بدون محطة (افتراضي)", agentOnline: "الوكيل متصل", agentOffline: "الوكيل غير متصل"
  },
  en: {
    title: "Printing & hardware", intro: "Printing runs through the local print agent. The browser never talks to hardware directly; it only enqueues jobs into the queue.", branch: "Branch", agent: "Local agent", configs: "Printer configs", templates: "Templates", routes: "Printer routes", queue: "Print queue", addConfig: "Add printer config", code: "Code", nameAr: "Arabic name", nameEn: "English name", kind: "Kind", deviceName: "Device name", sortOrder: "Sort order", active: "Active", inactive: "Inactive", addTemplate: "Add template", widthChars: "Characters", content: "Content", addRoute: "Add route", station: "Preparation station", printer: "Printer", template: "Template", priority: "Priority", add: "Add", enqueue: "Enqueue print job", orderId: "Order id", templateCode: "Template code", payload: "Job payload", sendToAgent: "Enqueue", jobsEmpty: "No print jobs.", saved: "Saved.", failed: "Unable to complete the operation.", enqueued: "Job enqueued.", retry: "Retry", fail: "Report failure", complete: "Printed", claim: "Claim", none: "None", statusPending: "Pending", statusPrinting: "Printing", statusPrinted: "Printed", statusFailed: "Failed", statusRetrying: "Retrying", attempt: "Attempt", jobKindReceipt: "Receipt", jobKindKitchen: "Kitchen", jobKindCashDrawer: "Cash drawer", printerKindReceipt: "Receipt", printerKindKitchen: "Kitchen", printerKindCashDrawer: "Cash drawer", loading: "Loading", noConfigs: "No configurations yet", noTemplates: "No templates yet", noRoutes: "No routes yet", isolated: "Browser isolated from hardware", defaultRoute: "Default", stationOptional: "No station (default)", agentOnline: "Agent online", agentOffline: "Agent offline"
  },
} as const;

const printerKinds: PrinterKind[] = ["Receipt", "Kitchen", "CashDrawer"];
const jobKinds: JobKind[] = ["Receipt", "Kitchen", "CashDrawerOpen"];

export function PrintingSection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) => (language === "ar" ? x.nameAr : x.nameEn);
  const printerKindLabel = (k: PrinterKind) => k === "Receipt" ? t.printerKindReceipt : k === "Kitchen" ? t.printerKindKitchen : t.printerKindCashDrawer;
  const jobKindLabel = (k: JobKind) => k === "Receipt" ? t.jobKindReceipt : k === "Kitchen" ? t.jobKindKitchen : t.jobKindCashDrawer;
  const statusLabel = (s: JobStatus) => s === "Pending" ? t.statusPending : s === "Printing" ? t.statusPrinting : s === "Printed" ? t.statusPrinted : s === "Failed" ? t.statusFailed : t.statusRetrying;

  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [tab, setTab] = useState<"configs" | "templates" | "routes" | "queue">("configs");
  const [stations, setStations] = useState<Station[]>([]);
  const [configs, setConfigs] = useState<Config[]>([]);
  const [templates, setTemplates] = useState<Template[]>([]);
  const [routes, setRoutes] = useState<Route[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);

  const [configForm, setConfigForm] = useState({ code: "", nameAr: "", nameEn: "", kind: "Receipt" as PrinterKind, deviceName: "", sortOrder: "" });
  const [templateForm, setTemplateForm] = useState({ code: "", nameAr: "", nameEn: "", kind: "Kitchen" as PrinterKind, widthChars: "42", content: "" });
  const [routeForm, setRouteForm] = useState({ stationId: "", configId: "", templateId: "", priority: "1" });
  const [enqueueForm, setEnqueueForm] = useState({ kind: "Receipt" as JobKind, orderId: "", stationId: "", templateCode: "", payload: "{\"message\":\"demo\"}" });

  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });
  const setMsg = (value: string, error = false) => { setMessage(value); setIsError(error); };

  async function load(id: string) {
    setLoading(true); setMsg("");
    const [stationsResponse, configsResponse, templatesResponse, routesResponse, jobsResponse] = await Promise.all([
      auth("/api/v1/print/stations"), auth(`/api/v1/print/configs?branchId=${id}`), auth(`/api/v1/print/templates?branchId=${id}`), auth(`/api/v1/print/routes?branchId=${id}`), auth(`/api/v1/print/jobs?branchId=${id}`),
    ]);
    if (stationsResponse.ok) setStations(await stationsResponse.json() as Station[]);
    if (configsResponse.ok) setConfigs(await configsResponse.json() as Config[]);
    if (templatesResponse.ok) setTemplates(await templatesResponse.json() as Template[]);
    if (routesResponse.ok) setRoutes(await routesResponse.json() as Route[]);
    if (jobsResponse.ok) setJobs(await jobsResponse.json() as Job[]);
    setLoading(false);
  }

  useEffect(() => { void (async () => { const response = await auth("/api/v1/pos/context"); if (!response.ok) return; const value = await response.json() as { branches: Branch[] }; setBranches(value.branches); if (value.branches[0]) { setBranchId(value.branches[0].id); } })(); }, []);
  useEffect(() => { if (branchId) void load(branchId); }, [branchId]);
  // Printer health is a timed-out heartbeat (see PrintingRules.HealthStaleAfter), so this tab needs to
  // keep polling to actually show a printer going offline instead of only reflecting the load on open.
  useEffect(() => {
    if (!branchId) return;
    const interval = setInterval(() => { void (async () => { const response = await auth(`/api/v1/print/configs?branchId=${branchId}`); if (response.ok) setConfigs(await response.json() as Config[]); })(); }, 10_000);
    return () => clearInterval(interval);
  }, [branchId]);

  async function submitConfig(event: React.FormEvent) { event.preventDefault(); setMsg(""); try { const response = await auth("/api/v1/print/configs", { method: "POST", body: JSON.stringify({ ...configForm, deviceName: configForm.deviceName.trim() || null, sortOrder: configForm.sortOrder === "" ? 0 : Number(configForm.sortOrder) }) }); if (!response.ok) throw new Error(t.failed); setConfigForm({ code: "", nameAr: "", nameEn: "", kind: "Receipt", deviceName: "", sortOrder: "" }); setMsg(t.saved); void load(branchId); } catch { setMsg(t.failed, true); } }
  async function submitTemplate(event: React.FormEvent) { event.preventDefault(); setMsg(""); try { const response = await auth("/api/v1/print/templates", { method: "POST", body: JSON.stringify({ ...templateForm, widthChars: Number(templateForm.widthChars) || 42 }) }); if (!response.ok) throw new Error(t.failed); setTemplateForm({ code: "", nameAr: "", nameEn: "", kind: "Kitchen", widthChars: "42", content: "" }); setMsg(t.saved); void load(branchId); } catch { setMsg(t.failed, true); } }
  async function submitRoute(event: React.FormEvent) { event.preventDefault(); setMsg(""); if (!routeForm.configId || !routeForm.templateId) { setMsg(t.failed, true); return; } try { const response = await auth("/api/v1/print/routes", { method: "POST", body: JSON.stringify({ branchId, preparationStationId: routeForm.stationId || null, printerConfigurationId: routeForm.configId, printTemplateId: routeForm.templateId, priority: Number(routeForm.priority) || 1 }) }); if (!response.ok) throw new Error(t.failed); setRouteForm({ stationId: "", configId: "", templateId: "", priority: "1" }); setMsg(t.saved); void load(branchId); } catch { setMsg(t.failed, true); } }

  async function enqueue(event: React.FormEvent) { event.preventDefault(); setMsg(""); try { const response = await auth("/api/v1/print/jobs", { method: "POST", body: JSON.stringify({ branchId, orderId: enqueueForm.orderId.trim() || null, clientRequestId: createId(), kind: enqueueForm.kind, preparationStationId: enqueueForm.stationId || null, templateCode: enqueueForm.templateCode.trim() || null, payload: JSON.parse(enqueueForm.payload) }) }); if (!response.ok) throw new Error(t.failed); setMsg(t.enqueued); void load(branchId); } catch { setMsg(t.failed, true); } }

  async function jobAction(id: string, action: "claim" | "complete" | "fail" | "retry") { setMsg(""); const url = action === "fail" ? `/api/v1/print/jobs/${id}/fail` : `/api/v1/print/jobs/${id}/${action}`; const body = action === "fail" ? { error: t.failed } : undefined; const response = await auth(url, body ? { method: "POST", body: JSON.stringify(body) } : { method: "POST" }); if (!response.ok) { const problem = await response.json().catch(() => null); setMsg(problem?.errors?.job?.[0] ?? t.failed, true); return; } await load(branchId); }

  const tabs: Array<["configs" | "templates" | "routes" | "queue", string]> = [["configs", t.configs], ["templates", t.templates], ["routes", t.routes], ["queue", t.queue]];

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <p className="mt-3 max-w-3xl text-[#64716b]">{t.intro}</p>
      <div className="mt-4 flex flex-wrap items-center gap-2 rounded-xl border border-[#d9dfd7] bg-[#edf5f1] p-3 text-sm text-[#08483f]"><ShieldCheck size={18} /><span>{t.isolated}</span><span className="rounded-full bg-white px-2 py-1 text-xs font-semibold">{t.agent}</span></div>

      <div className="mt-5 flex max-w-md flex-col gap-3 sm:flex-row sm:items-end">
        <label className="block flex-1 text-sm font-medium">{t.branch}
          <select value={branchId} onChange={(e) => setBranchId(e.target.value)} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20">{branches.map((branch) => <option key={branch.id} value={branch.id}>{name(branch)}</option>)}</select>
        </label>
      </div>

      <div className="mt-6 flex gap-2 overflow-x-auto pb-1">
        {tabs.map(([key, label]) => <button key={key} onClick={() => setTab(key)} className={`min-h-11 shrink-0 rounded-full px-4 text-sm font-semibold ${tab === key ? "bg-[#0e5a4f] text-white" : "bg-white text-[#53615b]"}`}>{label}</button>)}
      </div>

      {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}
      {loading && <div className="mt-6 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div>}

      {!loading && tab === "configs" && (
        <div className="mt-6 grid gap-5 xl:grid-cols-2">
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h2 className="font-semibold">{t.addConfig}</h2>
            <form onSubmit={submitConfig} className="mt-4 grid gap-4 sm:grid-cols-2">
              <Field label={t.code} value={configForm.code} onChange={(v) => setConfigForm({ ...configForm, code: v })} max={50} />
              <Field label={t.nameAr} value={configForm.nameAr} onChange={(v) => setConfigForm({ ...configForm, nameAr: v })} max={160} />
              <Field label={t.nameEn} value={configForm.nameEn} onChange={(v) => setConfigForm({ ...configForm, nameEn: v })} max={160} />
              <SelectField label={t.kind} value={configForm.kind} onChange={(v) => setConfigForm({ ...configForm, kind: v as PrinterKind })} options={printerKinds.map((k) => [k, printerKindLabel(k)])} />
              <Field label={t.deviceName} value={configForm.deviceName} onChange={(v) => setConfigForm({ ...configForm, deviceName: v })} max={100} />
              <Field label={t.sortOrder} value={configForm.sortOrder} onChange={(v) => setConfigForm({ ...configForm, sortOrder: v })} type="number" />
              <button disabled={loading} className="mt-1 inline-flex min-h-11 items-center gap-2 justify-self-start rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Printer size={18} />{t.add}</button>
            </form>
          </section>
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h2 className="font-semibold">{t.configs}</h2>
            {configs.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.noConfigs}</p> : (
              <ul className="mt-3 divide-y divide-[#e8ece8]">{configs.map((c) => <li key={c.id} className="flex flex-wrap items-center justify-between gap-2 py-3"><div className="min-w-0"><p className="font-medium">{name(c)}</p><p className="text-sm text-[#69766f]">{c.code} · {printerKindLabel(c.kind)}{c.deviceName ? ` · ${c.deviceName}` : ""}</p>{c.lastHealthError && <p className="text-xs text-[#b4322a]">{c.lastHealthError}</p>}</div><div className="flex items-center gap-1.5"><span className={`rounded-full px-3 py-1 text-xs font-semibold ${c.isActive ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#e8ece8] text-[#53615b]"}`}>{c.isActive ? t.active : t.inactive}</span><span title={c.lastSeenAt ? new Date(c.lastSeenAt).toLocaleString(language) : undefined} className={`rounded-full px-3 py-1 text-xs font-semibold ${c.online ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#fbe4e2] text-[#b4322a]"}`}>{c.online ? t.agentOnline : t.agentOffline}</span></div></li>)}</ul>
            )}
          </section>
        </div>
      )}

      {!loading && tab === "templates" && (
        <div className="mt-6 grid gap-5 xl:grid-cols-2">
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h2 className="font-semibold">{t.addTemplate}</h2>
            <form onSubmit={submitTemplate} className="mt-4 grid gap-4 sm:grid-cols-2">
              <Field label={t.code} value={templateForm.code} onChange={(v) => setTemplateForm({ ...templateForm, code: v })} max={50} />
              <SelectField label={t.kind} value={templateForm.kind} onChange={(v) => setTemplateForm({ ...templateForm, kind: v as PrinterKind })} options={printerKinds.map((k) => [k, printerKindLabel(k)])} />
              <Field label={t.nameAr} value={templateForm.nameAr} onChange={(v) => setTemplateForm({ ...templateForm, nameAr: v })} max={160} />
              <Field label={t.nameEn} value={templateForm.nameEn} onChange={(v) => setTemplateForm({ ...templateForm, nameEn: v })} max={160} />
              <Field label={t.widthChars} value={templateForm.widthChars} onChange={(v) => setTemplateForm({ ...templateForm, widthChars: v })} type="number" />
              <label className="block text-sm font-medium sm:col-span-2">{t.content}<textarea value={templateForm.content} onChange={(e) => setTemplateForm({ ...templateForm, content: e.target.value })} maxLength={4000} rows={5} className="mt-2 min-h-24 w-full rounded-lg border border-[#cdd7d0] px-3 py-2 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20" /></label>
              <button disabled={loading} className="mt-1 inline-flex min-h-11 items-center gap-2 justify-self-start rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Printer size={18} />{t.add}</button>
            </form>
          </section>
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h2 className="font-semibold">{t.templates}</h2>
            {templates.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.noTemplates}</p> : (
              <ul className="mt-3 divide-y divide-[#e8ece8]">{templates.map((x) => <li key={x.id} className="py-3"><div className="flex flex-wrap items-center justify-between gap-2"><p className="font-medium">{name(x)}</p><span className="rounded-full bg-[#edf5f1] px-2 py-1 text-xs font-medium text-[#0e5a4f]">{printerKindLabel(x.kind)}</span></div><p className="mt-1 text-sm text-[#69766f]">{x.code} · {x.widthChars}</p></li>)}</ul>
            )}
          </section>
        </div>
      )}

      {!loading && tab === "routes" && (
        <div className="mt-6 grid gap-5 xl:grid-cols-2">
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h2 className="font-semibold">{t.addRoute}</h2>
            <form onSubmit={submitRoute} className="mt-4 grid gap-4 sm:grid-cols-2">
              <label className="block text-sm font-medium">{t.station}<select value={routeForm.stationId} onChange={(e) => setRouteForm({ ...routeForm, stationId: e.target.value })} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{stations.length === 0 && <option value="">{t.stationOptional}</option>}<option value="">{t.stationOptional}</option>{stations.map((s) => <option key={s.id} value={s.id}>{s.code} · {name(s)}</option>)}</select></label>
              <SelectField label={t.printer} required value={routeForm.configId} onChange={(v) => setRouteForm({ ...routeForm, configId: v })} options={configs.map((c) => [c.id, `${name(c)} (${c.code})`])} />
              <SelectField label={t.template} required value={routeForm.templateId} onChange={(v) => setRouteForm({ ...routeForm, templateId: v })} options={templates.map((x) => [x.id, `${name(x)} (${x.code})`])} />
              <Field label={t.priority} value={routeForm.priority} onChange={(v) => setRouteForm({ ...routeForm, priority: v })} type="number" />
              <button disabled={loading} className="mt-1 inline-flex min-h-11 items-center gap-2 justify-self-start rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Printer size={18} />{t.add}</button>
            </form>
          </section>
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h2 className="font-semibold">{t.routes}</h2>
            {routes.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.noRoutes}</p> : (
              <ul className="mt-3 divide-y divide-[#e8ece8]">{routes.map((r) => <li key={r.id} className="flex flex-wrap items-center justify-between gap-2 py-3"><div className="min-w-0"><p className="font-medium">{r.stationCode ? `${r.stationCode} · ${r.stationNameAr ?? r.stationNameEn}` : t.defaultRoute}</p><p className="text-sm text-[#69766f]">{r.printerNameAr ?? r.printerCode} → {r.templateCode}</p></div><span className="rounded-full bg-[#e8ece8] px-3 py-1 text-xs font-semibold text-[#53615b]">{r.priority}</span></li>)}</ul>
            )}
          </section>
        </div>
      )}

      {!loading && tab === "queue" && (
        <div className="mt-6 grid gap-5 xl:grid-cols-2">
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h2 className="font-semibold">{t.enqueue}</h2>
            <form onSubmit={enqueue} className="mt-4 grid gap-4 sm:grid-cols-2">
              <SelectField label={t.kind} value={enqueueForm.kind} onChange={(v) => setEnqueueForm({ ...enqueueForm, kind: v as JobKind })} options={jobKinds.map((k) => [k, jobKindLabel(k)])} />
              <Field label={t.orderId} value={enqueueForm.orderId} onChange={(v) => setEnqueueForm({ ...enqueueForm, orderId: v })} />
              <label className="block text-sm font-medium">{t.station}<select value={enqueueForm.stationId} onChange={(e) => setEnqueueForm({ ...enqueueForm, stationId: e.target.value })} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3"><option value="">{t.stationOptional}</option>{stations.map((s) => <option key={s.id} value={s.id}>{s.code} · {name(s)}</option>)}</select></label>
              <Field label={t.templateCode} value={enqueueForm.templateCode} onChange={(v) => setEnqueueForm({ ...enqueueForm, templateCode: v })} max={50} />
              <label className="block text-sm font-medium sm:col-span-2">{t.payload}<textarea value={enqueueForm.payload} onChange={(e) => setEnqueueForm({ ...enqueueForm, payload: e.target.value })} rows={3} className="mt-2 min-h-20 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 py-2 font-mono text-xs outline-none focus:border-[#0e5a4f]" /></label>
              <button disabled={loading} className="mt-1 inline-flex min-h-11 items-center gap-2 justify-self-start rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60">{t.sendToAgent}</button>
            </form>
          </section>
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h2 className="font-semibold">{t.queue}</h2>
            {jobs.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.jobsEmpty}</p> : (
              <ul className="mt-3 divide-y divide-[#e8ece8]">{jobs.map((job) => (
                <li key={job.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={`rounded-full px-2 py-1 text-xs font-semibold ${job.status === "Printed" ? "bg-[#e3f4ea] text-[#137347]" : job.status === "Failed" ? "bg-[#fbe4e2] text-[#b4322a]" : job.status === "Printing" ? "bg-[#f4f1e3] text-[#8a6d1f]" : "bg-[#e8ece8] text-[#53615b]"}`}>{statusLabel(job.status)}</span>
                      <span className="rounded-full bg-[#edf5f1] px-2 py-1 text-xs font-medium text-[#0e5a4f]">{jobKindLabel(job.kind)}</span>
                    </div>
                    <p className="mt-1 text-sm text-[#69766f]">{t.attempt} {job.attemptCount}/{job.maxAttempts} · {job.templateCode ?? t.none}{job.nextAttemptAt && job.status !== "Printed" ? ` · ${new Intl.DateTimeFormat(language, { timeStyle: "short" }).format(new Date(job.nextAttemptAt))}` : ""}</p>
                    {job.lastError && <p className="mt-1 text-xs text-[#b4322a]">{job.lastError}</p>}
                  </div>
                  {job.status === "Pending" && <button onClick={() => void jobAction(job.id, "claim")} className="min-h-10 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white">{t.claim}</button>}
                  {job.status === "Printing" && <div className="flex gap-2"><button onClick={() => void jobAction(job.id, "complete")} className="min-h-10 rounded-lg bg-[#137347] px-3 text-sm font-semibold text-white">{t.complete}</button><button onClick={() => void jobAction(job.id, "fail")} className="min-h-10 rounded-lg border border-[#b4322a] px-3 text-sm font-semibold text-[#b4322a]">{t.fail}</button></div>}
                  {job.status === "Failed" && <button onClick={() => void jobAction(job.id, "retry")} className="min-h-10 rounded-lg border border-[#0e5a4f] px-3 text-sm font-semibold text-[#0e5a4f]">{t.retry}</button>}
                </li>
              ))}</ul>
            )}
          </section>
        </div>
      )}
    </div>
  );
}

function Field({ label, value, onChange, max, type = "text" }: { label: string; value: string; onChange: (value: string) => void; max?: number; type?: string }) {
  return <label className="block text-sm font-medium">{label}<input type={type} value={value} onChange={(e) => onChange(e.target.value)} maxLength={max} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20" /></label>;
}

function SelectField({ label, value, onChange, options, required }: { label: string; value: string; onChange: (value: string) => void; options: Array<[string, string]>; required?: boolean }) {
  return <label className="block text-sm font-medium">{label}<select required={required} value={value} onChange={(e) => onChange(e.target.value)} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{options.map(([v, text]) => <option key={v} value={v}>{text}</option>)}</select></label>;
}
