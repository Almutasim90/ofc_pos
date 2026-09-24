import { useEffect, useEffectEvent } from "react";

/** Keyboard-wedge scanners: printable burst followed by Enter. Never swallow propagation. */
export function useBarcodeScanner(
  onScan: (code: string) => boolean,
  enabled: boolean,
) {
  const scan = useEffectEvent(onScan);
  useEffect(() => {
    if (!enabled) return;
    let buffer = "";
    let lastKeyAt = 0;
    const keydown = (event: KeyboardEvent) => {
      const target = event.target instanceof HTMLElement ? event.target : null;
      if (
        event.defaultPrevented ||
        event.isComposing ||
        event.repeat ||
        event.ctrlKey ||
        event.metaKey ||
        event.altKey ||
        target?.isContentEditable ||
        target?.closest('input, textarea, select, [role="textbox"]') ||
        document.querySelector(
          '[role="dialog"], [role="combobox"][aria-expanded="true"]',
        )
      ) {
        buffer = "";
        return;
      }
      const now = performance.now();
      if (now - lastKeyAt > 80) buffer = "";
      lastKeyAt = now;
      if (event.key === "Enter") {
        // Suppress only the matched scan's native button activation, not the event itself.
        if (buffer.length >= 3 && scan(buffer)) event.preventDefault();
        buffer = "";
      } else if (event.key.length === 1) {
        buffer = (buffer + event.key).slice(-128);
      } else if (event.key !== "Shift") {
        buffer = "";
      }
    };
    window.addEventListener("keydown", keydown, { capture: true });
    return () => window.removeEventListener("keydown", keydown, true);
  }, [enabled]);
}
