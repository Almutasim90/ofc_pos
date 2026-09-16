import { Eye, EyeOff, Moon, ShieldCheck, Sparkles, Sun } from "lucide-react";
import { useState } from "react";

export type ThemeMode = "light" | "dark";
export type Accent = "sky" | "teal" | "violet";

type LoginScreenProps = {
  language: "ar" | "en";
  credentials: { username: string; password: string };
  error: string;
  loggingIn: boolean;
  theme: ThemeMode;
  accent: Accent;
  onLanguageChange: () => void;
  onCredentialsChange: (credentials: { username: string; password: string }) => void;
  onThemeChange: (theme: ThemeMode) => void;
  onAccentChange: (accent: Accent) => void;
  onSubmit: (event: React.FormEvent) => void;
};

const accents: Array<{ value: Accent; light: string; dark: string }> = [
  { value: "sky", light: "#38bdf8", dark: "#0284c7" },
  { value: "teal", light: "#2dd4bf", dark: "#0f766e" },
  { value: "violet", light: "#a78bfa", dark: "#7c3aed" },
];

export function LoginScreen({ language, credentials, error, loggingIn, theme, accent, onLanguageChange, onCredentialsChange, onThemeChange, onAccentChange, onSubmit }: LoginScreenProps) {
  const [showPassword, setShowPassword] = useState(false);
  const ar = language === "ar";
  const tr = (arabic: string, english: string) => ar ? arabic : english;

  return (
    <main className="login-shell">
      <div className="login-orb login-orb-one" />
      <div className="login-orb login-orb-two" />
      <section className="login-card" aria-labelledby="login-title">
        <aside className="login-showcase">
          <div className="login-brand"><span className="login-brand-mark">O</span><span>OFC</span></div>
          <div className="login-showcase-copy">
            <span className="login-kicker"><Sparkles size={16} />{tr("إدارة أذكى. عمل أسرع.", "Smarter management. Faster service.")}</span>
            <h2>{tr("كل عمليات مطعمك في مكان واحد.", "Your restaurant, all in one place.")}</h2>
            <p>{tr("تابع الطلبات والمبيعات والمخزون بسلاسة من منصة واحدة مصممة لفريقك.", "Run orders, sales, and inventory smoothly from one workspace built for your team.")}</p>
          </div>
          <div className="login-trust"><ShieldCheck size={19} /><span>{tr("دخول آمن ومحمي", "Secure, protected access")}</span></div>
        </aside>

        <div className="login-form-panel">
          <div className="login-toolbar">
            <div className="login-accents" aria-label={tr("اختيار اللون", "Choose color")}>
              {accents.map((item) => <button key={item.value} type="button" aria-label={item.value} aria-pressed={accent === item.value} onClick={() => onAccentChange(item.value)} style={{ background: `linear-gradient(135deg, ${item.light}, ${item.dark})` }} />)}
            </div>
            <button type="button" className="login-icon-button" onClick={() => onThemeChange(theme === "dark" ? "light" : "dark")} aria-label={theme === "dark" ? tr("الوضع النهاري", "Light mode") : tr("الوضع الليلي", "Dark mode")}>
              {theme === "dark" ? <Sun size={19} /> : <Moon size={19} />}
            </button>
            <button type="button" className="login-language" onClick={onLanguageChange}>{ar ? "EN" : "عربي"}</button>
          </div>

          <form onSubmit={onSubmit} className="login-form">
            <div className="login-heading">
              <span className="login-mobile-brand">OFC</span>
              <h1 id="login-title">{tr("مرحبًا بعودتك", "Welcome back")}</h1>
              <p>{tr("سجّل الدخول للمتابعة إلى لوحة التحكم", "Sign in to continue to your dashboard")}</p>
            </div>

            <label className="login-field">
              <span>{tr("اسم المستخدم", "Username")}</span>
              <input required autoFocus autoComplete="username" value={credentials.username} onChange={(event) => onCredentialsChange({ ...credentials, username: event.target.value })} placeholder={tr("أدخل اسم المستخدم", "Enter your username")} />
            </label>

            <label className="login-field">
              <span>{tr("كلمة المرور", "Password")}</span>
              <span className="login-password">
                <input required type={showPassword ? "text" : "password"} autoComplete="current-password" value={credentials.password} onChange={(event) => onCredentialsChange({ ...credentials, password: event.target.value })} placeholder={tr("أدخل كلمة المرور", "Enter your password")} />
                <button type="button" onClick={() => setShowPassword(!showPassword)} aria-label={showPassword ? tr("إخفاء كلمة المرور", "Hide password") : tr("إظهار كلمة المرور", "Show password")} aria-pressed={showPassword}>
                  {showPassword ? <EyeOff size={19} /> : <Eye size={19} />}
                </button>
              </span>
            </label>

            {error && <p className="login-error" role="alert">{error}</p>}
            <button disabled={loggingIn} className="login-submit">
              {loggingIn ? <span className="login-spinner" /> : tr("تسجيل الدخول", "Sign in")}
            </button>
          </form>
          <p className="login-footer">{tr("منصة OFC لإدارة المطاعم", "OFC restaurant management platform")}</p>
        </div>
      </section>
    </main>
  );
}
