import { useEffect, useState } from "react";
import { Building2, FolderTree, Languages, Monitor, Package, Users, Menu, X, Home, ListChecks, BadgeDollarSign, ShoppingBag, Printer, ChefHat, Boxes, CloudOff, BarChart3, Truck, Scale, QrCode, Sparkles, Maximize2, Minimize2, History } from "lucide-react";

import { store } from "@/lib/local-store";
import { enterKiosk, exitKiosk } from "@/lib/fullscreen-kiosk";
import { SelectionGroupsSection } from "@/app/SelectionGroupsSection";
import { PricingSection } from "@/app/PricingSection";
import { PosSection } from "@/app/PosSection";
import { CancellationSection } from "@/app/CancellationSection";
import { ShiftsSection } from "@/app/ShiftsSection";
import { PrintingSection } from "@/app/PrintingSection";
import { KitchenSection } from "@/app/KitchenSection";
import { InventorySection } from "@/app/InventorySection";
import { AdvancedInventorySection } from "@/app/AdvancedInventorySection";
import { ProcurementSection } from "@/app/ProcurementSection";
import { SyncSection } from "@/app/SyncSection";
import { ReportsSection } from "@/app/ReportsSection";
import { QrAdminSection } from "@/app/QrAdminSection";
import { QrCustomerPage } from "@/app/QrCustomerPage";
import { IntegrationsSection } from "@/app/IntegrationsSection";
import { AdminSection } from "@/app/AdminSection";
import { OrderHistorySection } from "@/app/OrderHistorySection";

import { CatalogScreen } from "@/app/CatalogScreen";
type Language = "ar" | "en";
type View = "branches" | "devices" | "users" | "categories" | "products" | "selectionGroups" | "pricing" | "pos" | "cancellations" | "shifts" | "printing" | "kitchen" | "inventory" | "inventoryAdvanced" | "procurement" | "sync" | "reports" | "qr" | "integrations" | "orderHistory";
export function App() {
  const [language, setLanguage] = useState<Language>(() => store.get<Language>("language") === "en" ? "en" : "ar");
  const [token, setToken] = useState(() => store.get<string>("session-token") ?? "");
  const [checkingSession, setCheckingSession] = useState(() => Boolean(store.get<string>("session-token")));
  const [view, setView] = useState<View>("pos");
  const [menuOpen, setMenuOpen] = useState(false);
  const [kiosk, setKiosk] = useState(false);
  const [credentials, setCredentials] = useState({ username: "", password: "" });
  const [loginError, setLoginError] = useState("");
  const [loggingIn, setLoggingIn] = useState(false);
  const [hash, setHash] = useState(window.location.hash);
  const ar = language === "ar";
  const tr = (a: string, e: string) => ar ? a : e;
  const text = { sync: tr("المزامنة", "Sync"), branches: tr("الفروع", "Branches"), devices: tr("الأجهزة", "Devices"), users: tr("المستخدمون", "Users"), categories: tr("التصنيفات", "Categories"), products: tr("المنتجات", "Products"), selectionGroups: tr("الوجبات والإضافات", "Combos & modifiers") };
  const navigation: Array<[View, string, typeof Building2]> = [["pos", language === "ar" ? "نقطة البيع" : "POS", ShoppingBag], ["kitchen", language === "ar" ? "المطبخ" : "Kitchen", ChefHat], ["inventory", language === "ar" ? "المخزون والوصفات" : "Inventory & recipes", Boxes], ["inventoryAdvanced", language === "ar" ? "الجرد والهدر والتكلفة" : "Counts, waste & costing", Scale], ["procurement", language === "ar" ? "المشتريات والموردون" : "Procurement & suppliers", Truck], ["shifts", language === "ar" ? "الورديات والنقد" : "Shifts & cash", BadgeDollarSign], ["printing", language === "ar" ? "الطباعة والأجهزة" : "Printing & hardware", Printer], ["cancellations", language === "ar" ? "الإلغاء والاسترجاع" : "Cancellations", BadgeDollarSign], ["orderHistory", language === "ar" ? "سجل الطلبات" : "Order history", History], ["sync", text.sync, CloudOff], ["branches", text.branches, Building2], ["devices", text.devices, Monitor], ["users", text.users, Users], ["categories", text.categories, FolderTree], ["products", text.products, Package], ["selectionGroups", text.selectionGroups, ListChecks], ["pricing", language === "ar" ? "التسعير والضريبة" : "Pricing & tax", BadgeDollarSign], ["reports", language === "ar" ? "التقارير والتدقيق" : "Reports & audit", BarChart3], ["qr", language === "ar" ? "QR" : "QR", QrCode], ["integrations", language === "ar" ? "التكاملات والذكاء" : "Integrations & AI", Sparkles]];

  useEffect(() => { document.documentElement.lang = language; document.documentElement.dir = ar ? "rtl" : "ltr"; store.set("language", language); }, [language]);
  useEffect(() => { const update = () => setHash(window.location.hash); window.addEventListener("hashchange", update); return () => window.removeEventListener("hashchange", update); }, []);
  useEffect(() => { const target = hash.slice(2); if (navigation.some(([key]) => key === target)) setView(target as View); else if (!hash || hash === "#/") setView("pos"); }, [hash]);
  // While the mobile drawer is open, keep the page from scrolling behind it and close on Escape.
  useEffect(() => {
    if (!menuOpen) return;
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setMenuOpen(false); };
    window.addEventListener("keydown", onKey);
    return () => { document.body.style.overflow = previous; window.removeEventListener("keydown", onKey); };
  }, [menuOpen]);
  function navigate(next: View) { window.location.hash = "/" + next; setView(next); setMenuOpen(false); }
  useEffect(() => { if (view !== "pos" && kiosk) { exitKiosk(); setKiosk(false); } }, [view, kiosk]);
  useEffect(() => {
    if (!token) { setCheckingSession(false); return; }
    const controller = new AbortController();
    setCheckingSession(true);
    void fetch("/api/v1/pos/context", { headers: { Authorization: `Bearer ${token}` }, signal: controller.signal })
      .then((response) => {
        if (response.status !== 401) return;
        store.remove("session-token");
        setToken("");
      })
      .catch(() => undefined)
      .finally(() => { if (!controller.signal.aborted) setCheckingSession(false); });
    return () => controller.abort();
  }, [token]);
  async function login(event: React.FormEvent) {
    event.preventDefault(); setLoginError(""); setLoggingIn(true);
    try { const r = await fetch("/api/v1/auth/login", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(credentials) }); if (!r.ok) throw new Error(); const value = await r.json() as { token: string }; store.set("session-token", value.token); setToken(value.token); }
    catch { setLoginError(tr("تعذر الدخول. تحقق من اسم المستخدم وكلمة المرور والاتصال.", "Unable to sign in. Check your credentials and connection.")); } finally { setLoggingIn(false); }
  }
  if (hash.startsWith("#/qr/")) return <QrCustomerPage code={decodeURIComponent(hash.slice(5))} />;
  if (checkingSession) return <main className="grid min-h-screen place-items-center bg-[#f5f6f2]"><div className="size-8 animate-spin rounded-full border-4 border-[#cdd7d0] border-t-[#0e5a4f]" aria-label={tr("جارٍ التحقق من الجلسة", "Checking session")} /></main>;
  if (!token) return <main className="grid min-h-screen place-items-center bg-[#f5f6f2] p-4"><form onSubmit={login} className="w-full max-w-md space-y-5 rounded-2xl border bg-white p-8"><div className="flex justify-between"><strong>OFC</strong><button type="button" onClick={() => setLanguage(ar ? "en" : "ar")} className="min-h-11 px-3">{ar ? "English" : "العربية"}</button></div><h1 className="text-2xl font-bold">{tr("تسجيل الدخول", "Sign in")}</h1><p className="text-sm text-[#64716b]">{tr("الطلبات والمبيعات والمخزون في مكان واحد.", "Orders, sales and stock in one place.")}</p><label className="block text-sm">{tr("اسم المستخدم", "Username")}<input required autoComplete="username" value={credentials.username} onChange={e => setCredentials({ ...credentials, username: e.target.value })} className="mt-2 min-h-12 w-full rounded-lg border px-3" /></label><label className="block text-sm">{tr("كلمة المرور", "Password")}<input required type="password" autoComplete="current-password" value={credentials.password} onChange={e => setCredentials({ ...credentials, password: e.target.value })} className="mt-2 min-h-12 w-full rounded-lg border px-3" /></label>{loginError && <p role="alert">{loginError}</p>}<button disabled={loggingIn} className="min-h-12 w-full rounded-lg bg-[#0e5a4f] font-semibold text-white">{loggingIn ? "…" : tr("دخول", "Sign in")}</button></form></main>;
  const groups: Array<{ label: string; keys: View[] }> = [
    { label: tr("العمل اليومي", "Daily work"), keys: ["pos", "kitchen", "shifts", "cancellations", "qr", "orderHistory"] },
    { label: tr("المخزون والمتابعة", "Stock & insights"), keys: ["inventory", "inventoryAdvanced", "procurement", "reports"] },
    { label: tr("إعداد القائمة", "Menu setup"), keys: ["products", "categories", "selectionGroups", "pricing"] },
    { label: tr("الإدارة والإعدادات", "Administration"), keys: ["users", "branches", "devices", "printing", "sync", "integrations"] }
  ];
  const current = navigation.find(([key]) => key === view)?.[1];
  const menuContent = groups.map(group => (
    <div key={group.label} className="mb-4">
      <p className="px-3 py-2 text-xs font-semibold text-[#69766f]">{group.label}</p>
      {group.keys.map(key => { const entry = navigation.find(([k]) => k === key)!; const Icon = entry[2]; return <button key={key} aria-current={view === key ? "page" : undefined} onClick={() => navigate(key)} className={`flex min-h-11 w-full items-center gap-3 rounded-lg px-3 text-start text-sm ${view === key ? "bg-[#e6f1ec] font-semibold text-[#08483f]" : "text-[#53615b] hover:bg-[#f2f5f2]"}`}><Icon size={18} />{entry[1]}</button>; })}
    </div>
  ));
  return <div className="min-h-screen bg-[#f5f6f2] text-[#17211f]">
    <header className="flex min-h-16 items-center justify-between gap-2 border-b bg-white px-4"><div className="flex items-center gap-2"><button aria-controls="mobile-nav" className={`grid size-11 place-items-center rounded-lg border ${kiosk ? "" : "lg:hidden"}`} aria-label={tr("القائمة الرئيسية", "Main menu")} aria-expanded={menuOpen} onClick={() => setMenuOpen(!menuOpen)}>{!kiosk && menuOpen ? <X /> : <Menu />}</button>{!kiosk && <button onClick={() => navigate("pos")} className="min-h-11 font-bold text-[#0e5a4f]">OFC · {tr("إدارة المطعم", "Restaurant")}</button>}</div><div className="flex items-center gap-1">{view === "pos" && (kiosk ? <button aria-label={tr("خروج من وضع الأكشاك", "Exit kiosk mode")} title={tr("خروج من وضع الأكشاك", "Exit kiosk mode")} className="min-h-11 px-3" onClick={() => { exitKiosk(); setKiosk(false); }}><Minimize2 size={18} /></button> : <button aria-label={tr("وضع الأكشاك", "Kiosk mode")} title={tr("وضع الأكشاك", "Kiosk mode")} className="min-h-11 px-3" onClick={async () => { const success = await enterKiosk(); if (success) setKiosk(true); }}><Maximize2 size={18} /></button>)}<button aria-label={tr("تغيير اللغة", "Change language")} className="min-h-11 px-3" onClick={() => setLanguage(ar ? "en" : "ar")}><Languages size={18} /></button><button className="min-h-11 px-3 text-sm" onClick={() => { store.remove("session-token"); setToken(""); }}>{tr("خروج", "Sign out")}</button></div></header>
    {menuOpen && (
      <div className={`fixed inset-0 z-50 ${kiosk ? "" : "lg:hidden"}`}>
        <div className="fixed inset-0 bg-black/40" onClick={() => setMenuOpen(false)} aria-hidden="true" />
        <aside id="mobile-nav" role="dialog" aria-modal="true" aria-label={tr("القائمة الرئيسية", "Main menu")} className="drawer-in absolute inset-y-0 start-0 flex w-72 max-w-[85vw] flex-col bg-white p-3 shadow-2xl">
          <div className="mb-3 flex items-center justify-between gap-2 border-b border-[#e8ece8] pb-3">
            <button onClick={() => navigate("pos")} className="min-h-11 font-bold text-[#0e5a4f]">OFC · {tr("إدارة المطعم", "Restaurant")}</button>
            <button onClick={() => setMenuOpen(false)} aria-label={tr("إغلاق القائمة", "Close menu")} className="grid size-10 place-items-center rounded-lg border border-[#cdd7d0]"><X size={18} /></button>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto pb-4">{menuContent}</div>
        </aside>
      </div>
    )}
    <div className={`grid ${kiosk ? "w-full" : "mx-auto max-w-[1920px] lg:grid-cols-[210px_minmax(0,1fr)]"}`}>
      {!kiosk && <nav aria-label={tr("القائمة الرئيسية", "Main menu")} className="hidden border-e bg-white p-3 lg:sticky lg:top-0 lg:block lg:h-[calc(100dvh-64px)] lg:overflow-y-auto">
        {menuContent}
      </nav>}
      <main className={`min-w-0 ${kiosk ? "p-2 sm:p-3" : "p-4 sm:p-6"}`}>{view !== "pos" && <nav aria-label={tr("مسار التنقل", "Breadcrumb")} className="mb-5 flex flex-wrap items-center gap-2 text-sm text-[#64716b]"><button onClick={() => navigate("pos")} className="inline-flex min-h-9 items-center gap-1 text-[#0e5a4f]"><Home size={15} />{tr("الرئيسية", "Home")}</button><span aria-hidden="true">/</span><span>{groups.find(g => g.keys.includes(view))?.label}</span><span aria-hidden="true">/</span><span aria-current="page" className="font-medium text-[#17211f]">{current}</span></nav>}{view === "pos" ? <PosSection language={language} kiosk={kiosk} onKioskChange={setKiosk} /> : view === "kitchen" ? <KitchenSection language={language} /> : view === "inventory" ? <InventorySection language={language} /> : view === "inventoryAdvanced" ? <AdvancedInventorySection language={language} /> : view === "procurement" ? <ProcurementSection language={language} /> : view === "reports" ? <ReportsSection language={language} /> : view === "integrations" ? <IntegrationsSection language={language} /> : view === "sync" ? <SyncSection language={language} /> : view === "cancellations" ? <CancellationSection language={language} /> : view === "orderHistory" ? <OrderHistorySection language={language} /> : view === "categories" ? <CatalogScreen language={language} mode="categories" /> : view === "products" ? <CatalogScreen language={language} mode="products" /> : view === "selectionGroups" ? <SelectionGroupsSection language={language} /> : view === "shifts" ? <ShiftsSection language={language} /> : view === "printing" ? <PrintingSection language={language} /> : view === "pricing" ? <PricingSection language={language} /> : view === "qr" ? <QrAdminSection language={language} /> : <AdminSection language={language} view={view as "branches" | "devices" | "users"} />}</main>
    </div>
  </div>;
}
