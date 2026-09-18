import type { ReactNode } from "react";
import { X } from "lucide-react";

export function FormDialog({ title, closeLabel, onClose, children, width = "max-w-3xl" }: { title: string; closeLabel: string; onClose: () => void; children: ReactNode; width?: string }) {
  return <div className="fixed inset-0 z-50 grid min-w-0 place-items-end bg-black/35 sm:place-items-center sm:p-5" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <section role="dialog" aria-modal="true" aria-label={title} className={`flex max-h-[100dvh] min-w-0 w-full ${width} flex-col overflow-hidden rounded-t-2xl bg-[#f5f6f2] shadow-2xl sm:max-h-[calc(100dvh-2.5rem)] sm:rounded-2xl`}>
      <header className="flex shrink-0 items-center justify-between gap-3 border-b border-[#e8ece8] bg-white px-4 py-3 sm:px-5 sm:py-4">
        <h2 className="min-w-0 break-words text-lg font-bold tracking-tight sm:text-xl">{title}</h2>
        <button type="button" onClick={onClose} aria-label={closeLabel} className="grid size-11 shrink-0 place-items-center rounded-lg border border-[#cdd7d0] text-[#000000] hover:bg-[#f2f5f2]"><X size={18} /></button>
      </header>
      <div className="min-h-0 min-w-0 flex-1 overscroll-contain overflow-x-hidden overflow-y-auto p-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] sm:p-5">{children}</div>
    </section>
  </div>;
}
