import { Fragment, useMemo, useState } from "react";
import { Search } from "lucide-react";

type Language = "ar" | "en";
type Permission = { id: string; code: string };
export type PermissionOverride = "inherit" | "grant" | "revoke";

const copy = {
  ar: { search: "بحث عن صلاحية...", code: "الصلاحية", fromRole: "من الدور", override: "التخصيص", inherit: "تلقائي (الدور)", grant: "منح", revoke: "سحب", effective: "النتيجة", granted: "ممنوحة", denied: "مرفوضة", included: "مضمّنة", empty: "لا توجد صلاحيات مطابقة", count: "صلاحية" },
  en: { search: "Search permissions...", code: "Permission", fromRole: "From role", override: "Override", inherit: "Inherit (role)", grant: "Grant", revoke: "Revoke", effective: "Effective", granted: "Granted", denied: "Denied", included: "Included", empty: "No matching permissions", count: "permissions" },
} as const;

export function PermissionGrid({ language, permissions, mode, states, roleDefault, onChange }: {
  language: Language;
  permissions: Permission[];
  mode: "user" | "role";
  states: Record<string, PermissionOverride>;
  roleDefault: Set<string>;
  onChange: (code: string, state: PermissionOverride) => void;
}) {
  const t = copy[language];
  const [filter, setFilter] = useState("");

  const groups = useMemo(() => {
    const map = new Map<string, Permission[]>();
    for (const p of permissions) {
      if (filter && !p.code.toLowerCase().includes(filter.toLowerCase())) continue;
      const module = p.code.split(".")[0];
      if (!map.has(module)) map.set(module, []);
      map.get(module)!.push(p);
    }
    return [...map.entries()].sort((a, b) => a[0].localeCompare(b[0]));
  }, [permissions, filter]);

  const isGranted = (p: Permission) => mode === "role" ? states[p.code] === "grant" : (states[p.code] === "grant" || (states[p.code] !== "revoke" && roleDefault.has(p.code)));

  const shown = permissions.filter((p) => !filter || p.code.toLowerCase().includes(filter.toLowerCase()));

  return (
    <div className="overflow-hidden rounded-xl border border-[#dfe5df] bg-white">
      <div className="flex flex-wrap items-center justify-between gap-3 border-b border-[#e8ece8] px-4 py-3">
        <p className="text-sm font-semibold">{shown.length} {t.count}</p>
        <label className="flex min-h-10 items-center gap-2 rounded-lg border border-[#cdd7d0] px-3"><Search size={15} className="text-[#69766f]" /><input value={filter} onChange={(e) => setFilter(e.target.value)} placeholder={t.search} className="min-w-[160px] bg-transparent text-sm outline-none" /></label>
      </div>
      <div className="max-h-[50vh] overflow-y-auto">
        <table className="w-full text-sm">
          <thead className="sticky top-0 bg-white">
            <tr className="text-[#69766f]">
              <th className="px-4 py-2 text-start text-xs font-semibold">{t.code}</th>
              {mode === "user" && <th className="px-4 py-2 text-center text-xs font-semibold">{t.fromRole}</th>}
              <th className="px-4 py-2 text-start text-xs font-semibold">{mode === "role" ? t.included : t.override}</th>
              {mode === "user" && <th className="px-4 py-2 text-center text-xs font-semibold">{t.effective}</th>}
            </tr>
          </thead>
          <tbody>
            {groups.map(([module, rows]) => (
              <Fragment key={module}>
                <tr className="bg-[#f4f7f4]"><td colSpan={mode === "user" ? 4 : 2} className="px-4 py-1.5 text-xs font-semibold uppercase text-[#0e5a4f]">{module}</td></tr>
                {rows.map((p) => {
                  const granted = isGranted(p);
                  const override = states[p.code] ?? "inherit";
                  return (
                    <tr key={p.id} className="border-t border-[#eef1ee]">
                      <td className="px-4 py-2 font-mono text-xs text-[#33413b]">{p.code}</td>
                      {mode === "user" && <td className="px-4 py-2 text-center">{roleDefault.has(p.code) ? <span className="rounded-full bg-[#e6f1ec] px-2 py-0.5 text-xs font-semibold text-[#08483f]">✓</span> : <span className="text-[#c0c9c3]">—</span>}</td>}
                      <td className="px-4 py-2">
                        {mode === "role" ? (
                          <label className="inline-flex min-h-8 items-center gap-2"><input type="checkbox" checked={override === "grant"} onChange={(e) => onChange(p.code, e.target.checked ? "grant" : "inherit")} className="size-4 accent-[#0e5a4f]" /></label>
                        ) : (
                          <select value={override} onChange={(e) => onChange(p.code, e.target.value as PermissionOverride)} className="min-h-9 rounded-lg border border-[#cdd7d0] bg-white px-2 text-xs outline-none focus:border-[#0e5a4f]">
                            <option value="inherit">{t.inherit}</option>
                            <option value="grant">{t.grant}</option>
                            <option value="revoke">{t.revoke}</option>
                          </select>
                        )}
                      </td>
                      {mode === "user" && <td className="px-4 py-2 text-center">{granted ? <span className="rounded-full bg-[#e3f4ea] px-2.5 py-0.5 text-xs font-semibold text-[#137347]">{t.granted}</span> : <span className="rounded-full bg-[#fbe4e2] px-2.5 py-0.5 text-xs font-semibold text-[#b4322a]">{t.denied}</span>}</td>}
                    </tr>
                  );
                })}
              </Fragment>
            ))}
            {shown.length === 0 && <tr><td colSpan={mode === "user" ? 4 : 2} className="px-4 py-8 text-center text-sm text-[#69766f]">{t.empty}</td></tr>}
          </tbody>
        </table>
      </div>
    </div>
  );
}
