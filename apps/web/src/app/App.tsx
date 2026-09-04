import { useEffect, useState } from "react";
import { Activity, CloudOff, Languages, RefreshCw, Wifi } from "lucide-react";

import { store } from "@/lib/local-store";

type Language = "ar" | "en";
type HealthStatus = "loading" | "online" | "offline" | "error";

const copy = {
  ar: {
    brand: "منصة OFC",
    eyebrow: "أساسيات التشغيل",
    title: "مساحة عمل واضحة، تبدأ متصلة.",
    description: "هذه الواجهة جاهزة لرحلة فريقك. ستظهر الأدوات والعمليات هنا مع استمرار بناء المنصة.",
    health: "حالة الخدمة",
    loading: "جارٍ التحقق من الاتصال",
    online: "الخدمة متصلة وجاهزة",
    offline: "أنت غير متصل بالإنترنت",
    error: "تعذر الوصول إلى الخدمة",
    retry: "إعادة المحاولة",
    workspace: "مساحة العمل",
    comingSoon: "ستتوفر وحدات العمل هنا قريباً.",
    language: "English",
  },
  en: {
    brand: "OFC Platform",
    eyebrow: "Operations foundation",
    title: "A clear workspace, connected from the start.",
    description: "This interface is ready for your team's journey. Tools and workflows will appear here as the platform grows.",
    health: "Service status",
    loading: "Checking connection",
    online: "Service is connected and ready",
    offline: "You are offline",
    error: "Unable to reach the service",
    retry: "Try again",
    workspace: "Workspace",
    comingSoon: "Work modules will be available here soon.",
    language: "العربية",
  },
} as const;

function initialLanguage(): Language {
  const saved = store.get<Language>("language");
  return saved === "en" || saved === "ar" ? saved : "ar";
}

export function App() {
  const [language, setLanguage] = useState<Language>(initialLanguage);
  const [health, setHealth] = useState<HealthStatus>("loading");
  const text = copy[language];

  useEffect(() => {
    document.documentElement.lang = language;
    document.documentElement.dir = language === "ar" ? "rtl" : "ltr";
    document.title = text.brand;
    store.set("language", language);
  }, [language, text.brand]);

  async function checkHealth() {
    if (!navigator.onLine) {
      setHealth("offline");
      return;
    }

    setHealth("loading");
    try {
      const response = await fetch("/health", { cache: "no-store" });
      setHealth(response.ok ? "online" : "error");
    } catch {
      setHealth(navigator.onLine ? "error" : "offline");
    }
  }

  useEffect(() => {
    void checkHealth();
    const markOffline = () => setHealth("offline");
    const reconnect = () => void checkHealth();
    window.addEventListener("offline", markOffline);
    window.addEventListener("online", reconnect);
    return () => {
      window.removeEventListener("offline", markOffline);
      window.removeEventListener("online", reconnect);
    };
  }, []);

  const isAvailable = health === "online";
  const isRetryable = health === "offline" || health === "error";
  const StatusIcon = isAvailable ? Wifi : health === "offline" ? CloudOff : Activity;
  const statusText = health === "loading" ? text.loading : health === "online" ? text.online : health === "offline" ? text.offline : text.error;

  return (
    <main className="min-h-screen bg-[#f7f7f5] px-4 py-4 sm:px-8 sm:py-8">
      <div className="mx-auto flex min-h-[calc(100vh-2rem)] max-w-6xl flex-col rounded-3xl border border-[#dce2dc] bg-white shadow-[0_24px_80px_-38px_rgba(18,44,37,0.35)] sm:min-h-[calc(100vh-4rem)]">
        <header className="flex items-center justify-between border-b border-[#e8ece8] px-5 py-4 sm:px-8">
          <div className="flex items-center gap-3">
            <span className="grid size-10 place-items-center rounded-xl bg-[#0e5a4f] text-sm font-bold text-white">O</span>
            <span className="font-semibold tracking-tight">{text.brand}</span>
          </div>
          <button
            type="button"
            onClick={() => setLanguage(language === "ar" ? "en" : "ar")}
            className="inline-flex items-center gap-2 rounded-lg border border-[#d7dfd9] px-3 py-2 text-sm font-medium transition hover:border-[#0e5a4f] hover:text-[#0e5a4f] focus:outline-none focus:ring-2 focus:ring-[#0e5a4f]/30"
          >
            <Languages size={17} aria-hidden="true" />
            {text.language}
          </button>
        </header>

        <section className="flex flex-1 items-center px-5 py-14 sm:px-12 lg:px-20">
          <div className="grid w-full gap-12 lg:grid-cols-[1.3fr_0.7fr] lg:items-center">
            <div className="max-w-2xl">
              <p className="mb-5 text-sm font-semibold tracking-wide text-[#0e5a4f]">{text.eyebrow}</p>
              <h1 className="text-4xl font-semibold leading-tight tracking-tight text-[#17211f] sm:text-6xl">{text.title}</h1>
              <p className="mt-6 max-w-xl text-base leading-8 text-[#5b6863] sm:text-lg">{text.description}</p>
            </div>

            <aside className="rounded-2xl border border-[#e0e7e1] bg-[#fbfcfa] p-6">
              <p className="text-sm font-semibold text-[#35443e]">{text.health}</p>
              <div className="mt-5 flex items-start gap-3">
                <span className={`grid size-10 shrink-0 place-items-center rounded-full ${isAvailable ? "bg-[#dff4e9] text-[#137347]" : health === "loading" ? "bg-[#fff3d6] text-[#a26000]" : "bg-[#fbe4e2] text-[#b4322a]"}`}>
                  <StatusIcon className={health === "loading" ? "animate-pulse" : ""} size={19} aria-hidden="true" />
                </span>
                <div>
                  <p className="font-medium text-[#17211f]" aria-live="polite">{statusText}</p>
                  {isRetryable && (
                    <button type="button" onClick={() => void checkHealth()} className="mt-3 inline-flex items-center gap-2 text-sm font-semibold text-[#0e5a4f] hover:underline">
                      <RefreshCw size={15} aria-hidden="true" />
                      {text.retry}
                    </button>
                  )}
                </div>
              </div>
            </aside>
          </div>
        </section>

        <footer className="border-t border-[#e8ece8] px-5 py-4 text-sm text-[#73807a] sm:px-8">
          <span className="font-medium text-[#35443e]">{text.workspace}</span>
          <span className="px-2" aria-hidden="true">/</span>
          {text.comingSoon}
        </footer>
      </div>
    </main>
  );
}
