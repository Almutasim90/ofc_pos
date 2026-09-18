import { useEffect, useMemo, useRef, useState } from "react";

type Option = { value: string; label: string; disabled?: boolean };

// An <option>'s children is often several nodes (e.g. `{sku} · {name}` is three children:
// a string, the literal " · ", and another string), not one plain string — join every
// primitive descendant instead of only handling the single-string-child case, otherwise a
// multi-part label silently falls back to showing the raw id.
function childrenToText(node: React.ReactNode): string {
  if (node === null || node === undefined || typeof node === "boolean") return "";
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (Array.isArray(node)) return node.map(childrenToText).join("");
  if (typeof node === "object" && "props" in node) return childrenToText((node as { props: { children?: React.ReactNode } }).props.children);
  return "";
}

function optionsFromChildren(children: React.ReactNode): Option[] {
  const options: Option[] = [];
  for (const child of Array.isArray(children) ? children.flat(Infinity) : [children]) {
    if (!child || typeof child !== "object" || !("props" in child)) continue;
    const props = (child as { props: { value?: string; children?: React.ReactNode; disabled?: boolean } }).props;
    if (props.value === undefined) continue;
    const label = childrenToText(props.children).trim();
    options.push({ value: String(props.value), label: label || String(props.value), disabled: props.disabled });
  }
  return options;
}

const boxClass = "mt-2 min-h-11 w-full rounded-lg border border-[#cdd7d0] bg-white px-3 outline-none focus:border-[#0e5a4f] focus:ring-2 focus:ring-[#0e5a4f]/20 disabled:cursor-not-allowed disabled:opacity-60";

export function SearchableSelect({ label, value, onChange, children, disabled = false, placeholder, hideLabel = false }: { label: string; value: string; onChange: (value: string) => void; children: React.ReactNode; disabled?: boolean; placeholder?: string; hideLabel?: boolean }) {
  const options = useMemo(() => optionsFromChildren(children), [children]);
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [activeIndex, setActiveIndex] = useState(0);
  const rootRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const uid = useRef(`ss-${Math.random().toString(36).slice(2, 9)}`).current;

  const selected = options.find((o) => o.value === value) ?? null;
  const filtered = query.trim() === "" ? options : options.filter((o) => o.label.toLowerCase().includes(query.trim().toLowerCase()));

  useEffect(() => {
    if (!open) return;
    function onDocClick(e: MouseEvent) { if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false); }
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, [open]);

  useEffect(() => {
    if (open) { setQuery(""); setActiveIndex(Math.max(0, filtered.findIndex((o) => o.value === value))); requestAnimationFrame(() => inputRef.current?.focus()); }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => { setActiveIndex(0); }, [query]);

  useEffect(() => {
    if (!open) return;
    const el = listRef.current?.querySelector(`[data-index="${activeIndex}"]`);
    el?.scrollIntoView({ block: "nearest" });
  }, [activeIndex, open]);

  function choose(option: Option) {
    if (option.disabled) return;
    onChange(option.value);
    setOpen(false);
  }

  function onKeyDown(e: React.KeyboardEvent) {
    if (!open) {
      if (e.key === "ArrowDown" || e.key === "Enter" || e.key === " ") { e.preventDefault(); setOpen(true); }
      return;
    }
    if (e.key === "Escape") { e.preventDefault(); setOpen(false); return; }
    if (e.key === "ArrowDown") { e.preventDefault(); setActiveIndex((i) => Math.min(i + 1, filtered.length - 1)); return; }
    if (e.key === "ArrowUp") { e.preventDefault(); setActiveIndex((i) => Math.max(i - 1, 0)); return; }
    if (e.key === "Enter") { e.preventDefault(); const opt = filtered[activeIndex]; if (opt) choose(opt); return; }
    if (e.key === "Tab") setOpen(false);
  }

  return <div ref={rootRef} className="relative min-w-0">
    <span className={hideLabel ? "sr-only" : "block text-sm font-medium"} id={`${uid}-label`}>{label}</span>
    <button type="button" disabled={disabled} onClick={() => setOpen((v) => !v)} onKeyDown={onKeyDown}
      role="combobox" aria-expanded={open} aria-haspopup="listbox" aria-controls={`${uid}-list`} aria-labelledby={`${uid}-label`}
      className={`${boxClass} ${hideLabel ? "mt-0" : ""} flex items-center justify-between gap-2 text-start`}>
      <span className={`truncate ${selected ? "" : "text-[#8b968f]"}`}>{selected ? selected.label : (placeholder ?? "—")}</span>
      <svg aria-hidden="true" viewBox="0 0 20 20" className={`size-4 shrink-0 text-[#69766f] transition-transform ${open ? "rotate-180" : ""}`}><path d="M5.5 7.5l4.5 4.5 4.5-4.5" stroke="currentColor" strokeWidth="1.5" fill="none" strokeLinecap="round" strokeLinejoin="round" /></svg>
    </button>
    {open && <div className="absolute inset-x-0 z-20 mt-1 min-w-0 overflow-hidden rounded-lg border border-[#cdd7d0] bg-white shadow-lg">
      <div className="border-b border-[#e8ece8] p-2">
        <input ref={inputRef} value={query} onChange={(e) => setQuery(e.target.value)} onKeyDown={onKeyDown}
          role="searchbox" aria-autocomplete="list" aria-controls={`${uid}-list`}
          placeholder="بحث… / Search…" className="min-h-9 w-full rounded-md border border-[#cdd7d0] px-2 text-sm outline-none focus:border-[#0e5a4f]" />
      </div>
      <ul ref={listRef} id={`${uid}-list`} role="listbox" aria-labelledby={`${uid}-label`} className="max-h-60 overflow-y-auto py-1 text-sm">
        {filtered.length === 0 ? <li className="px-3 py-2 text-[#69766f]">{"لا توجد نتائج / No results"}</li> : filtered.map((o, i) => (
          <li key={o.value} data-index={i} role="option" aria-selected={o.value === value}
            onMouseEnter={() => setActiveIndex(i)} onClick={() => choose(o)}
            className={`cursor-pointer px-3 py-2 ${o.disabled ? "cursor-not-allowed opacity-50" : i === activeIndex ? "bg-[#e6f1ec]" : ""} ${o.value === value ? "font-semibold text-[#08483f]" : ""}`}>
            {o.label}
          </li>
        ))}
      </ul>
    </div>}
  </div>;
}
