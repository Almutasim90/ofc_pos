import { ChevronLeft, ChevronRight } from "lucide-react";
import { Button } from "@/components/ui/button";

export const PAGE_SIZE = 10;

export function Pagination({ page, pageSize, total, onPageChange, language }: { page: number; pageSize: number; total: number; onPageChange: (page: number) => void; language: "ar" | "en" }) {
  const ar = language === "ar";
  const tr = (a: string, e: string) => ar ? a : e;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (totalPages <= 1) return null;
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);
  return <nav aria-label={tr("ترقيم الصفحات", "Pagination")} className="flex flex-wrap items-center justify-between gap-3 border-t border-[#e8ece8] pt-4">
    <p className="text-sm text-[#000000]" aria-live="polite">{tr(`عرض ${from}–${to} من ${total}`, `Showing ${from}–${to} of ${total}`)}</p>
    <div className="flex items-center gap-2">
      <Button type="button" variant="outline" size="icon" disabled={page <= 1} onClick={() => onPageChange(page - 1)} aria-label={tr("الصفحة السابقة", "Previous page")}>{ar ? <ChevronRight size={16} /> : <ChevronLeft size={16} />}</Button>
      <span className="min-w-16 text-center text-sm font-medium" aria-current="page">{page} / {totalPages}</span>
      <Button type="button" variant="outline" size="icon" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} aria-label={tr("الصفحة التالية", "Next page")}>{ar ? <ChevronLeft size={16} /> : <ChevronRight size={16} />}</Button>
    </div>
  </nav>;
}
