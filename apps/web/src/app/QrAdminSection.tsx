import { useEffect, useState } from "react";
import { Check, Plus, Power, RefreshCw, X } from "lucide-react";
import { store } from "@/lib/local-store";

type Language = "ar" | "en";
type Channel = { id: string; code: string; nameAr: string; nameEn: string };
type Branch = { id: string; code: string; nameAr: string; nameEn: string; isActive: boolean };
type QrContextItem = { id: string; branchId: string; code: string; kind: "Table" | "Parking" | "Branch"; nameAr: string; nameEn: string; approvalMode: "None" | "AutoApprove" | "RequiresStaffApproval"; isActive: boolean; salesChannelId: string; salesChannelCode: string | null; salesChannelNameAr: string | null; salesChannelNameEn: string | null };
type QrOrder = { id: string; status: string; grossAmount: number; createdAt: string; clientRequestId: string; lines: Array<{ id: string; productNameAr: string; productNameEn: string; quantity: number }>; approval: { id: string; status: string; note: string | null } | null };
type LoadState = "idle" | "loading" | "error";

const copy = {
  ar: {
    title: "QR والطلبات الذاتية", intro: "أنشئ أكواد الطاولات والمواقف، وراجع طلبات العملاء واعتمدها قبل إرسالها.", selectBranch: "اختر الفرع", contexts: "أكواد QR", addContext: "إضافة كود جديد", code: "الكود", nameAr: "الاسم بالعربية", nameEn: "الاسم بالإنجليزية", type: "النوع", table: "طاولة", parking: "موقف", branch: "الفرع", channel: "قناة البيع", approvalMode: "وضع الاعتماد", none: "بدون", auto: "اعتماد تلقائي", manual: "اعتماد موظف", active: "فعال", inactive: "معطل", toggle: "تبديل", noContexts: "لا توجد أكواد بعد", create: "إنشاء", pending: "طلبات بانتظار الاعتماد", orders: "طلبات QR", noPending: "لا توجد طلبات معلّقة", noOrders: "لا توجد طلبات", approve: "اعتماد", reject: "رفض", loading: "جارٍ التحميل...", error: "تعذر تحميل بيانات QR", retry: "إعادة المحاولة", saved: "تم الحفظ", language: "English", customer: "العميل", walkIn: "زائر", amount: "المبلغ", status: "الحالة", empty: "لا توجد بيانات"
  } as const,
  en: {
    title: "QR & self ordering", intro: "Create table and parking QR codes, review customer orders, and approve them before they go to the kitchen.", selectBranch: "Select branch", contexts: "QR codes", addContext: "Add new code", code: "Code", nameAr: "Arabic name", nameEn: "English name", type: "Type", table: "Table", parking: "Parking", branch: "Branch", channel: "Sales channel", approvalMode: "Approval mode", none: "None", auto: "Auto approve", manual: "Staff approval", active: "Active", inactive: "Inactive", toggle: "Toggle", noContexts: "No QR codes yet", create: "Create", pending: "Orders awaiting approval", orders: "QR orders", noPending: "No pending orders", noOrders: "No orders yet", approve: "Approve", reject: "Reject", loading: "Loading...", error: "Unable to load QR data", retry: "Retry", saved: "Saved", language: "العربية", customer: "Customer", walkIn: "Walk-in", amount: "Amount", status: "Status", empty: "No data"
  } as const,
};

function nameArEn<T extends { nameAr: string; nameEn: string }>(item: T, language: Language) { return language === "ar" ? item.nameAr : item.nameEn; }

export function QrAdminSection({ language }: { language: Language }) {
  const t = copy[language];
  const token = store.get<string>("session-token") ?? "";
  const [branches, setBranches] = useState<Branch[]>([]);
  const [channels, setChannels] = useState<Channel[]>([]);
  const [branchId, setBranchId] = useState("");
  const [contexts, setContexts] = useState<QrContextItem[]>([]);
  const [orders, setOrders] = useState<QrOrder[]>([]);
  const [state, setState] = useState<LoadState>("idle");
  const [notice, setNotice] = useState<{ text: string; error: boolean } | null>(null);
  const [form, setForm] = useState({ code: "", nameAr: "", nameEn: "", kind: "Table" as QrContextItem["kind"], channelId: "", approvalMode: "AutoApprove" as QrContextItem["approvalMode"] });

  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}`, ...(init?.headers ?? {}) } });

  async function load() {
    setState("loading");
    setNotice(null);
    try {
      const contextResponse = await auth("/api/v1/pos/context");
      if (!contextResponse.ok) throw new Error();
      const context = await contextResponse.json() as { branches: Branch[]; channels: Channel[] };
      setBranches(context.branches.filter((b) => b.isActive));
      setChannels(context.channels);
      const branch = context.branches.find((b) => b.isActive) ?? context.branches[0];
      if (branch) { setBranchId(branch.id); }
      setState("idle");
    } catch {
      setState("error");
    }
  }

  useEffect(() => { void load(); }, []);

  async function loadBranch() {
    if (!branchId) return;
    try {
      const [contextsResponse, ordersResponse] = await Promise.all([auth(`/api/v1/qr/contexts?branchId=${branchId}`), auth(`/api/v1/qr/orders?branchId=${branchId}`)]);
      if (!contextsResponse.ok || !ordersResponse.ok) throw new Error();
      const [ctxList, orderList] = await Promise.all([contextsResponse.json() as Promise<QrContextItem[]>, ordersResponse.json() as Promise<QrOrder[]>]);
      setContexts(ctxList);
      setOrders(orderList);
      setNotice(null);
    } catch {
      setNotice({ text: t.error, error: true });
    }
  }

  useEffect(() => { void loadBranch(); }, [branchId]);

  async function createContext(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setNotice(null);
    try {
      const response = await auth("/api/v1/qr/contexts", { method: "POST", body: JSON.stringify({ branchId, salesChannelId: form.channelId || channels[0]?.id, kind: form.kind, code: form.code, nameAr: form.nameAr, nameEn: form.nameEn, approvalMode: form.approvalMode, isActive: true }) });
      if (!response.ok) throw new Error(t.error);
      setNotice({ text: t.saved, error: false });
      setForm({ code: "", nameAr: "", nameEn: "", kind: "Table", channelId: "", approvalMode: "AutoApprove" });
      void loadBranch();
    } catch (e) {
      setNotice({ text: e instanceof Error ? e.message : t.error, error: true });
    }
  }

  async function toggleContext(id: string) {
    setNotice(null);
    try { await auth(`/api/v1/qr/contexts/${id}/toggle`, { method: "POST" }); void loadBranch(); } catch { setNotice({ text: t.error, error: true }); }
  }

  async function review(approvalId: string, decision: "approve" | "reject") {
    setNotice(null);
    try {
      const response = await auth(`/api/v1/qr/approvals/${approvalId}/review`, { method: "POST", body: JSON.stringify({ decision }) });
      if (!response.ok) throw new Error(t.error);
      void loadBranch();
    } catch (e) {
      setNotice({ text: e instanceof Error ? e.message : t.error, error: true });
    }
  }

  const pending = orders.filter((o) => o.approval?.status === "Pending");
  const kindLabel = { Table: t.table, Parking: t.parking, Branch: t.branch };
  const modeLabel = { None: t.none, AutoApprove: t.auto, RequiresStaffApproval: t.manual };

  const fmt = (value: number) => new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 3 }).format(value);
  const fmtDate = (value: string) => new Intl.DateTimeFormat(language, { dateStyle: "short", timeStyle: "short" }).format(new Date(value));

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
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
              <h2 className="font-semibold">{t.addContext}</h2>
              <form onSubmit={createContext} className="mt-4 space-y-3">
                <input value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} placeholder={t.code} maxLength={50} className="min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 text-sm outline-none focus:border-[#0e5a4f]" />
                <input value={form.nameAr} onChange={(e) => setForm({ ...form, nameAr: e.target.value })} placeholder={t.nameAr} maxLength={160} className="min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 text-sm outline-none focus:border-[#0e5a4f]" />
                <input value={form.nameEn} onChange={(e) => setForm({ ...form, nameEn: e.target.value })} placeholder={t.nameEn} maxLength={160} className="min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3 text-sm outline-none focus:border-[#0e5a4f]" />
                <select value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value as QrContextItem["kind"] })} className="min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 text-sm"><option value="Table">{t.table}</option><option value="Parking">{t.parking}</option><option value="Branch">{t.branch}</option></select>
                <select value={form.channelId} onChange={(e) => setForm({ ...form, channelId: e.target.value })} className="min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 text-sm">{channels.map((c) => <option key={c.id} value={c.id}>{nameArEn(c, language)}</option>)}</select>
                <select value={form.approvalMode} onChange={(e) => setForm({ ...form, approvalMode: e.target.value as QrContextItem["approvalMode"] })} className="min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 text-sm"><option value="AutoApprove">{t.auto}</option><option value="RequiresStaffApproval">{t.manual}</option><option value="None">{t.none}</option></select>
                <button className="inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={16} />{t.create}</button>
              </form>
            </section>
          </aside>

          <section className="space-y-5">
            <div className="rounded-xl border border-[#dfe5df] bg-white">
              <div className="flex items-center justify-between border-b border-[#e8ece8] px-5 py-4"><h2 className="font-semibold">{t.contexts} ({contexts.length})</h2></div>
              {contexts.length === 0 ? <p className="p-8 text-center text-sm text-[#69766f]">{t.noContexts}</p> : (
                <ul className="divide-y divide-[#e8ece8]">{contexts.map((c) => (
                  <li key={c.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                    <div><p className="font-medium">{c.code} · {language === "ar" ? c.nameAr : c.nameEn}</p><p className="mt-1 text-sm text-[#69766f]">{kindLabel[c.kind]} · {modeLabel[c.approvalMode]} · {c.salesChannelNameAr ?? ""}</p></div>
                    <div className="flex items-center gap-2"><span className={`rounded-full px-3 py-1 text-xs font-semibold ${c.isActive ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#e8ece8] text-[#53615b]"}`}>{c.isActive ? t.active : t.inactive}</span><button onClick={() => void toggleContext(c.id)} className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#0e5a4f] px-3 text-xs font-semibold text-[#0e5a4f]"><Power size={14} />{t.toggle}</button></div>
                  </li>
                ))}</ul>
              )}
            </div>

            <div className="rounded-xl border border-[#dfe5df] bg-white">
              <div className="flex items-center justify-between border-b border-[#e8ece8] px-5 py-4"><h2 className="font-semibold">{t.pending} ({pending.length})</h2></div>
              {pending.length === 0 ? <p className="p-8 text-center text-sm text-[#69766f]">{t.noPending}</p> : (
                <ul className="divide-y divide-[#e8ece8]">{pending.map((o) => (
                  <li key={o.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                    <div className="min-w-0"><p className="text-sm font-medium">{o.clientRequestId.slice(0, 8)} · {fmt(o.grossAmount)} {language === "ar" ? "ر.ع" : "OMR"}</p><p className="mt-1 text-xs text-[#69766f]">{o.lines.map((l) => l.quantity + "× " + (language === "ar" ? l.productNameAr : l.productNameEn)).join(", ")}</p></div>
                    <div className="flex gap-2"><button onClick={() => void review(o.approval!.id, "approve")} className="inline-flex min-h-9 items-center gap-1.5 rounded-lg bg-[#0e5a4f] px-3 text-xs font-semibold text-white"><Check size={14} />{t.approve}</button><button onClick={() => void review(o.approval!.id, "reject")} className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-[#b4322a] px-3 text-xs font-semibold text-[#b4322a]"><X size={14} />{t.reject}</button></div>
                  </li>
                ))}</ul>
              )}
            </div>

            <div className="rounded-xl border border-[#dfe5df] bg-white">
              <div className="flex items-center justify-between border-b border-[#e8ece8] px-5 py-4"><h2 className="font-semibold">{t.orders} ({orders.length})</h2></div>
              {orders.length === 0 ? <p className="p-8 text-center text-sm text-[#69766f]">{t.noOrders}</p> : (
                <div className="divide-y divide-[#e8ece8]">{orders.slice(0, 50).map((o) => (
                  <div key={o.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                    <div><p className="text-sm font-medium">{o.clientRequestId.slice(0, 8)} · {fmt(o.grossAmount)} {language === "ar" ? "ر.ع" : "OMR"}</p><p className="mt-1 text-xs text-[#69766f]">{fmtDate(o.createdAt)} · {o.lines.length} items</p></div>
                    <div className="flex items-center gap-2"><span className="rounded-full bg-[#e6f1ec] px-3 py-1 text-xs font-semibold text-[#08483f]">{o.status}</span>{o.approval && <span className={`rounded-full px-3 py-1 text-xs font-semibold ${o.approval.status === "Approved" ? "bg-[#e3f4ea] text-[#137347]" : o.approval.status === "Rejected" ? "bg-[#fbe4e2] text-[#b4322a]" : "bg-[#f4f1e3] text-[#8a6d1f]"}`}>{o.approval.status}</span>}</div>
                  </div>
                ))}</div>
              )}
            </div>
          </section>
        </div>
      )}
    </div>
  );
}
