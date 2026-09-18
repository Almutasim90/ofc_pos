import { Fragment, useMemo, useState } from "react";
import { Search } from "lucide-react";
import { SearchableSelect } from "@/app/SearchableSelect";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Input } from "@/components/ui/input";

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
      <div className="grid gap-3 border-b border-[#e8ece8] px-3 py-3 sm:flex sm:items-center sm:justify-between sm:px-4">
        <p className="text-sm font-semibold">{shown.length} {t.count}</p>
        <label className="flex min-h-11 min-w-0 items-center gap-2 rounded-lg border border-[#cdd7d0] px-3"><Search size={15} className="shrink-0 text-[#000000]" /><Input value={filter} onChange={(e) => setFilter(e.target.value)} placeholder={t.search} className="min-w-0 flex-1 bg-transparent text-sm outline-none" /></label>
      </div>
      <div className="max-h-[50vh] overflow-y-auto md:hidden">
        {groups.map(([module, rows]) => <section key={module}>
          <h4 className="sticky top-0 z-10 border-y border-[#e8ece8] bg-[#f4f7f4] px-3 py-2 text-xs font-semibold uppercase text-[#0e5a4f]">{module}</h4>
          <div className="divide-y divide-[#eef1ee] px-3">{rows.map((p) => {
            const granted = isGranted(p);
            const override = states[p.code] ?? "inherit";
            return <article key={p.id} className="py-3">
              <p className="break-all font-mono text-xs font-semibold text-[#33413b]">{p.code}</p>
              {mode === "user" ? <>
                <div className="mt-3"><SearchableSelect label={t.override} value={override} onChange={(v) => onChange(p.code, v as PermissionOverride)}><option value="inherit">{t.inherit}</option><option value="grant">{t.grant}</option><option value="revoke">{t.revoke}</option></SearchableSelect></div>
                <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-xs"><span className="text-[#000000]">{t.fromRole}: {roleDefault.has(p.code) ? t.included : "—"}</span><span className={`rounded-full px-2.5 py-1 font-semibold ${granted ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#fbe4e2] text-[#b4322a]"}`}>{granted ? t.granted : t.denied}</span></div>
              </> : <label className="mt-3 flex min-h-10 items-center gap-2 text-sm"><Input type="checkbox" checked={override === "grant"} onChange={(e) => onChange(p.code, e.target.checked ? "grant" : "inherit")} className="size-4 accent-[#0e5a4f]" />{t.included}</label>}
            </article>;
          })}</div>
        </section>)}
        {shown.length === 0 && <p className="px-4 py-8 text-center text-sm text-[#000000]">{t.empty}</p>}
      </div>
      <div className="hidden max-h-[50vh] overflow-y-auto md:block">
        <Table className="w-full text-sm">
          <TableHeader className="sticky top-0 bg-white">
            <TableRow className="text-[#000000]">
              <TableHead className="px-4 py-2 text-start text-xs font-semibold">{t.code}</TableHead>
              {mode === "user" && <TableHead className="px-4 py-2 text-center text-xs font-semibold">{t.fromRole}</TableHead>}
              <TableHead className="px-4 py-2 text-start text-xs font-semibold">{mode === "role" ? t.included : t.override}</TableHead>
              {mode === "user" && <TableHead className="px-4 py-2 text-center text-xs font-semibold">{t.effective}</TableHead>}
            </TableRow>
          </TableHeader>
          <TableBody>
            {groups.map(([module, rows]) => (
              <Fragment key={module}>
                <TableRow className="bg-[#f4f7f4]"><TableCell colSpan={mode === "user" ? 4 : 2} className="px-4 py-1.5 text-xs font-semibold uppercase text-[#0e5a4f]">{module}</TableCell></TableRow>
                {rows.map((p) => {
                  const granted = isGranted(p);
                  const override = states[p.code] ?? "inherit";
                  return (
                    <TableRow key={p.id} className="border-t border-[#eef1ee]">
                      <TableCell className="px-4 py-2 font-mono text-xs text-[#33413b]">{p.code}</TableCell>
                      {mode === "user" && <TableCell className="px-4 py-2 text-center">{roleDefault.has(p.code) ? <span className="rounded-full bg-[#e6f1ec] px-2 py-0.5 text-xs font-semibold text-[#08483f]">✓</span> : <span className="text-[#c0c9c3]">—</span>}</TableCell>}
                      <TableCell className="px-4 py-2">
                        {mode === "role" ? (
                          <label className="inline-flex min-h-8 items-center gap-2"><Input type="checkbox" checked={override === "grant"} onChange={(e) => onChange(p.code, e.target.checked ? "grant" : "inherit")} className="size-4 accent-[#0e5a4f]" /></label>
                        ) : (
                          <SearchableSelect label={`${t.override} · ${p.code}`} hideLabel value={override} onChange={(v) => onChange(p.code, v as PermissionOverride)}>
                            <option value="inherit">{t.inherit}</option>
                            <option value="grant">{t.grant}</option>
                            <option value="revoke">{t.revoke}</option>
                          </SearchableSelect>
                        )}
                      </TableCell>
                      {mode === "user" && <TableCell className="px-4 py-2 text-center">{granted ? <span className="rounded-full bg-[#e3f4ea] px-2.5 py-0.5 text-xs font-semibold text-[#137347]">{t.granted}</span> : <span className="rounded-full bg-[#fbe4e2] px-2.5 py-0.5 text-xs font-semibold text-[#b4322a]">{t.denied}</span>}</TableCell>}
                    </TableRow>
                  );
                })}
              </Fragment>
            ))}
            {shown.length === 0 && <TableRow><TableCell colSpan={mode === "user" ? 4 : 2} className="px-4 py-8 text-center text-sm text-[#000000]">{t.empty}</TableCell></TableRow>}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
