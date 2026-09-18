import { Moon, Sun, X } from "lucide-react";
import { useState } from "react";
import type { ThemeMode } from "@/app/LoginScreen";
import { Button } from "@/components/ui/button";

type Props = {
  language: "ar" | "en";
  theme: ThemeMode;
  onThemeChange: (theme: ThemeMode) => void;
};

export function ThemeControls({ language, theme, onThemeChange }: Props) {
  const [open, setOpen] = useState(false);
  const ar = language === "ar";
  const tr = (a: string, e: string) => ar ? a : e;

  return (
    <div className="theme-navbar-control">
      {open && <div className="theme-popover" role="dialog" aria-label={tr("تخصيص المظهر", "Customize appearance")}>
        <div className="theme-popover-title"><span>{tr("المظهر", "Appearance")}</span><Button onClick={() => setOpen(false)} aria-label={tr("إغلاق", "Close")}><X size={17} /></Button></div>
        <p>{tr("الوضع", "Mode")}</p>
        <div className="theme-mode-options">
          <Button className={theme === "light" ? "active" : ""} onClick={() => onThemeChange("light")}><Sun size={17} />{tr("نهاري", "Light")}</Button>
          <Button className={theme === "dark" ? "active" : ""} onClick={() => onThemeChange("dark")}><Moon size={17} />{tr("ليلي", "Dark")}</Button>
        </div>
      </div>}
      <Button className="theme-fab" onClick={() => setOpen(!open)} aria-expanded={open} aria-label={tr("تغيير المظهر", "Change appearance")}>{theme === "dark" ? <Moon size={19} /> : <Sun size={19} />}</Button>
    </div>
  );
}
