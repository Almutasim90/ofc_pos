import type { ReactNode } from "react";
import { X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";

export function FormDialog({ title, closeLabel, onClose, children, width = "max-w-3xl" }: { title: string; closeLabel: string; onClose: () => void; children: ReactNode; width?: string }) {
  return <Dialog open onOpenChange={(open) => { if (!open) onClose(); }}>
    <DialogContent showCloseButton={false} className={`top-auto bottom-0 left-0 flex max-h-[100dvh] min-w-0 w-full max-w-none translate-x-0 translate-y-0 flex-col gap-0 overflow-hidden rounded-b-none rounded-t-xl border bg-background p-0 sm:top-1/2 sm:bottom-auto sm:left-1/2 sm:max-h-[calc(100dvh-2.5rem)] sm:-translate-x-1/2 sm:-translate-y-1/2 sm:rounded-xl ${width}`}>
      <DialogHeader className="flex shrink-0 flex-row items-center justify-between gap-3 border-b px-4 py-3 text-start sm:px-5 sm:py-4">
        <DialogTitle className="min-w-0 break-words text-lg font-bold tracking-tight sm:text-xl">{title}</DialogTitle>
        <Button type="button" variant="outline" size="icon" onClick={onClose} aria-label={closeLabel} className="size-11 shrink-0"><X size={18} /></Button>
      </DialogHeader>
      <div className="min-h-0 min-w-0 flex-1 overscroll-contain overflow-x-hidden overflow-y-auto p-3 pb-[max(0.75rem,env(safe-area-inset-bottom))] sm:p-5">{children}</div>
    </DialogContent>
  </Dialog>;
}
