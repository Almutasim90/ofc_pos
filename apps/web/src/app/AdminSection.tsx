import { UserEditor } from "@/app/UserEditor";
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
    users: "المستخدمون", addUser: "إضافة مستخدم", username: "اسم المستخدم", email: "البريد الإلكتروني", displayName: "الاسم الظاهر", password: "كلمة المرور", roles: "الأدوار", branches2: "الفروع المخصصة", noUsers: "لا يوجد مستخدمون بعد", editRoles: "تعديل الأدوار", save: "حفظ",
    addRole: "إضافة دور", roleName: "اسم الدور", permissions: "الصلاحيات", noRoles: "لا توجد أدوار بعد",
  },
  en: {
    access: "Access is protected by server permissions", loading: "Loading", error: "Unable to load data. Check your connection and permissions.", retry: "Retry", saved: "Saved.", add: "Add",
    branches: "Branches", addBranch: "Add branch", code: "Code", nameAr: "Arabic name", nameEn: "English name", timeZone: "Time zone", active: "Active", inactive: "Inactive", settings: "Settings", settingKey: "Key", settingValue: "Value", addSetting: "Add/update setting", noBranches: "No branches yet",
    devices: "Devices", addDevice: "Add device", branch: "Branch", deviceName: "Device name", registrationCode: "Registration code", lastSeen: "Last seen", never: "Never connected", online: "Online", offline: "Offline", noDevices: "No devices yet",
    users: "Users", addUser: "Add user", username: "Username", email: "Email", displayName: "Display name", password: "Password", roles: "Roles", branches2: "Assigned branches", noUsers: "No users yet", editRoles: "Edit roles", save: "Save",
    addRole: "Add role", roleName: "Role name", permissions: "Permissions", noRoles: "No roles yet",
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
      if (!branchesRes.ok || !devicesRes.ok || !usersRes.ok || !rolesRes.ok) throw new Error();
      setBranches(await branchesRes.json() as Branch[]);
      setDevices(await devicesRes.json() as Device[]);
      setUsers(await usersRes.json() as AdminUser[]);
      setRoles(await rolesRes.json() as Role[]);
      if (permissionsRes.ok) setPermissions(await permissionsRes.json() as Permission[]);
    } catch { setError(true); } finally { setLoading(false); }
  }
  useEffect(() => { void load(); }, []);

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
    {!loading && !error && view === "users" && <UsersPanel t={t} language={language} users={users} roles={roles} branches={branches} permissions={permissions} name={name} auth={auth} onSaved={(msg) => { flash(msg); void load(); }} />}
  </>;
}

function BranchesPanel({ t, branches, auth, onSaved }: { t: Copy; language: Language; branches: Branch[]; auth: (p: string, i?: RequestInit) => Promise<Response>; onSaved: (msg: string) => void }) {
  const [form, setForm] = useState({ code: "", nameAr: "", nameEn: "", timeZone: "" });
  const [settingForm, setSettingForm] = useState<Record<string, { key: string; value: string }>>({});
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  async function createBranch(e: React.FormEvent) {
    e.preventDefault(); setFormError(""); setSaving(true);
    try {
      const response = await auth("/api/v1/branches", { method: "POST", body: JSON.stringify({ code: form.code, nameAr: form.nameAr, nameEn: form.nameEn, timeZone: form.timeZone.trim() || null }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.code?.[0] ?? t.error); }
      setForm({ code: "", nameAr: "", nameEn: "", timeZone: "" }); onSaved(t.saved);
    } catch (e2) { setFormError(e2 instanceof Error ? e2.message : t.error); } finally { setSaving(false); }
  }

  async function saveSetting(branchId: string) {
    const entry = settingForm[branchId]; if (!entry?.key.trim()) return;
    const response = await auth(`/api/v1/branches/${branchId}/settings`, { method: "PUT", body: JSON.stringify({ [entry.key.trim()]: entry.value }) });
    if (response.ok) { setSettingForm({ ...settingForm, [branchId]: { key: "", value: "" } }); onSaved(t.saved); }
  }

  return <>
    <form onSubmit={createBranch} className="mt-6 rounded-xl border border-[#dfe5df] bg-white p-5">
      <h2 className="font-semibold">{t.addBranch}</h2>
      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <Field label={t.code} value={form.code} onChange={(v) => setForm({ ...form, code: v })} max={30} />
        <Field label={t.timeZone} value={form.timeZone} onChange={(v) => setForm({ ...form, timeZone: v })} max={60} />
        <Field required label={t.nameAr} value={form.nameAr} onChange={(v) => setForm({ ...form, nameAr: v })} max={160} />
        <Field required label={t.nameEn} value={form.nameEn} onChange={(v) => setForm({ ...form, nameEn: v })} max={160} />
      </div>
      {formError && <p role="alert" className="mt-4 text-sm text-[#b4322a]">{formError}</p>}
      <button disabled={saving} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Plus size={18} />{t.add}</button>
    </form>
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
  const [form, setForm] = useState({ branchId: "", name: "", registrationCode: "" });
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  async function createDevice(e: React.FormEvent) {
    e.preventDefault(); setFormError(""); setSaving(true);
    try {
      const response = await auth("/api/v1/devices", { method: "POST", body: JSON.stringify({ branchId: form.branchId, name: form.name, registrationCode: form.registrationCode }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.name?.[0] ?? problem?.errors?.branchId?.[0] ?? t.error); }
      setForm({ branchId: "", name: "", registrationCode: "" }); onSaved(t.saved);
    } catch (e2) { setFormError(e2 instanceof Error ? e2.message : t.error); } finally { setSaving(false); }
  }

  return <>
    <form onSubmit={createDevice} className="mt-6 rounded-xl border border-[#dfe5df] bg-white p-5">
      <h2 className="font-semibold">{t.addDevice}</h2>
      <div className="mt-4 grid gap-4 sm:grid-cols-2">
        <label className="block text-sm font-medium">{t.branch}<select required value={form.branchId} onChange={(e) => setForm({ ...form, branchId: e.target.value })} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3"><option value="">—</option>{branches.map((b) => <option key={b.id} value={b.id}>{name(b)}</option>)}</select></label>
        <Field label={t.deviceName} value={form.name} onChange={(v) => setForm({ ...form, name: v })} max={100} />
        <Field label={t.registrationCode} value={form.registrationCode} onChange={(v) => setForm({ ...form, registrationCode: v })} max={100} />
      </div>
      {formError && <p role="alert" className="mt-4 text-sm text-[#b4322a]">{formError}</p>}
      <button disabled={saving} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Plus size={18} />{t.add}</button>
    </form>
    {devices.length === 0 ? <Empty text={t.noDevices} /> : <div className="mt-6 divide-y divide-[#e8ece8] rounded-xl border border-[#dfe5df] bg-white">{devices.map((d) => <div key={d.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
      <div><p className="font-medium">{d.name}</p><p className="mt-1 text-sm text-[#69766f]">{branches.find((b) => b.id === d.branchId)?.code ?? d.branchId} · {t.lastSeen}: {d.lastSeenAt ? new Date(d.lastSeenAt).toLocaleString() : t.never}</p></div>
      <span className={`rounded-full px-3 py-1 text-xs font-semibold ${d.isActive ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#fbe4e2] text-[#b4322a]"}`}>{d.isActive ? t.online : t.offline}</span>
    </div>)}</div>}
  </>;
}

function UsersPanel({ t, language, users, roles, branches, permissions, name, auth, onSaved }: { t: Copy; language: Language; users: AdminUser[]; roles: Role[]; branches: Branch[]; permissions: Permission[]; name: (x: { nameAr: string; nameEn: string }) => string; auth: (p: string, i?: RequestInit) => Promise<Response>; onSaved: (msg: string) => void }) {
  const [form, setForm] = useState({ username: "", email: "", displayName: "", password: "", roleIds: [] as string[], branchIds: [] as string[] });
  const [editingUser, setEditingUser] = useState<AdminUser | null>(null);
  const [roleForm, setRoleForm] = useState({ name: "", permissionCodes: [] as string[] });
  const [editingRoles, setEditingRoles] = useState<Record<string, string[]>>({});
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState("");

  const roleIdByName = (roleName: string) => roles.find((r) => r.name === roleName)?.id;

  async function createUser(e: React.FormEvent) {
    e.preventDefault(); setFormError(""); setSaving(true);
    try {
      const response = await auth("/api/v1/users", { method: "POST", body: JSON.stringify({ username: form.username, email: form.email.trim() || null, displayName: form.displayName, password: form.password, roleIds: form.roleIds, branchIds: form.branchIds }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.username?.[0] ?? problem?.errors?.password?.[0] ?? problem?.errors?.branchIds?.[0] ?? t.error); }
      setForm({ username: "", email: "", displayName: "", password: "", roleIds: [], branchIds: [] }); onSaved(t.saved);
    } catch (e2) { setFormError(e2 instanceof Error ? e2.message : t.error); } finally { setSaving(false); }
  }

  async function createRole(e: React.FormEvent) {
    e.preventDefault(); setFormError(""); setSaving(true);
    try {
      const response = await auth("/api/v1/roles", { method: "POST", body: JSON.stringify({ name: roleForm.name, permissionCodes: roleForm.permissionCodes }) });
      if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.name?.[0] ?? t.error); }
      setRoleForm({ name: "", permissionCodes: [] }); onSaved(t.saved);
    } catch (e2) { setFormError(e2 instanceof Error ? e2.message : t.error); } finally { setSaving(false); }
  }

  async function saveUserRoles(userId: string) {
    const selected = editingRoles[userId]; if (!selected) return;
    const roleIds = selected.map((n) => roleIdByName(n)).filter((id): id is string => !!id);
    const response = await auth(`/api/v1/users/${userId}/roles`, { method: "PUT", body: JSON.stringify(roleIds) });
    if (response.ok) { const next = { ...editingRoles }; delete next[userId]; setEditingRoles(next); onSaved(t.saved); }
  }

  return <>
    <div className="mt-6 grid gap-5 xl:grid-cols-2">
      <form onSubmit={createUser} className="rounded-xl border border-[#dfe5df] bg-white p-5">
        <h2 className="font-semibold">{t.addUser}</h2>
        <div className="mt-4 grid gap-4 sm:grid-cols-2">
          <Field required label={t.username} value={form.username} onChange={(v) => setForm({ ...form, username: v })} max={40} />
          <Field label={t.email} value={form.email} onChange={(v) => setForm({ ...form, email: v })} max={160} />
          <Field required label={t.displayName} value={form.displayName} onChange={(v) => setForm({ ...form, displayName: v })} max={160} />
          <Field required label={t.password} value={form.password} onChange={(v) => setForm({ ...form, password: v })} type="password" />
        </div>
        <fieldset className="mt-4"><legend className="text-sm font-medium">{t.roles}</legend><div className="mt-2 flex flex-wrap gap-2">{roles.map((r) => <label key={r.id} className="flex min-h-9 items-center gap-1.5 rounded-full border border-[#dfe5df] px-3 text-sm"><input type="checkbox" checked={form.roleIds.includes(r.id)} onChange={(e) => setForm({ ...form, roleIds: e.target.checked ? [...form.roleIds, r.id] : form.roleIds.filter((id) => id !== r.id) })} className="accent-[#0e5a4f]" />{r.name}</label>)}</div></fieldset>
        <fieldset className="mt-4"><legend className="text-sm font-medium">{t.branches2}</legend><div className="mt-2 flex flex-wrap gap-2">{branches.map((b) => <label key={b.id} className="flex min-h-9 items-center gap-1.5 rounded-full border border-[#dfe5df] px-3 text-sm"><input type="checkbox" checked={form.branchIds.includes(b.id)} onChange={(e) => setForm({ ...form, branchIds: e.target.checked ? [...form.branchIds, b.id] : form.branchIds.filter((id) => id !== b.id) })} className="accent-[#0e5a4f]" />{name(b)}</label>)}</div></fieldset>
        {formError && <p role="alert" className="mt-4 text-sm text-[#b4322a]">{formError}</p>}
        <button disabled={saving} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Plus size={18} />{t.add}</button>
      </form>
      <details><summary className="min-h-11 cursor-pointer font-semibold">{t.addRole}</summary><form onSubmit={createRole} className="rounded-xl border border-[#dfe5df] bg-white p-5">
        <h2 className="font-semibold">{t.addRole}</h2>
        <div className="mt-4"><Field required label={t.roleName} value={roleForm.name} onChange={(v) => setRoleForm({ ...roleForm, name: v })} max={100} /></div>
        <fieldset className="mt-4"><legend className="text-sm font-medium">{t.permissions}</legend><div className="mt-2 flex max-h-56 flex-wrap gap-2 overflow-y-auto">{permissions.map((p) => <label key={p.id} className="flex min-h-9 items-center gap-1.5 rounded-full border border-[#dfe5df] px-3 text-xs"><input type="checkbox" checked={roleForm.permissionCodes.includes(p.code)} onChange={(e) => setRoleForm({ ...roleForm, permissionCodes: e.target.checked ? [...roleForm.permissionCodes, p.code] : roleForm.permissionCodes.filter((c) => c !== p.code) })} className="accent-[#0e5a4f]" />{p.code}</label>)}</div></fieldset>
        <button disabled={saving} className="mt-5 inline-flex min-h-11 items-center gap-2 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f] disabled:opacity-60"><Plus size={18} />{t.add}</button>
      </form></details>
    </div>
    {roles.length > 0 && <div className="mt-5 flex flex-wrap gap-2">{roles.map((r) => <span key={r.id} className="rounded-full bg-[#f4f7f4] px-3 py-1 text-xs text-[#53615b]">{r.name} ({r.permissions.length})</span>)}</div>}
    {editingUser && <UserEditor key={editingUser.id} user={editingUser} branches={branches} language={language} auth={auth} onClose={() => setEditingUser(null)} onSaved={() => { setEditingUser(null); onSaved(t.saved); }} />}
    {users.length === 0 ? <Empty text={t.noUsers} /> : <div className="mt-6 divide-y divide-[#e8ece8] rounded-xl border border-[#dfe5df] bg-white">{users.map((u) => <div key={u.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
      <div className="min-w-0"><p className="font-medium">{u.displayName} <span className="font-normal text-[#69766f]">@{u.username}</span></p><p className="mt-1 text-sm text-[#69766f]">{u.roles.join(", ") || "—"}</p></div>
      <button onClick={() => { setEditingUser(u); }} className="min-h-11 rounded-lg border border-[#0e5a4f] px-4 text-sm font-semibold text-[#0e5a4f]">{language === "ar" ? "تعديل البيانات" : "Edit details"}</button>
      {editingRoles[u.id] ? <div className="flex flex-wrap items-center gap-2">{roles.map((r) => <label key={r.id} className="flex min-h-8 items-center gap-1.5 rounded-full border border-[#dfe5df] px-2.5 text-xs"><input type="checkbox" checked={editingRoles[u.id].includes(r.name)} onChange={(e) => setEditingRoles({ ...editingRoles, [u.id]: e.target.checked ? [...editingRoles[u.id], r.name] : editingRoles[u.id].filter((n) => n !== r.name) })} className="accent-[#0e5a4f]" />{r.name}</label>)}<button onClick={() => void saveUserRoles(u.id)} className="min-h-8 rounded-lg bg-[#0e5a4f] px-3 text-xs font-semibold text-white">{t.save}</button></div>
        : <button onClick={() => setEditingRoles({ ...editingRoles, [u.id]: [...u.roles] })} className="min-h-9 rounded-lg border border-[#0e5a4f] px-3 text-xs font-semibold text-[#0e5a4f]">{t.editRoles}</button>}
    </div>)}</div>}
  </>;
}

function Field({ label, value, onChange, max, type = "text", required = false }: { label: string; value: string; onChange: (value: string) => void; max?: number; type?: string; required?: boolean }) {
  return <label className="block text-sm font-medium">{label}{required && " *"}<input required={required} type={type} value={value} onChange={(e) => onChange(e.target.value)} maxLength={max} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20" /></label>;
}
function Empty({ text }: { text: string }) { return <div className="mt-6 rounded-xl border border-[#dfe5df] bg-white p-8 text-center text-sm text-[#69766f]">{text}</div>; }
