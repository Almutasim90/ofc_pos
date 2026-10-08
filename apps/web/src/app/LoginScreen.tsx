import { Eye, EyeOff, Moon, Sun } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

import type { ThemeMode } from "@/lib/theme";
export type { ThemeMode } from "@/lib/theme";
export type Accent =
  "sky" | "teal" | "violet" | "orange" | "tomato" | "gold" | "olive";

type LoginScreenProps = {
  language: "ar" | "en";
  credentials: { username: string; password: string };
  error: string;
  loggingIn: boolean;
  theme: ThemeMode;
  onLanguageChange: () => void;
  onCredentialsChange: (credentials: {
    username: string;
    password: string;
  }) => void;
  onThemeChange: (theme: ThemeMode) => void;
  onSubmit: (event: React.FormEvent) => void;
};

export function LoginScreen({
  language,
  credentials,
  error,
  loggingIn,
  theme,
  onLanguageChange,
  onCredentialsChange,
  onThemeChange,
  onSubmit,
}: LoginScreenProps) {
  const [showPassword, setShowPassword] = useState(false);
  const ar = language === "ar";
  const tr = (arabic: string, english: string) => (ar ? arabic : english);

  return (
    <main className="login-shell">
      <section className="login-card" aria-labelledby="login-title">
        <div className="login-form-panel">
          <div className="login-toolbar">
            <Button
              type="button"
              className="login-icon-button"
              onClick={() => onThemeChange(theme === "dark" ? "light" : "dark")}
              aria-label={
                theme === "dark"
                  ? tr("الوضع النهاري", "Light mode")
                  : tr("الوضع الليلي", "Dark mode")
              }
            >
              {theme === "dark" ? <Sun size={19} /> : <Moon size={19} />}
            </Button>
            <Button
              type="button"
              className="login-language"
              onClick={onLanguageChange}
            >
              {ar ? "EN" : "عربي"}
            </Button>
          </div>

          <form onSubmit={onSubmit} className="login-form">
            <div className="login-heading">
              <span className="login-mobile-brand">OFC</span>
              <h1 id="login-title">{tr("مرحبًا بعودتك", "Welcome back")}</h1>
              <p>
                {tr(
                  "سجّل الدخول للمتابعة إلى لوحة التحكم",
                  "Sign in to continue to your dashboard",
                )}
              </p>
            </div>

            <label className="login-field">
              <span>{tr("اسم المستخدم", "Username")}</span>
              <Input
                required
                autoFocus
                autoComplete="username"
                value={credentials.username}
                onChange={(event) =>
                  onCredentialsChange({
                    ...credentials,
                    username: event.target.value,
                  })
                }
                placeholder={tr("أدخل اسم المستخدم", "Enter your username")}
              />
            </label>

            <label className="login-field">
              <span>{tr("كلمة المرور", "Password")}</span>
              <span className="login-password">
                <Input
                  required
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  value={credentials.password}
                  onChange={(event) =>
                    onCredentialsChange({
                      ...credentials,
                      password: event.target.value,
                    })
                  }
                  placeholder={tr("أدخل كلمة المرور", "Enter your password")}
                />
                <Button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={
                    showPassword
                      ? tr("إخفاء كلمة المرور", "Hide password")
                      : tr("إظهار كلمة المرور", "Show password")
                  }
                  aria-pressed={showPassword}
                >
                  {showPassword ? <EyeOff size={19} /> : <Eye size={19} />}
                </Button>
              </span>
            </label>

            {error && (
              <p className="login-error" role="alert">
                {error}
              </p>
            )}
            <Button disabled={loggingIn} className="login-submit">
              {loggingIn ? (
                <span className="login-spinner" />
              ) : (
                tr("تسجيل الدخول", "Sign in")
              )}
            </Button>
          </form>
          <p className="login-footer">
            {tr(
              "منصة OFC لإدارة المطاعم",
              "OFC restaurant management platform",
            )}
          </p>
        </div>
      </section>
    </main>
  );
}
