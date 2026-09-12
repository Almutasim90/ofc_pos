import { UserForm } from "@/app/UserForm";
import { PermissionGrid, type PermissionOverride } from "@/app/PermissionGrid";
import { FormDialog } from "@/app/FormDialog";
import { useEffect, useState } from "react";
import { Plus, RefreshCw } from "lucide-react";
import { store } from "@/lib/local-store";

type Language = "ar" | "en";
type View = "branches" | "devices" | "users";
type Branch = { id: string; code: string; nameAr: string; nameEn: string; timeZone: string; isActive: boolean; settings: Array<{ key: string; value: string }> };
type Device = { id: string; branchId: string; name: string; registrationCode: string; isActive: boolean; lastSeenAt: string | null };
type Role = { id: string; name: string; permissions: string[] };
type Permission = { id: string; code: string };
type AdminUser = { id: string; username: string; email: string | null; displayName: string; isActive: boolean; roles: string[]; branchIds: string[] };

const copy = {
  ar: {
    access: "الوصول محمي بصلاحيات الخادم", loading: "جارٍ التحميل", error: "تعذر تحميل البيانات. تحقق من اتصالك وصلاحياتك.", retry: "إعادة المحاولة", saved: "تم الحفظ.", add: "إضافة",
    branches: "الفروع", addBranch: "إضافة فرع", code: "الرمز", nameAr: "الاسم بالعربية", nameEn: "الاسم بالإنجليزية", timeZone: "المنطقة الزمنية", active: "نشط", inactive: "غير نشط", settings: "الإعدادات", settingKey: "المفتاح", settingValue: "القيمة", addSetting: "إضافة/تحديث إعداد", noBranches: "لا توجد فروع بعد",
    devices: "الأجهزة", addDevice: "إضافة جهاز", branch: "الفرع", deviceName: "اسم الجهاز", registrationCode: "رمز التسجيل", lastSeen: "آخر ظهور", never: "لم يتصل بعد", online: "متصل", offline: "غير متصل", noDevices: "لا توجد أجهزة بعد",
    users: "المستخدمون", addUser: "إضافة مستخدم", username: "اسم المستخدم", email: "البريد الإلكتروني", displayName: "الاسم الظاهر", password: "كلمة المرور", roles: "الأدوار", branches2: "الفروع المخصصة", noUsers: "لا يوجد مستخدمون بعد", editRoles: "تعديل الأدوار", edit: "تعديل", save: "حفظ",
    addRole: "إضافة دور", roleName: "اسم الدور", permissions: "الصلاحيات", noRoles: "لا توجد أدوار بعد", cancel: "إلغاء",
  },
  en: {
    access: "Access is protected by server permissions", loading: "Loading", error: "Unable to load data. Check your connection and permissions.", retry: "Retry", saved: "Saved.", add: "Add",
    branches: "Branches", addBranch: "Add branch", code: "Code", nameAr: "Arabic name", nameEn: "English name", timeZone: "Time zone", active: "Active", inactive: "Inactive", settings: "Settings", settingKey: "Key", settingValue: "Value", addSetting: "Add/update setting", noBranches: "No branches yet",
    devices: "Devices", addDevice: "Add device", branch: "Branch", deviceName: "Device name", registrationCode: "Registration code", lastSeen: "Last seen", never: "Never connected", online: "Online", offline: "Offline", noDevices: "No devices yet",
    users: "Users", addUser: "Add user", username: "Username", email: "Email", displayName: "Display name", password: "Password", roles: "Roles", branches2: "Assigned branches", noUsers: "No users yet", editRoles: "Edit roles", edit: "Edit", save: "Save",
    addRole: "Add role", roleName: "Role name", permissions: "Permissions", noRoles: "No roles yet", cancel: "Cancel",
  },
} as const;
type Copy = (typeof copy)[Language];

export function AdminSection({ language, view }: { language: Language; view: View }) {
  const t = copy[language];
  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });

  const [branches, setBranches] = useState<Branch[]>([]);
  const [devices, setDevices] = useState<Device[]>([]);
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [permissions, setPermissions] = useState<Permission[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [notice, setNotice] = useState("");

  async function load() {
    setLoading(true); setError(false);
    try {
      const [branchesRes, devicesRes, usersRes, rolesRes, permissionsRes] = await Promise.all([
        auth("/api/v1/branches"), auth("/api/v1/devices"), auth("/api/v1/users"), auth("/api/v1/roles"), auth("/api/v1/permissions"),
      ]);
      if (branchesRes.ok) setBranches(await branchesRes.json() as Branch[]);
      if (devicesRes.ok) setDevices(await devicesRes.json() as Device[]);
      if (usersRes.ok) setUsers(await usersRes.json() as AdminUser[]);
      if (rolesRes.ok) setRoles(await rolesRes.json() as Role[]);
      if (permissionsRes.ok) setPermissions(await permissionsRes.json() as Permission[]);
      // Sidebar/route access already gates which of Branches/Devices/Users this component is even
      // shown for; only the resource that matches the active sub-view needs to have actually loaded —
      // a role without permission for a sibling admin resource must not fail the whole page.
      const required = view === "branches" ? branchesRes : view === "devices" ? devicesRes : usersRes;
      if (!required.ok) throw new Error();
    } catch { setError(true); } finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, [view]);

  const name = (x: { nameAr: string; nameEn: string }) => (language === "ar" ? x.nameAr : x.nameEn);
  const flash = (msg: string) => { setNotice(msg); setTimeout(() => setNotice(""), 3000); };

  return <>
    <p className="text-sm font-semibold text-[#0e5a4f]">{t.access}</p>
    <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{view === "branches" ? t.branches : view === "devices" ? t.devices : t.users}</h1>
    {notice && <p role="status" className="mt-3 text-sm text-[#137347]">{notice}</p>}
    {loading && <div className="mt-8 flex items-center gap-3 text-[#53615b]"><RefreshCw className="animate-spin" size={20} />{t.loading}</div>}
    {!loading && error && <div role="alert" className="mt-8 rounded-xl border border-[#efc5c1] bg-[#fff5f4] p-5 text-[#9b2922]"><p>{t.error}</p><button onClick={() => void load()} className="mt-3 font-semibold underline">{t.retry}</button></div>}
    {!loading && !error && view === "branches" && <BranchesPanel t={t} language={language} branches={branches} auth={auth} onSaved={(msg) => { flash(msg); void load(); }} />}
    {!loading && !error && view === "devices" && <DevicesPanel t={t} language={language} devices={devices} branches={branches} name={name} auth={auth} onSaved={(msg) => { flash(msg); void load(); }} />}
    {!loading && !error && view === "users" && <UsersPanel t={t} language={language} users={users} roles={roles} branches={branches} permissions={permissions} auth={auth} onSaved={(msg) => { flash(msg); void load(); }} />}
  </>;
}

function BranchesPanel({ t, branches, auth, onSaved }: { t: Copy; language: Language; branches: Branch[]; auth: (p: string, i?: RequestInit) => Promise<Response>; onSaved: (msg: string) => void }) {
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ code: "", nameAr: "", nameEn: "", timeZone: "" });
  const [settingForm, setSettingForm] = useState<Record<string, { key: string; value: string }>>({});
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  async function createBranch(e: React.FormEvent) {
    e.preventDefault(); setFormError(""); setSaving(true);
    try {
      const response = await auth("/api/v1/branches", { method: "POST", body: JSON.stringify({ code: form.code, nameAr: form.nameAr, nameEn: form.nameEn, timeZone: form.timeZone.trim() || null }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.code?.[0] ?? t.error); }
      setForm({ code: "", nameAr: "", nameEn: "", timeZone: "" }); setCreating(false); onSaved(t.saved);
    } catch (e2) { setFormError(e2 instanceof Error ? e2.message : t.error); } finally { setSaving(false); }
  }

  async function saveSetting(branchId: string) {
    const entry = settingForm[branchId]; if (!entry?.key.trim()) return;
    const response = await auth(`/api/v1/branches/${branchId}/settings`, { method: "PUT", body: JSON.stringify({ [entry.key.trim()]: entry.value }) });
    if (response.ok) { setSettingForm({ ...settingForm, [branchId]: { key: "", value: "" } }); onSaved(t.saved); }
  }

  return <>
    <button onClick={() => setCreating(true)} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.addBranch}</button>
    {creating && <FormDialog title={t.addBranch} closeLabel={t.cancel} onClose={() => setCreating(false)}>
      <form onSubmit={createBranch}>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label={t.code} value={form.code} onChange={(v) => setForm({ ...form, code: v })} max={30} />
          <Field label={t.timeZone} value={form.timeZone} onChange={(v) => setForm({ ...form, timeZone: v })} max={60} />
          <Field required label={t.nameAr} value={form.nameAr} onChange={(v) => setForm({ ...form, nameAr: v })} max={160} />
          <Field required label={t.nameEn} value={form.nameEn} onChange={(v) => setForm({ ...form, nameEn: v })} max={160} />
        </div>
        {formError && <p role="alert" className="mt-4 text-sm text-[#b4322a]">{formError}</p>}
        <button disabled={saving} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Plus size={18} />{t.add}</button>
      </form>
    </FormDialog>}
    {branches.length === 0 ? <Empty text={t.noBranches} /> : <div className="mt-6 grid gap-4 lg:grid-cols-2">{branches.map((b) => <div key={b.id} className="rounded-xl border border-[#dfe5df] bg-white p-5">
      <div className="flex items-center justify-between gap-3"><h3 className="font-semibold">{b.nameEn} / {b.nameAr}</h3><span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${b.isActive ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#fbe4e2] text-[#b4322a]"}`}>{b.isActive ? t.active : t.inactive}</span></div>
      <p className="mt-1 text-sm text-[#69766f]">{b.code} · {b.timeZone}</p>
      {b.settings.length > 0 && <ul className="mt-3 space-y-1 text-sm text-[#53615b]">{b.settings.map((s) => <li key={s.key}><b className="font-medium">{s.key}</b>: {s.value}</li>)}</ul>}
      <div className="mt-4 grid grid-cols-[1fr_1fr_auto] items-end gap-2">
        <label className="text-xs font-medium">{t.settingKey}<input value={settingForm[b.id]?.key ?? ""} onChange={(e) => setSettingForm({ ...settingForm, [b.id]: { key: e.target.value, value: settingForm[b.id]?.value ?? "" } })} className="mt-1 min-h-10 w-full rounded-lg border border-[#cdd7d0] px-2 text-sm" /></label>
        <label className="text-xs font-medium">{t.settingValue}<input value={settingForm[b.id]?.value ?? ""} onChange={(e) => setSettingForm({ ...settingForm, [b.id]: { key: settingForm[b.id]?.key ?? "", value: e.target.value } })} className="mt-1 min-h-10 w-full rounded-lg border border-[#cdd7d0] px-2 text-sm" /></label>
        <button onClick={() => void saveSetting(b.id)} className="min-h-10 rounded-lg border border-[#0e5a4f] px-3 text-xs font-semibold text-[#0e5a4f]">{t.addSetting}</button>
      </div>
    </div>)}</div>}
  </>;
}

function DevicesPanel({ t, devices, branches, name, auth, onSaved }: { t: Copy; language: Language; devices: Device[]; branches: Branch[]; name: (x: { nameAr: string; nameEn: string }) => string; auth: (p: string, i?: RequestInit) => Promise<Response>; onSaved: (msg: string) => void }) {
  const [creating, setCreating] = useState(false);
  const [form, setForm] = useState({ branchId: "", name: "", registrationCode: "" });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  async function createDevice(e: React.FormEvent) {
    e.preventDefault(); setFormError(""); setSaving(true);
    try {
      const response = await auth("/api/v1/devices", { method: "POST", body: JSON.stringify({ branchId: form.branchId, name: form.name, registrationCode: form.registrationCode }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.name?.[0] ?? problem?.errors?.branchId?.[0] ?? t.error); }
      setForm({ branchId: "", name: "", registrationCode: "" }); setCreating(false); onSaved(t.saved);
    } catch (e2) { setFormError(e2 instanceof Error ? e2.message : t.error); } finally { setSaving(false); }
  }

  return <>
    <button onClick={() => setCreating(true)} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.addDevice}</button>
    {creating && <FormDialog title={t.addDevice} closeLabel={t.cancel} onClose={() => setCreating(false)}>
      <form onSubmit={createDevice}>
        <div className="grid gap-4 sm:grid-cols-2">
          <label className="block text-sm font-medium">{t.branch}<select required value={form.branchId} onChange={(e) => setForm({ ...form, branchId: e.target.value })} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3"><option value="">—</option>{branches.map((b) => <option key={b.id} value={b.id}>{name(b)}</option>)}</select></label>
          <Field label={t.deviceName} value={form.name} onChange={(v) => setForm({ ...form, name: v })} max={100} />
          <Field label={t.registrationCode} value={form.registrationCode} onChange={(v) => setForm({ ...form, registrationCode: v })} max={100} />
        </div>
        {formError && <p role="alert" className="mt-4 text-sm text-[#b4322a]">{formError}</p>}
        <button disabled={saving} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Plus size={18} />{t.add}</button>
      </form>
    </FormDialog>}
    {devices.length === 0 ? <Empty text={t.noDevices} /> : <div className="mt-6 divide-y divide-[#e8ece8] rounded-xl border border-[#dfe5df] bg-white">{devices.map((d) => <div key={d.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
      <div><p className="font-medium">{d.name}</p><p className="mt-1 text-sm text-[#69766f]">{branches.find((b) => b.id === d.branchId)?.code ?? d.branchId} · {t.lastSeen}: {d.lastSeenAt ? new Date(d.lastSeenAt).toLocaleString() : t.never}</p></div>
      <span className={`rounded-full px-3 py-1 text-xs font-semibold ${d.isActive ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#fbe4e2] text-[#b4322a]"}`}>{d.isActive ? t.online : t.offline}</span>
    </div>)}</div>}
  </>;
}

function UsersPanel({ t, language, users, roles, branches, permissions, auth, onSaved }: { t: Copy; language: Language; users: AdminUser[]; roles: Role[]; branches: Branch[]; permissions: Permission[]; auth: (p: string, i?: RequestInit) => Promise<Response>; onSaved: (msg: string) => void }) {
  const [tab, setTab] = useState<"users" | "roles">("users");
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<AdminUser | null>(null);
  const [roleDraft, setRoleDraft] = useState<{ id: string | null; name: string; states: Record<string, PermissionOverride> } | null>(null);

  return <>
    <div className="mt-6 flex border-b border-[#dfe5df]" role="tablist" aria-label={`${t.users} / ${t.roles}`}>
      {(["users", "roles"] as const).map((key) => <button key={key} type="button" role="tab" aria-selected={tab === key} onClick={() => setTab(key)} className={`min-h-12 border-b-2 px-5 text-sm font-semibold ${tab === key ? "border-[#0e5a4f] text-[#0e5a4f]" : "border-transparent text-[#69766f] hover:text-[#17211f]"}`}>{key === "users" ? `${t.users} (${users.length})` : `${t.roles} (${roles.length})`}</button>)}
    </div>

    {tab === "users" ? <>
      <button onClick={() => setCreating(true)} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.addUser}</button>
      {users.length === 0 ? <Empty text={t.noUsers} /> : <UsersTable t={t} users={users} branches={branches} onEdit={setEditing} />}
    </> : <>
      <button onClick={() => setRoleDraft({ id: null, name: "", states: {} })} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]"><Plus size={18} />{t.addRole}</button>
      {roles.length === 0 ? <Empty text={t.noRoles} /> : <RolesTable t={t} roles={roles} onEdit={(r) => setRoleDraft({ id: r.id, name: r.name, states: Object.fromEntries(r.permissions.map((c) => [c, "grant"])) })} />}
    </>}

    {creating && <UserForm language={language} user={null} roles={roles} permissions={permissions} branches={branches} auth={auth} onClose={() => setCreating(false)} onSaved={() => { setCreating(false); onSaved(t.saved); }} />}
    {editing && <UserForm language={language} user={editing} roles={roles} permissions={permissions} branches={branches} auth={auth} onClose={() => setEditing(null)} onSaved={() => { setEditing(null); onSaved(t.saved); }} />}
    {roleDraft && <RoleForm t={t} language={language} draft={roleDraft} permissions={permissions} auth={auth} onClose={() => setRoleDraft(null)} onSaved={() => { setRoleDraft(null); onSaved(t.saved); }} />}
  </>;
}

function UsersTable({ t, users, branches, onEdit }: { t: Copy; users: AdminUser[]; branches: Branch[]; onEdit: (u: AdminUser) => void }) {
  return <div className="mt-6 overflow-x-auto rounded-xl border border-[#dfe5df] bg-white">
    <table className="w-full text-sm">
      <thead><tr className="border-b border-[#e8ece8] text-start text-xs text-[#69766f]">
        <th className="px-4 py-3 text-start font-semibold">{t.users}</th>
        <th className="px-4 py-3 text-start font-semibold">{t.email}</th>
        <th className="px-4 py-3 text-start font-semibold">{t.roles}</th>
        <th className="px-4 py-3 text-start font-semibold">{t.branches2}</th>
        <th className="px-4 py-3 text-start font-semibold">{t.active}</th>
        <th className="px-4 py-3 text-end font-semibold"></th>
      </tr></thead>
      <tbody>{users.map((u) => <tr key={u.id} className="border-b border-[#eef1ee] last:border-0 hover:bg-[#fafbf9]">
        <td className="px-4 py-3"><p className="font-medium">{u.displayName}</p><p className="text-xs text-[#69766f]">@{u.username}</p></td>
        <td className="px-4 py-3 text-[#53615b]">{u.email ?? "—"}</td>
        <td className="px-4 py-3">{u.roles.length ? <div className="flex flex-wrap gap-1">{u.roles.map((r) => <span key={r} className="rounded-full bg-[#e6f1ec] px-2 py-0.5 text-xs font-semibold text-[#08483f]">{r}</span>)}</div> : "—"}</td>
        <td className="px-4 py-3 text-[#53615b]">{u.branchIds.map((id) => branches.find((b) => b.id === id)?.code ?? "").filter(Boolean).join(", ") || "—"}</td>
        <td className="px-4 py-3"><span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${u.isActive ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#fbe4e2] text-[#b4322a]"}`}>{u.isActive ? t.active : t.inactive}</span></td>
        <td className="px-4 py-3 text-end"><button onClick={() => onEdit(u)} className="min-h-9 rounded-lg border border-[#0e5a4f] px-3 text-xs font-semibold text-[#0e5a4f]">{t.edit}</button></td>
      </tr>)}</tbody>
    </table>
  </div>;
}

function RolesTable({ t, roles, onEdit }: { t: Copy; roles: Role[]; onEdit: (r: Role) => void }) {
  return <div className="mt-6 overflow-x-auto rounded-xl border border-[#dfe5df] bg-white">
    <div className="border-b border-[#e8ece8] px-4 py-3"><h2 className="text-sm font-semibold">{t.addRole} ({roles.length})</h2></div>
    <table className="w-full text-sm">
      <thead><tr className="border-b border-[#e8ece8] text-start text-xs text-[#69766f]"><th className="px-4 py-3 text-start font-semibold">{t.roleName}</th><th className="px-4 py-3 text-start font-semibold">{t.permissions}</th><th className="px-4 py-3 text-end font-semibold"></th></tr></thead>
      <tbody>{roles.map((r) => <tr key={r.id} className="border-b border-[#eef1ee] last:border-0 hover:bg-[#fafbf9]"><td className="px-4 py-3 font-medium">{r.name}</td><td className="px-4 py-3 text-[#53615b]">{r.permissions.length}</td><td className="px-4 py-3 text-end"><button onClick={() => onEdit(r)} className="min-h-9 rounded-lg border border-[#0e5a4f] px-3 text-xs font-semibold text-[#0e5a4f]">{t.editRoles}</button></td></tr>)}</tbody>
    </table>
  </div>;
}

function RoleForm({ t, language, draft, permissions, auth, onClose, onSaved }: { t: Copy; language: Language; draft: { id: string | null; name: string; states: Record<string, PermissionOverride> }; permissions: Permission[]; auth: (p: string, i?: RequestInit) => Promise<Response>; onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState(draft.name);
  const [states, setStates] = useState<Record<string, PermissionOverride>>(draft.states);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (saving) return;
    setSaving(true);
    setError("");
    const codes = Object.entries(states).filter(([, s]) => s === "grant").map(([c]) => c);
    try {
      const response = draft.id
        ? await auth(`/api/v1/roles/${draft.id}/permissions`, { method: "PUT", body: JSON.stringify(codes) })
        : await auth("/api/v1/roles", { method: "POST", body: JSON.stringify({ name: name.trim(), permissionCodes: codes }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.name?.[0] ?? problem?.errors?.permissionCodes?.[0] ?? t.error); }
      onSaved();
    } catch (e2) { setError(e2 instanceof Error ? e2.message : t.error); } finally { setSaving(false); }
  }

  return <FormDialog title={draft.id ? draft.name : t.addRole} closeLabel={t.cancel ?? "close"} onClose={onClose}>
    <form onSubmit={save} className="space-y-4">
      {!draft.id && <label className="block text-sm font-medium">{t.roleName}<input required value={name} onChange={(e) => setName(e.target.value)} maxLength={100} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] px-3 outline-none focus:border-[#0e5a4f]" /></label>}
      <PermissionGrid language={language} permissions={permissions} mode="role" states={states} roleDefault={new Set()} onChange={(code, s) => setStates((prev) => { const next = { ...prev }; if (s === "inherit") delete next[code]; else next[code] = s; return next; })} />
      <div className="flex items-center justify-between gap-3 border-t border-[#e8ece8] pt-4">
        <p className="text-xs text-[#69766f]">{t.permissions} · {Object.values(states).filter((s) => s === "grant").length}</p>
        <div className="flex items-center gap-2">
          {error && <p role="alert" className="text-sm text-[#b4322a]">{error}</p>}
          <button type="button" onClick={onClose} className="min-h-11 rounded-lg border border-[#cdd7d0] px-4 text-sm font-semibold">{t.cancel ?? "close"}</button>
          <button disabled={saving} className="min-h-11 rounded-lg bg-[#0e5a4f] px-5 text-sm font-semibold text-white disabled:opacity-60">{saving ? "..." : t.save}</button>
        </div>
      </div>
    </form>
  </FormDialog>;
}

function Field({ label, value, onChange, max, type = "text", required = false }: { label: string; value: string; onChange: (value: string) => void; max?: number; type?: string; required?: boolean }) {
  return <label className="block text-sm font-medium">{label}{required && " *"}<input required={required} type={type} value={value} onChange={(e) => onChange(e.target.value)} maxLength={max} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20" /></label>;
}
function Empty({ text }: { text: string }) { return <div className="mt-6 rounded-xl border border-[#dfe5df] bg-white p-8 text-center text-sm text-[#69766f]">{text}</div>; }
