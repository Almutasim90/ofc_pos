import type { ReactNode } from "react";
import { X } from "lucide-react";

export function FormDialog({ title, closeLabel, onClose, children, width = "max-w-3xl" }: { title: string; closeLabel: string; onClose: () => void; children: ReactNode; width?: string }) {
  return <div className="fixed inset-0 z-50 grid place-items-end bg-black/35 sm:place-items-center sm:p-5" onMouseDown={(event) => { if (event.target === event.currentTarget) onClose(); }}>
    <section role="dialog" aria-modal="true" aria-label={title} className={`flex max-h-[94dvh] w-full ${width} flex-col overflow-hidden rounded-t-2xl bg-[#f5f6f2] shadow-2xl sm:rounded-2xl`}>
      <header className="flex items-center justify-between gap-4 border-b border-[#e8ece8] bg-white px-5 py-4">
        <h2 className="text-xl font-bold tracking-tight">{title}</h2>
        <button type="button" onClick={onClose} aria-label={closeLabel} className="grid size-11 shrink-0 place-items-center rounded-lg border border-[#cdd7d0] text-[#53615b] hover:bg-[#f2f5f2]"><X size={18} /></button>
      </header>
      <div className="min-h-0 flex-1 overflow-y-auto p-5">{children}</div>
    </section>
  </div>;
}
