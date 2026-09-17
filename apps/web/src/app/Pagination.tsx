import { ChevronLeft, ChevronRight } from "lucide-react";

export const PAGE_SIZE = 10;

export function Pagination({ page, pageSize, total, onPageChange, language }: { page: number; pageSize: number; total: number; onPageChange: (page: number) => void; language: "ar" | "en" }) {
  const ar = language === "ar";
  const tr = (a: string, e: string) => ar ? a : e;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (totalPages <= 1) return null;
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);
  const navButton = "grid size-9 place-items-center rounded-lg border border-[#cdd7d0] hover:bg-[#f2f5f2] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#0e5a4f] focus-visible:ring-offset-1 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-transparent";
  return <nav aria-label={tr("ترقيم الصفحات", "Pagination")} className="flex flex-wrap items-center justify-between gap-3 border-t border-[#e8ece8] pt-4">
    <p className="text-sm text-[#000000]" aria-live="polite">{tr(`عرض ${from}–${to} من ${total}`, `Showing ${from}–${to} of ${total}`)}</p>
    <div className="flex items-center gap-2">
      <button type="button" disabled={page <= 1} onClick={() => onPageChange(page - 1)} className={navButton} aria-label={tr("الصفحة السابقة", "Previous page")}>{ar ? <ChevronRight size={16} /> : <ChevronLeft size={16} />}</button>
      <span className="min-w-16 text-center text-sm font-medium" aria-current="page">{page} / {totalPages}</span>
      <button type="button" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} className={navButton} aria-label={tr("الصفحة التالية", "Next page")}>{ar ? <ChevronLeft size={16} /> : <ChevronRight size={16} />}</button>
    </div>
  </nav>;
}
