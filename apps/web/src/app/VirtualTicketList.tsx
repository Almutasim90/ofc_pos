import { useLayoutEffect, useRef, useState, type ReactNode } from "react";

// Fixed row geometry keeps layout work bounded even for several hundred ticket lines.
const ROW_HEIGHT = 100;
const OVERSCAN = 3;

export function VirtualTicketList<T extends { key: string }>({
  lines,
  label,
  empty,
  renderLine,
}: {
  lines: T[];
  label: string;
  empty: string;
  renderLine: (line: T) => ReactNode;
}) {
  const viewport = useRef<HTMLDivElement>(null);
  const [window, setWindow] = useState({ top: 0, height: 336 });
  const [focusedKey, setFocusedKey] = useState<string | null>(null);
  const followEnd = useRef(true);
  const previousCount = useRef(lines.length);
  useLayoutEffect(() => {
    const element = viewport.current;
    if (!element) return;
    const measure = () =>
      setWindow({ top: element.scrollTop, height: element.clientHeight });
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    measure();
    return () => observer.disconnect();
  }, []);
  useLayoutEffect(() => {
    const element = viewport.current;
    if (element && lines.length > previousCount.current && followEnd.current) {
      element.scrollTop = element.scrollHeight;
      setWindow({ top: element.scrollTop, height: element.clientHeight });
    }
    previousCount.current = lines.length;
  }, [lines.length]);
  const start = Math.max(
    0,
    Math.min(lines.length - 1, Math.floor(window.top / ROW_HEIGHT) - OVERSCAN),
  );
  const end = Math.min(
    lines.length,
    Math.ceil((window.top + window.height) / ROW_HEIGHT) + OVERSCAN,
  );
  const indices = Array.from(
    { length: Math.max(0, end - start) },
    (_, offset) => start + offset,
  );
  // Keep the actively edited row mounted when the operator scrolls with a finger/wheel.
  const focusedIndex = focusedKey
    ? lines.findIndex((line) => line.key === focusedKey)
    : -1;
  if (focusedIndex >= 0 && !indices.includes(focusedIndex))
    indices.push(focusedIndex);
  indices.sort((a, b) => a - b);
  return (
    <div
      className="pos-ticket-scroll"
      ref={viewport}
      tabIndex={0}
      aria-label={label}
      onScroll={(event) => {
        const element = event.currentTarget;
        followEnd.current =
          element.scrollHeight - element.scrollTop - element.clientHeight <
          ROW_HEIGHT;
        setWindow({ top: element.scrollTop, height: element.clientHeight });
      }}
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null))
          setFocusedKey(null);
      }}
    >
      {!lines.length ? (
        <p className="pos-ticket-empty">{empty}</p>
      ) : (
        <div
          role="list"
          aria-label={label}
          className="pos-ticket-space"
          style={{ height: lines.length * ROW_HEIGHT }}
        >
          {indices.map((index) => (
            <div
              key={lines[index].key}
              role="listitem"
              aria-posinset={index + 1}
              aria-setsize={lines.length}
              className="pos-ticket-slot"
              style={{ top: index * ROW_HEIGHT, height: ROW_HEIGHT }}
              onFocus={() => setFocusedKey(lines[index].key)}
            >
              {renderLine(lines[index])}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
