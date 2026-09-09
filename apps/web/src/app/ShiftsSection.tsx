import { useEffect, useState } from "react";
import { FormDialog } from "@/app/FormDialog";
import { store } from "@/lib/local-store";

type Language = "ar" | "en";
type Branch = { id: string; nameAr: string; nameEn: string };
type Movement = { id: string; type: "CashIn" | "CashOut" | "PettyCash" | "CashDrop"; amount: number; reason: string | null; note: string | null; createdAt: string };
type CurrentShift = { id: string; status: "Open" | "Closed" | "Reviewed"; openingCash: number; openedAt: string; openedByUserId: string; cashSales: number; cardSales: number; cashRefunds: number; cardRefunds: number; movements: Movement[] };
type ShiftRow = { id: string; status: "Open" | "Closed" | "Reviewed"; openingCash: number; openedAt: string; closedAt: string | null; expectedCash: number | null; actualCash: number | null; cashVariance: number | null; cardVariance: number | null; reviewStatus: "NotReviewed" | "Approved" | "Rejected" };
type CloseResult = { status: string; openingCash: number; expectedCash: number; actualCash: number; cashVariance: number; cardExpected: number; actualCardTotal: number; cardVariance: number; denominationTotal: number; closedAt: string };

const denominations = [50, 20, 10, 5, 1, 0.5, 0.1, 0.05, 0.025] as const;

const copy = {
  ar: {
    title: "الورديات والنقد", branch: "الفرع", open: "فتح الوردية", openingCash: "رصيد الافتتاح", openSuccess: "تم فتح الوردية.", noShift: "لا توجد وردية مفتوحة لهذا الفرع.", currentShift: "الوردية المفتوحة", cashSales: "مبيعات نقدية", cardSales: "مبيعات بطاقات", cashRefunds: "مرتجعات نقدية", cardRefunds: "مرتجعات بطاقات", movements: "حركات النقد", addMovement: "تسجيل حركة", movementType: "نوع الحركة", amount: "المبلغ", reason: "السبب", note: "ملاحظة", cashIn: "إيداع نقدي", cashOut: "سحب نقدي", pettyCash: "مصاريف نثرية", cashDrop: "إنزال نقدي", blindClose: "تقفيل أعمى", actualCash: "النقد الفعلي", cardTotal: "إجمالي البطاقات", denominationsTitle: "تعداد الفئات النقدية", counted: "المجموع المعدّ", close: "تنفيذ التقفيل", closeResult: "ناتج التقفيل", expectedCash: "النقد المتوقع", actual: "الفعلي", cashVariance: "فرق النقد", cardVariance: "فرق البطاقات", history: "سجل الورديات", review: "مراجعة المشرف", approve: "اعتماد", reject: "رفض", reviewed: "تمت المراجعة.", saving: "جارٍ الحفظ", failed: "تعذر تنفيذ العملية.", branchTerminal: "محطة الفروع", none: "لا يوجد", statusOpen: "مفتوحة", statusClosed: "مغلقة", statusReviewed: "معتمدة", revApproved: "معتمدة", revRejected: "مرفوضة", revNotReviewed: "بدون مراجعة", openAt: "وقت الفتح", closeAt: "وقت الإغلاق", needActualCashMatch: "مجموع الفئات يجب أن يطابق النقد الفعلي.", denominationsHint: "أدخل عدد كل فئة. يجب أن يطابق المجموع النقدَ الفعلي.", dialogClose: "إغلاق"
  },
  en: {
    title: "Shifts & cash", branch: "Branch", open: "Open shift", openingCash: "Opening float", openSuccess: "Shift opened.", noShift: "No open shift for this branch.", currentShift: "Open shift", cashSales: "Cash sales", cardSales: "Card sales", cashRefunds: "Cash refunds", cardRefunds: "Card refunds", movements: "Cash movements", addMovement: "Record movement", movementType: "Movement type", amount: "Amount", reason: "Reason", note: "Note", cashIn: "Cash in", cashOut: "Cash out", pettyCash: "Petty cash", cashDrop: "Cash drop", blindClose: "Blind close", actualCash: "Actual cash", cardTotal: "Card total", denominationsTitle: "Cash denomination count", counted: "Counted total", close: "Close shift", closeResult: "Close result", expectedCash: "Expected cash", actual: "Actual", cashVariance: "Cash variance", cardVariance: "Card variance", history: "Shift history", review: "Supervisor review", approve: "Approve", reject: "Reject", reviewed: "Reviewed.", saving: "Saving", failed: "Unable to complete the operation.", branchTerminal: "Branch terminal", none: "None", statusOpen: "Open", statusClosed: "Closed", statusReviewed: "Reviewed", revApproved: "Approved", revRejected: "Rejected", revNotReviewed: "Not reviewed", openAt: "Opened", closeAt: "Closed", needActualCashMatch: "The denomination total must match the entered actual cash.", denominationsHint: "Enter the count of each denomination. The total must equal the actual cash.", dialogClose: "Close"
  },
} as const;

const movementTypes = ["CashIn", "CashOut", "PettyCash", "CashDrop"] as const;

export function ShiftsSection({ language }: { language: Language }) {
  const t = copy[language];
  const name = (x: { nameAr: string; nameEn: string }) => (language === "ar" ? x.nameAr : x.nameEn);
  const money = (v: number | null | undefined) => v === null || v === undefined ? "—" : new Intl.NumberFormat(language, { minimumFractionDigits: 0, maximumFractionDigits: 4 }).format(v);
  const movementLabel = (m: string) => m === "CashIn" ? t.cashIn : m === "CashOut" ? t.cashOut : m === "PettyCash" ? t.pettyCash : m === "CashDrop" ? t.cashDrop : m;
  const shiftStatusLabel = (s: string) => s === "Open" ? t.statusOpen : s === "Closed" ? t.statusClosed : s === "Reviewed" ? t.statusReviewed : s;
  const reviewStatusLabel = (s: string) => s === "Approved" ? t.revApproved : s === "Rejected" ? t.revRejected : s === "NotReviewed" ? t.revNotReviewed : s;

  const [branches, setBranches] = useState<Branch[]>([]);
  const [branchId, setBranchId] = useState("");
  const [current, setCurrent] = useState<CurrentShift | null>(null);
  const [loading, setLoading] = useState(false);
  const [openForm, setOpenForm] = useState({ openingCash: "" });
  const [movementForm, setMovementForm] = useState({ type: "CashIn" as Movement["type"], amount: "", reason: "", note: "" });
  const [denom, setDenom] = useState<Record<string, string>>({});
  const [blind, setBlind] = useState({ actualCash: "", cardTotal: "" });
  const [dialog, setDialog] = useState<"open" | "movement" | "close" | null>(null);
  const [closeResult, setCloseResult] = useState<CloseResult | null>(null);
  const [history, setHistory] = useState<ShiftRow[]>([]);
  const [canReview, setCanReview] = useState(false);
  const [message, setMessage] = useState("");
  const [isError, setIsError] = useState(false);
  const [reviewNote, setReviewNote] = useState<Record<string, string>>({});

  const auth = (path: string, init?: RequestInit) => fetch(path, { ...init, headers: { "Content-Type": "application/json", Authorization: `Bearer ${store.get<string>("session-token") ?? ""}`, ...(init?.headers ?? {}) } });
  const setMsg = (value: string, error = false) => { setMessage(value); setIsError(error); };

  async function loadCurrent(id: string) {
    const [currentResponse, historyResponse] = await Promise.all([auth(`/api/v1/shifts/current?branchId=${id}`), auth(`/api/v1/shifts?branchId=${id}`)]);
    if (currentResponse.ok) { const value = await currentResponse.json() as { shift: CurrentShift | null }; setCurrent(value.shift); }
    if (historyResponse.ok) { setHistory(await historyResponse.json() as ShiftRow[]); setCanReview(true); } else { setHistory([]); setCanReview(false); }
  }

  useEffect(() => { void (async () => { const response = await auth("/api/v1/pos/context"); if (!response.ok) return; const value = await response.json() as { branches: Branch[] }; setBranches(value.branches); if (value.branches[0]) { setBranchId(value.branches[0].id); await loadCurrent(value.branches[0].id); } })(); }, []);
  useEffect(() => { if (branchId) void loadCurrent(branchId); }, [branchId]);
  function changeBranch(id: string) { setBranchId(id); setCloseResult(null); setDialog(null); setBlind({ actualCash: "", cardTotal: "" }); setDenom({}); setMsg(""); }

  async function openShift(event: React.FormEvent) { event.preventDefault(); setMsg(""); setLoading(true); try { const response = await auth("/api/v1/shifts", { method: "POST", body: JSON.stringify({ branchId, openingCash: Number(openForm.openingCash) || 0 }) }); if (!response.ok) throw new Error(t.failed); setOpenForm({ openingCash: "" }); setCloseResult(null); setDialog(null); setBlind({ actualCash: "", cardTotal: "" }); setDenom({}); setMsg(t.openSuccess); await loadCurrent(branchId); } catch { setMsg(t.failed, true); } finally { setLoading(false); } }

  async function addMovement(event: React.FormEvent) { event.preventDefault(); if (!current) return; setMsg(""); setLoading(true); try { const response = await auth(`/api/v1/shifts/${current.id}/movements`, { method: "POST", body: JSON.stringify({ type: movementForm.type, amount: Number(movementForm.amount), reason: movementForm.reason.trim() || null, note: movementForm.note.trim() || null }) }); if (!response.ok) throw new Error(t.failed); setMovementForm({ type: "CashIn", amount: "", reason: "", note: "" }); setDialog(null); await loadCurrent(branchId); } catch { setMsg(t.failed, true); } finally { setLoading(false); } }

  const denominationTotal = denominationCounts(denom);
  async function blindClose(event: React.FormEvent) { event.preventDefault(); if (!current) return; setMsg(""); setLoading(true); try { const counts = denominations.map((d) => ({ denomination: d, count: Number(denom[d] ?? 0) })); const response = await auth(`/api/v1/shifts/${current.id}/blind-close`, { method: "POST", body: JSON.stringify({ actualCash: Number(blind.actualCash), actualCardTotal: Number(blind.cardTotal) || 0, denominations: counts }) }); if (!response.ok) { const problem = await response.json().catch(() => null); throw new Error(problem?.errors?.denominations?.[0] ?? problem?.errors?.cash?.[0] ?? problem?.errors?.shift?.[0] ?? t.failed); } setCloseResult(await response.json() as CloseResult); setDialog(null); setMsg(t.blindClose); await loadCurrent(branchId); } catch (e) { setMsg(e instanceof Error ? e.message : t.failed, true); } finally { setLoading(false); } }

  async function review(id: string, status: "Approved" | "Rejected") { setMsg(""); const response = await auth(`/api/v1/shifts/${id}/review`, { method: "POST", body: JSON.stringify({ status, note: reviewNote[id]?.trim() || null }) }); if (!response.ok) { const problem = await response.json().catch(() => null); setMsg(problem?.errors?.shift?.[0] ?? problem?.errors?.status?.[0] ?? t.failed, true); return; } setMsg(t.reviewed); await loadCurrent(branchId); }

  const displayCurrent = current;

  return (
    <div>
      <p className="text-sm font-semibold text-[#0e5a4f]">{t.title}</p>
      <h1 className="mt-2 text-2xl font-semibold tracking-tight sm:text-3xl">{t.title}</h1>
      <div className="mt-5 max-w-md">
        <label className="block text-sm font-medium">{t.branch}
          <select value={branchId} onChange={(e) => changeBranch(e.target.value)} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20">{branches.map((branch) => <option key={branch.id} value={branch.id}>{name(branch)}</option>)}</select>
        </label>
      </div>

      {message && <p role={isError ? "alert" : "status"} className={`mt-4 text-sm ${isError ? "text-[#b4322a]" : "text-[#137347]"}`}>{message}</p>}

      <div className="mt-6 grid gap-5 xl:grid-cols-2">
        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          {closeResult ? (
            <div className="rounded-lg border border-[#0e5a4f]/25 bg-[#edf5f1] p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 className="font-semibold text-[#08483f]">{t.closeResult}</h2>
                <span className="rounded-full bg-[#e8ece8] px-3 py-1 text-xs font-semibold text-[#53615b]">{shiftStatusLabel(closeResult.status)}</span>
              </div>
              <dl className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
                <Metric label={t.expectedCash} value={money(closeResult.expectedCash)} />
                <Metric label={t.actual} value={money(closeResult.actualCash)} />
                <Metric label={t.cashVariance} value={money(closeResult.cashVariance)} tone={closeResult.cashVariance >= 0 ? "good" : "bad"} />
                <Metric label={t.cardVariance} value={money(closeResult.cardVariance)} tone={closeResult.cardVariance >= 0 ? "good" : "bad"} />
                <Metric label={t.amount} value={money(closeResult.denominationTotal)} />
              </dl>
              <p className="mt-4 text-sm text-[#69766f]">{t.closeAt}: {new Intl.DateTimeFormat(language, { dateStyle: "medium", timeStyle: "short" }).format(new Date(closeResult.closedAt))}</p>
            </div>
          ) : displayCurrent && displayCurrent.status === "Open" ? (
            <>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 className="font-semibold">{t.currentShift}</h2>
                <span className="rounded-full bg-[#e3f4ea] px-3 py-1 text-xs font-semibold text-[#137347]">{shiftStatusLabel(displayCurrent.status)}</span>
              </div>
              <dl className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
                <Metric label={t.openingCash} value={money(displayCurrent.openingCash)} />
                <Metric label={t.cashSales} value={money(displayCurrent.cashSales)} />
                <Metric label={t.cardSales} value={money(displayCurrent.cardSales)} />
                <Metric label={t.cashRefunds} value={money(displayCurrent.cashRefunds)} />
              </dl>

              <div className="mt-5">
                <div className="flex flex-wrap items-center justify-between gap-3"><h3 className="font-semibold">{t.movements}</h3><button onClick={() => setDialog("movement")} className="min-h-10 rounded-lg bg-[#0e5a4f] px-4 text-sm font-semibold text-white">{t.addMovement}</button></div>
                {displayCurrent.movements.length === 0 ? <p className="mt-2 text-sm text-[#69766f]">{t.none}</p> : (
                  <ul className="mt-2 divide-y divide-[#e8ece8]">
                    {displayCurrent.movements.slice().reverse().map((m) => <li key={m.id} className="flex flex-wrap items-center justify-between gap-2 py-2 text-sm"><span className="rounded-full bg-[#edf5f1] px-2 py-1 text-xs font-medium text-[#0e5a4f]">{movementLabel(m.type)}</span><span className="font-medium">{money(m.amount)}</span><span className="text-[#69766f]">{m.reason ?? m.note ?? ""}</span></li>)}
                  </ul>
                )}
              </div>

              <button onClick={() => setDialog("close")} className="mt-5 min-h-11 rounded-lg border border-[#0e5a4f] px-4 font-semibold text-[#0e5a4f] hover:bg-[#edf5f1]">{t.blindClose}</button>
            </>
          ) : (
            <div className="p-1">
              <div className="flex items-center justify-between gap-2">
                <h2 className="font-semibold">{t.open}</h2>
                <span className="rounded-full bg-[#fbe4e2] px-3 py-1 text-xs font-semibold text-[#9b2922]">{t.noShift}</span>
              </div>
              <button onClick={() => setDialog("open")} className="mt-4 min-h-12 w-full rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white hover:bg-[#08483f]">{t.open}</button>
            </div>
          )}
        </section>

        <section className="rounded-xl border border-[#dfe5df] bg-white p-5">
          <h2 className="font-semibold">{t.history}</h2>
          {!canReview ? <p className="mt-3 text-sm text-[#69766f]">{t.none}</p> : history.length === 0 ? <p className="mt-3 text-sm text-[#69766f]">{t.none}</p> : (
            <ul className="mt-3 divide-y divide-[#e8ece8]">
              {history.map((row) => (
                <li key={row.id} className="flex flex-wrap items-center justify-between gap-3 py-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={`rounded-full px-2 py-1 text-xs font-semibold ${row.status === "Open" ? "bg-[#e3f4ea] text-[#137347]" : "bg-[#e8ece8] text-[#53615b]"}`}>{shiftStatusLabel(row.status)}</span>
                      {row.status !== "Open" && <span className={`rounded-full px-2 py-1 text-xs font-semibold ${row.reviewStatus === "Approved" ? "bg-[#e3f4ea] text-[#137347]" : row.reviewStatus === "Rejected" ? "bg-[#fbe4e2] text-[#b4322a]" : "bg-[#f4f1e3] text-[#8a6d1f]"}`}>{reviewStatusLabel(row.reviewStatus)}</span>}
                    </div>
                    <p className="mt-1 text-sm text-[#69766f]">{t.openAt}: {new Intl.DateTimeFormat(language, { dateStyle: "medium", timeStyle: "short" }).format(new Date(row.openedAt))}</p>
                    {row.status === "Open" && <span className="mt-1 inline-block text-sm font-medium text-[#0e5a4f]">{money(row.openingCash)}</span>}
                    {row.status !== "Open" && <div className="mt-1 flex flex-wrap gap-4 text-sm"><span className="text-[#69766f]">{t.expectedCash}: {money(row.expectedCash)}</span><span className="text-[#69766f]">{t.actual}: {money(row.actualCash)}</span><span className={`font-semibold ${(row.cashVariance ?? 0) >= 0 ? "text-[#137347]" : "text-[#b4322a]"}`}>{t.cashVariance}: {money(row.cashVariance)}</span></div>}
                  </div>
                  {row.status !== "Open" && row.reviewStatus === "NotReviewed" && (
                    <div className="flex items-center gap-2">
                      <input value={reviewNote[row.id] ?? ""} onChange={(e) => setReviewNote({ ...reviewNote, [row.id]: e.target.value })} placeholder={t.note} maxLength={500} className="w-36 min-h-10 rounded-lg border border-[#cdd7d0] px-3 text-sm" />
                      <button onClick={() => void review(row.id, "Approved")} className="min-h-10 rounded-lg bg-[#0e5a4f] px-3 text-sm font-semibold text-white">{t.approve}</button>
                      <button onClick={() => void review(row.id, "Rejected")} className="min-h-10 rounded-lg border border-[#b4322a] px-3 text-sm font-semibold text-[#b4322a]">{t.reject}</button>
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
      {dialog === "open" && <FormDialog title={t.open} closeLabel={t.dialogClose} onClose={() => setDialog(null)} width="max-w-md"><form onSubmit={openShift}><label className="block text-sm font-medium">{t.openingCash}<input required type="number" min="0" step="0.001" value={openForm.openingCash} onChange={(e) => setOpenForm({ openingCash: e.target.value })} className="mt-2 min-h-12 w-full rounded-lg border border-[#cdd7d0] px-3" /></label><button disabled={loading} className="mt-4 min-h-12 w-full rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white disabled:opacity-60">{loading ? t.saving : t.open}</button></form></FormDialog>}
      {dialog === "movement" && <FormDialog title={t.addMovement} closeLabel={t.dialogClose} onClose={() => setDialog(null)} width="max-w-xl"><form onSubmit={addMovement} className="grid gap-3 sm:grid-cols-2"><label className="block text-sm font-medium">{t.movementType}<select value={movementForm.type} onChange={(e) => setMovementForm({ ...movementForm, type: e.target.value as Movement["type"] })} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3">{movementTypes.map((m) => <option key={m} value={m}>{movementLabel(m)}</option>)}</select></label><label className="block text-sm font-medium">{t.amount}<input required type="number" min="0.001" step="0.001" value={movementForm.amount} onChange={(e) => setMovementForm({ ...movementForm, amount: e.target.value })} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3" /></label><label className="block text-sm font-medium">{t.reason}<input value={movementForm.reason} onChange={(e) => setMovementForm({ ...movementForm, reason: e.target.value })} maxLength={200} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3" /></label><label className="block text-sm font-medium">{t.note}<input value={movementForm.note} onChange={(e) => setMovementForm({ ...movementForm, note: e.target.value })} maxLength={500} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3" /></label><button disabled={loading} className="min-h-11 justify-self-start rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white disabled:opacity-60">{loading ? t.saving : t.addMovement}</button></form></FormDialog>}
      {dialog === "close" && <FormDialog title={t.blindClose} closeLabel={t.dialogClose} onClose={() => setDialog(null)}><form onSubmit={blindClose}><div className="grid gap-3 sm:grid-cols-2"><label className="block text-sm font-medium">{t.actualCash}<input required type="number" min="0" step="0.001" value={blind.actualCash} onChange={(e) => setBlind({ ...blind, actualCash: e.target.value })} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3" /></label><label className="block text-sm font-medium">{t.cardTotal}<input required type="number" min="0" step="0.001" value={blind.cardTotal} onChange={(e) => setBlind({ ...blind, cardTotal: e.target.value })} className="mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] px-3" /></label></div><div className="mt-4"><h4 className="text-sm font-semibold">{t.denominationsTitle}</h4><p className="mt-1 text-xs text-[#69766f]">{t.denominationsHint}</p><div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3">{denominations.map((d) => <label key={d} className="flex items-center justify-between gap-2 rounded-lg border border-[#cdd7d0] px-3 py-2 text-sm"><span>{money(d)}</span><input type="number" min="0" step="1" value={denom[String(d)] ?? ""} onChange={(e) => setDenom({ ...denom, [String(d)]: e.target.value })} className="w-16 rounded border border-[#cdd7d0] px-2 py-1 text-right" /></label>)}</div><p className="mt-3 text-sm font-medium">{t.counted}: {money(denominationTotal)}</p>{Math.abs(denominationTotal - (Number(blind.actualCash) || 0)) > 0.0001 && <p className="mt-1 text-xs text-[#b4322a]">{t.needActualCashMatch}</p>}</div><button disabled={loading} className="mt-4 min-h-11 rounded-lg bg-[#0e5a4f] px-4 font-semibold text-white disabled:opacity-60">{loading ? t.saving : t.close}</button></form></FormDialog>}
    </div>
  );
}

function denominationCounts(denom: Record<string, string>): number {
  return denominations.reduce((sum, d) => sum + (Number(denom[String(d)]) || 0) * d, 0);
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: "good" | "bad" }) {
  return (
    <div className="rounded-lg border border-[#e8ece8] bg-white p-3">
      <p className="text-xs font-medium text-[#69766f]">{label}</p>
      <p key={tone} className={`mt-1 text-lg font-semibold ${tone === "good" ? "text-[#137347]" : tone === "bad" ? "text-[#b4322a]" : "text-[#17211f]"}`}>{value}</p>
    </div>
  );
}
