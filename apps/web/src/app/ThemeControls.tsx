import { Moon, Palette, Sun, X } from "lucide-react";
import { useState } from "react";
import type { Accent, ThemeMode } from "@/app/LoginScreen";

type Props = {
  language: "ar" | "en";
  theme: ThemeMode;
  accent: Accent;
  onThemeChange: (theme: ThemeMode) => void;
  onAccentChange: (accent: Accent) => void;
};

const colors: Array<{ value: Accent; color: string }> = [
  { value: "sky", color: "linear-gradient(135deg,#38bdf8,#0284c7)" },
  { value: "teal", color: "linear-gradient(135deg,#2dd4bf,#0f766e)" },
  { value: "violet", color: "linear-gradient(135deg,#a78bfa,#7c3aed)" },
];

export function ThemeControls({ language, theme, accent, onThemeChange, onAccentChange }: Props) {
  const [open, setOpen] = useState(false);
  const ar = language === "ar";
  const tr = (a: string, e: string) => ar ? a : e;

  return (
    <div className="theme-dock">
      {open && <div className="theme-popover" role="dialog" aria-label={tr("تخصيص المظهر", "Customize appearance")}>
        <div className="theme-popover-title"><span>{tr("المظهر", "Appearance")}</span><button onClick={() => setOpen(false)} aria-label={tr("إغلاق", "Close")}><X size={17} /></button></div>
        <p>{tr("الوضع", "Mode")}</p>
        <div className="theme-mode-options">
          <button className={theme === "light" ? "active" : ""} onClick={() => onThemeChange("light")}><Sun size={17} />{tr("نهاري", "Light")}</button>
          <button className={theme === "dark" ? "active" : ""} onClick={() => onThemeChange("dark")}><Moon size={17} />{tr("ليلي", "Dark")}</button>
        </div>
        <p>{tr("اللون الرئيسي", "Accent color")}</p>
        <div className="theme-color-options">
          {colors.map((item) => <button key={item.value} aria-label={item.value} aria-pressed={accent === item.value} onClick={() => onAccentChange(item.value)} style={{ background: item.color }} />)}
        </div>
      </div>}
      <button className="theme-fab" onClick={() => setOpen(!open)} aria-expanded={open} aria-label={tr("تخصيص المظهر", "Customize appearance")}><Palette size={21} /></button>
    </div>
  );
}
