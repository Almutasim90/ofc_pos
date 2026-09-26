import { useId, useRef, useState, type ReactNode } from "react";

// Dashboard charts drawn with plain HTML/CSS so they follow the app theme, dark mode and RTL without a
// charting dependency. Specs: bars <= 24px thick with a 4px rounded data end, 1px recessive gridlines,
// one hue per magnitude chart, validated categorical slots for part-to-whole, a tooltip on every mark
// (hover and keyboard focus) and a table view so no value is reachable only by hovering.

type Tip = { x: number; y: number; value: string; label: string } | null;

function useTooltip() {
  const frame = useRef<HTMLDivElement>(null);
  const [tip, setTip] = useState<Tip>(null);
  const show = (target: HTMLElement, value: string, label: string) => {
    const box = frame.current?.getBoundingClientRect();
    const mark = target.getBoundingClientRect();
    if (!box) return;
    setTip({
      x: mark.left + mark.width / 2 - box.left,
      y: mark.top - box.top,
      value,
      label,
    });
  };
  const node = tip && (
    <div
      className="viz-tooltip"
      role="presentation"
      style={{ left: tip.x, top: tip.y }}
    >
      <strong>{tip.value}</strong>
      <span>{tip.label}</span>
    </div>
  );
  return { frame, show, hide: () => setTip(null), node };
}

function markHandlers(
  show: (target: HTMLElement, value: string, label: string) => void,
  hide: () => void,
  value: string,
  label: string,
) {
  return {
    tabIndex: 0,
    "aria-label": `${label}: ${value}`,
    onPointerEnter: (e: React.PointerEvent<HTMLElement>) =>
      show(e.currentTarget, value, label),
    onFocus: (e: React.FocusEvent<HTMLElement>) =>
      show(e.currentTarget, value, label),
    onPointerLeave: hide,
    onBlur: hide,
  };
}

export function ChartCard({
  title,
  subtitle,
  table,
  tableLabel,
  wide,
  children,
}: {
  wide?: boolean;
  title: string;
  subtitle?: string;
  table?: { head: string[]; rows: string[][] };
  tableLabel: string;
  children: ReactNode;
}) {
  return (
    <section className={`viz-card ${wide ? "viz-span-2" : ""}`}>
      <header>
        <h3>{title}</h3>
        {subtitle && <p>{subtitle}</p>}
      </header>
      {children}
      {table && table.rows.length > 0 && (
        <details className="viz-table">
          <summary>{tableLabel}</summary>
          <table>
            <thead>
              <tr>
                {table.head.map((h) => (
                  <th key={h}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {table.rows.map((row, i) => (
                <tr key={i}>
                  {row.map((cell, j) => (
                    <td key={j}>{cell}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </details>
      )}
    </section>
  );
}

export function EmptyChart({ text }: { text: string }) {
  return <p className="viz-empty">{text}</p>;
}

export function slotClass(key: string, index: number) {
  return key === "other" ? "viz-cat-other" : `viz-cat-${(index % 8) + 1}`;
}

function niceMax(value: number) {
  if (value <= 0) return 1;
  const power = 10 ** Math.floor(Math.log10(value));
  const step = [1, 2, 2.5, 5, 10].find((s) => s * power >= value)!;
  return step * power;
}

// Columns from a shared baseline. Time series: one hue, the peak labelled. Categories (`categorical`):
// one validated slot per column, every cap labelled and every name shown, so colour is never the only key.
export function ColumnChart({
  points,
  format,
  formatAxis,
  label,
  categorical = false,
}: {
  points: Array<{ key: string; label: string; value: number }>;
  format: (v: number) => string;
  formatAxis: (v: number) => string;
  label: string;
  categorical?: boolean;
}) {
  const { frame, show, hide, node } = useTooltip();
  const max = niceMax(Math.max(...points.map((p) => p.value), 0));
  const ticks = [max, max / 2, 0];
  const peak = points.reduce(
    (best, p, i) => (p.value > (points[best]?.value ?? -1) ? i : best),
    0,
  );
  // Label every nth tick so the time axis never collides.
  const every = categorical ? 1 : Math.max(1, Math.ceil(points.length / 8));
  // Time reads left to right in both languages (categories follow the page direction); the axis, gridlines and columns share one plot box so
  // tick labels line up exactly with their gridlines.
  return (
    <div
      className={`viz-columns ${categorical ? "viz-columns-categories" : ""}`}
      ref={frame}
      aria-label={label}
      role="group"
      dir={categorical ? undefined : "ltr"}
    >
      <div className="viz-y-axis" aria-hidden="true">
        {ticks.map((tick) => (
          <span key={tick} style={{ bottom: `${(tick / max) * 100}%` }}>
            {formatAxis(tick)}
          </span>
        ))}
      </div>
      <div className="viz-plot">
        {ticks.map((tick) => (
          <i
            key={tick}
            className="viz-grid"
            style={{ bottom: `${(tick / max) * 100}%` }}
          />
        ))}
        {points.map((point, i) => (
          <div key={point.key} className="viz-column-slot">
            <div
              className={`viz-column ${categorical ? slotClass(point.key, i) : ""}`}
              style={{ height: `${(point.value / max) * 100}%` }}
              {...markHandlers(show, hide, format(point.value), point.label)}
            >
              {categorical ? (
                <span className="viz-cap">{format(point.value)}</span>
              ) : (
                i === peak &&
                point.value > 0 && (
                  <span className="viz-peak">{format(point.value)}</span>
                )
              )}
            </div>
          </div>
        ))}
      </div>
      <span />
      <div className="viz-x-axis" aria-hidden="true">
        {points.map((point, i) => (
          <span key={point.key} title={point.label}>
            {i % every === 0 ? point.label : ""}
          </span>
        ))}
      </div>
      {node}
    </div>
  );
}

// Ranked magnitude: horizontal bars, value at the tip.
export function BarList({
  items,
  format,
  categorical = false,
}: {
  items: Array<{ key: string; label: string; value: number }>;
  format: (v: number) => string;
  categorical?: boolean;
}) {
  const { frame, show, hide, node } = useTooltip();
  const max = Math.max(...items.map((x) => x.value), 0) || 1;
  return (
    <div className="viz-bars" ref={frame}>
      {items.map((item, i) => (
        <div key={item.key} className="viz-bar-row">
          <span className="viz-bar-label" title={item.label}>
            {item.label}
          </span>
          <div className="viz-bar-track">
            <div
              className={`viz-bar ${categorical ? slotClass(item.key, i) : ""}`}
              style={{ width: `${Math.max(2, (item.value / max) * 100)}%` }}
              {...markHandlers(show, hide, format(item.value), item.label)}
            />
            <span className="viz-bar-value">{format(item.value)}</span>
          </div>
        </div>
      ))}
      {node}
    </div>
  );
}

// Part-to-whole: one 100% bar with a 2px surface gap between segments, legend carrying name, value, share.
export function ShareBar({
  items,
  format,
}: {
  items: Array<{ key: string; label: string; value: number }>;
  format: (v: number) => string;
}) {
  const { frame, show, hide, node } = useTooltip();
  const total = items.reduce((sum, x) => sum + x.value, 0) || 1;
  const pct = (v: number) => `${Math.round((v / total) * 100)}%`;
  return (
    <div ref={frame} className="viz-share">
      <div className="viz-share-bar">
        {items.map((item, i) => (
          <div
            key={item.key}
            className={`viz-share-segment ${slotClass(item.key, i)}`}
            style={{ flexGrow: item.value }}
            {...markHandlers(
              show,
              hide,
              `${format(item.value)} · ${pct(item.value)}`,
              item.label,
            )}
          />
        ))}
      </div>
      <ul className="viz-legend">
        {items.map((item, i) => (
          <li key={item.key}>
            <i
              className={`viz-swatch ${slotClass(item.key, i)}`}
              aria-hidden="true"
            />
            <span>{item.label}</span>
            <strong>{format(item.value)}</strong>
            <span className="viz-muted">{pct(item.value)}</span>
          </li>
        ))}
      </ul>
      {node}
    </div>
  );
}

// A single ratio against limits: the fill carries severity, the track is a lighter step of the same hue.
export function Meter({
  value,
  warnAt,
  dangerAt,
  label,
  states,
}: {
  value: number;
  warnAt: number;
  dangerAt: number;
  label: string;
  states: { good: string; warn: string; danger: string };
}) {
  const id = useId();
  const level =
    value >= dangerAt ? "danger" : value >= warnAt ? "warn" : "good";
  const shown = Math.min(1, value / Math.max(dangerAt * 2, 0.0001));
  return (
    <div className={`viz-meter viz-meter-${level}`}>
      <div className="viz-meter-head">
        <strong id={id}>{`${(value * 100).toFixed(1)}%`}</strong>
        <span className="viz-meter-state">
          <i aria-hidden="true" />
          {states[level]}
        </span>
      </div>
      <div
        className="viz-meter-track"
        role="meter"
        aria-labelledby={id}
        aria-label={label}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(value * 1000) / 10}
      >
        <div className="viz-meter-fill" style={{ width: `${shown * 100}%` }} />
      </div>
    </div>
  );
}
