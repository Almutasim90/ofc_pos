import { useEffect, useMemo, useState } from "react";
import { X } from "lucide-react";
import { store } from "@/lib/local-store";
import { PermissionGrid, type PermissionOverride } from "@/app/PermissionGrid";

type Language = "ar" | "en";
type Role = { id: string; name: string; permissions: string[] };
type Permission = { id: string; code: string };
type Branch = { id: string; nameAr: string; nameEn: string };
type User = { id: string; username: string; email: string | null; displayName: string; isActive: boolean; roles: string[]; branchIds: string[] };

const copy = {
  ar: { create: "مستخدم جديد", edit: "تعديل المستخدم", account: "بيانات الحساب", username: "اسم المستخدم *", email: "البريد الإلكتروني (اختياري)", displayName: "الاسم الظاهر *", password: "كلمة المرور *", newPassword: "كلمة مرور جديدة (اتركها فارغة للإبقاء)", roles: "الأدوار", branches: "الفروع المخصصة *", active: "الحساب نشط", permissions: "الصلاحيات (تخصيص إضافي على الدور)", save: "حفظ", cancel: "إلغاء", saving: "جارٍ الحفظ...", error: "تعذر الحفظ. تحقق من اسم المستخدم والبريد والفروع.", selfNote: "سيحتاج المستخدم إلى تسجيل الدخول مجددًا بعد التعديل.", noRoles: "لا توجد أدوار. أنشئ دورًا أولًا.", noBranches: "لا توجد فروع.", roleHint: "اختر دورًا لمنح المستخدم صلاحياته الافتراضية، ثم يمكنك منح أو سحب صلاحيات إضافية أدناه." },
  en: { create: "New user", edit: "Edit user", account: "Account details", username: "Username *", email: "Email (optional)", displayName: "Display name *", password: "Password *", newPassword: "New password (leave blank to keep)", roles: "Roles", branches: "Assigned branches *", active: "Account active", permissions: "Permissions (extra overrides on top of the role)", save: "Save", cancel: "Cancel", saving: "Saving...", error: "Unable to save. Check the username, email and branches.", selfNote: "The user will need to sign in again after these changes.", noRoles: "No roles yet. Create a role first.", noBranches: "No branches.", roleHint: "Choose a role to give the user its default permissions, then grant or revoke extra permissions below." },
} as const;

export function UserForm({ language, user, roles, permissions, branches, auth, onClose, onSaved }: { language: Language; user: User | null; roles: Role[]; permissions: Permission[]; branches: Branch[]; auth: (p: string, i?: RequestInit) => Promise<Response>; onClose: () => void; onSaved: () => void }) {
  const t = copy[language];
  const ar = language === "ar";
  const [form, setForm] = useState({ username: user?.username ?? "", email: user?.email ?? "", displayName: user?.displayName ?? "", password: "", isActive: user?.isActive ?? true });
  const [roleIds, setRoleIds] = useState<string[]>(user ? roles.filter((r) => user.roles.includes(r.name)).map((r) => r.id) : []);
  const [branchIds, setBranchIds] = useState<string[]>(user?.branchIds ?? []);
  const [states, setStates] = useState<Record<string, PermissionOverride>>({});
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    if (!user) return;
    void (async () => {
      const response = await auth(`/api/v1/users/${user.id}/permissions`);
      if (!response.ok) return;
      const value = await response.json() as { permissions: Array<{ code: string; source: string }> };
      const next: Record<string, PermissionOverride> = {};
      for (const row of value.permissions) if (row.source === "granted") next[row.code] = "grant"; else if (row.source === "revoked") next[row.code] = "revoke";
      setStates(next);
    })();
  }, [user]);

  const roleDefault = useMemo(() => new Set(roles.filter((r) => roleIds.includes(r.id)).flatMap((r) => r.permissions)), [roles, roleIds]);
  const name = (b: Branch) => (ar ? b.nameAr : b.nameEn);

  function toggleRole(id: string, checked: boolean) { setRoleIds(checked ? [...roleIds, id] : roleIds.filter((x) => x !== id)); }
  function toggleBranch(id: string, checked: boolean) { setBranchIds(checked ? [...branchIds, id] : branchIds.filter((x) => x !== id)); }
  function setOverride(code: string, state: PermissionOverride) { setStates((prev) => { const next = { ...prev }; if (state === "inherit") delete next[code]; else next[code] = state; return next; }); }

  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (saving) return;
    setSaving(true);
    setError("");
    const permissionsPayload = Object.entries(states).map(([code, state]) => ({ permissionCode: code, isGrant: state === "grant" }));
    try {
      if (user) {
        const r2 = await auth(`/api/v1/users/${user.id}/roles`, { method: "PUT", body: JSON.stringify(roleIds) });
        if (!r2.ok) throw new Error();
        const r1 = await auth(`/api/v1/users/${user.id}`, { method: "PUT", body: JSON.stringify({ username: form.username, email: form.email.trim() || null, displayName: form.displayName, password: form.password || null, isActive: form.isActive, branchIds, permissions: permissionsPayload }) });
        if (!r1.ok || !r2.ok) throw new Error();
        const result = await r1.json() as { signInRequired: boolean };
        if (result.signInRequired) { store.remove("session-token"); window.location.reload(); return; }
      } else {
        const r = await auth("/api/v1/users", { method: "POST", body: JSON.stringify({ username: form.username, email: form.email.trim() || null, displayName: form.displayName, password: form.password, roleIds, branchIds, permissions: permissionsPayload }) });
        if (!r.ok) { const problem = await r.json().catch(() => null); throw new Error(problem?.errors?.username?.[0] ?? problem?.errors?.password?.[0] ?? problem?.errors?.branchIds?.[0] ?? t.error); }
      }
      onSaved();
    } catch (e2) { setError(e2 instanceof Error ? e2.message : t.error); } finally { setSaving(false); }
  }

  return (
    <div className="fixed inset-0 z-50 grid place-items-end bg-black/35 sm:place-items-center sm:p-5">
      <form onSubmit={save} className="flex max-h-[94vh] w-full max-w-3xl flex-col overflow-hidden rounded-t-2xl bg-[#f5f6f2] shadow-2xl sm:rounded-2xl">
        <div className="flex items-center justify-between border-b border-[#e8ece8] bg-white px-5 py-4"><div><p className="text-sm font-semibold text-[#0e5a4f]">{user ? t.edit : t.create}</p><h2 className="text-xl font-bold">{user ? user.displayName : t.create}</h2></div><button type="button" aria-label={t.cancel} onClick={onClose} className="grid size-11 place-items-center rounded-lg border border-[#cdd7d0]"><X size={18} /></button></div>
        <div className="min-h-0 flex-1 space-y-5 overflow-y-auto p-5">
          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h3 className="text-sm font-semibold">{t.account}</h3>
            <div className="mt-4 grid gap-4 sm:grid-cols-2">
              <Field required label={t.username} value={form.username} onChange={(v) => setForm({ ...form, username: v })} max={40} />
              <Field label={t.email} type="email" value={form.email} onChange={(v) => setForm({ ...form, email: v })} max={160} />
              <Field required label={t.displayName} value={form.displayName} onChange={(v) => setForm({ ...form, displayName: v })} max={160} />
              <Field required={!user} label={user ? t.newPassword : t.password} type="password" value={form.password} onChange={(v) => setForm({ ...form, password: v })} min={6} />
            </div>
            {user && <label className="mt-4 flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.target.checked })} className="size-4 accent-[#0e5a4f]" />{t.active}</label>}
          </section>

          <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
            <h3 className="text-sm font-semibold">{t.roles}</h3>
            <p className="mt-1 text-xs text-[#69766f]">{t.roleHint}</p>
            {roles.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.noRoles}</p> : <div className="mt-3 flex flex-wrap gap-2">{roles.map((r) => <label key={r.id} className={`flex min-h-10 items-center gap-1.5 rounded-full border px-3 text-sm ${roleIds.includes(r.id) ? "border-[#0e5a4f] bg-[#e6f1ec] text-[#08483f]" : "border-[#dfe5df] text-[#53615b]"}`}><input type="checkbox" checked={roleIds.includes(r.id)} onChange={(e) => toggleRole(r.id, e.target.checked)} className="size-4 accent-[#0e5a4f]" />{r.name}</label>)}</div>}
            <h3 className="mt-5 text-sm font-semibold">{t.branches}</h3>
            {branches.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.noBranches}</p> : <div className="mt-3 flex flex-wrap gap-2">{branches.map((b) => <label key={b.id} className={`flex min-h-10 items-center gap-1.5 rounded-full border px-3 text-sm ${branchIds.includes(b.id) ? "border-[#0e5a4f] bg-[#e6f1ec] text-[#08483f]" : "border-[#dfe5df] text-[#53615b]"}`}><input type="checkbox" checked={branchIds.includes(b.id)} onChange={(e) => toggleBranch(b.id, e.target.checked)} className="size-4 accent-[#0e5a4f]" />{name(b)}</label>)}</div>}
          </section>

          <section>
            <h3 className="mb-2 text-sm font-semibold">{t.permissions}</h3>
            <PermissionGrid language={language} permissions={permissions} mode="user" states={states} roleDefault={roleDefault} onChange={setOverride} />
          </section>
        </div>
        <div className="flex items-center justify-between gap-3 border-t border-[#e8ece8] bg-white px-5 py-4">
          <p className="text-xs text-[#69766f]">{t.selfNote}</p>
          <div className="flex items-center gap-2">
            {error && <p role="alert" className="text-sm text-[#b4322a]">{error}</p>}
            <button type="button" onClick={onClose} className="min-h-11 rounded-lg border border-[#cdd7d0] px-4 text-sm font-semibold">{t.cancel}</button>
            <button disabled={saving} className="min-h-11 rounded-lg bg-[#0e5a4f] px-5 text-sm font-semibold text-white disabled:opacity-60">{saving ? t.saving : t.save}</button>
          </div>
        </div>
      </form>
    </div>
  );
}

function Field({ label, value, onChange, type = "text", max, min, required = false }: { label: string; value: string; onChange: (v: string) => void; type?: string; max?: number; min?: number; required?: boolean }) {
  return <label className="block text-sm font-medium">{label}<input required={required} type={type} value={value} onChange={(e) => onChange(e.target.value)} maxLength={max} minLength={min} autoComplete={type === "password" ? "new-password" : "off"} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20" /></label>;
}
