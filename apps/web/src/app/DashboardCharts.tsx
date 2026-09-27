import { useId, useRef, useState, type ReactNode } from "react";

// Dashboard charts drawn with plain HTML/CSS so they follow the app theme, dark mode and RTL without a
// charting dependency. Specs: bars <= 24px thick with a 4px rounded data end, 1px recessive gridlines,
// one hue per magnitude chart, validated categorical slots for part-to-whole, a tooltip on every mark
// (hover and keyboard focus) and a table view so no value is reachable only by hovering.

type Tip = { x: number; y: number; value: string; label: string } | null;

function useTooltip() {
  const frame = useRef<HTMLDivElement>(null);
  const [tip, setTip] = useState<Tip>(null);
  const show = (target: Element, value: string, label: string) => {
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
  show: (target: Element, value: string, label: string) => void,
  hide: () => void,
  value: string,
  label: string,
) {
  return {
    tabIndex: 0,
    "aria-label": `${label}: ${value}`,
    onPointerEnter: (e: React.PointerEvent<Element>) =>
      show(e.currentTarget, value, label),
    onFocus: (e: React.FocusEvent<Element>) =>
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

// Trend: one series as a 2px line over a ~10% area wash. A crosshair snaps to the nearest point under the
// pointer (or the arrow keys), so the reader aims at a time, never at the thin line.
export function LineChart({
  points,
  format,
  formatAxis,
  label,
}: {
  points: Array<{ key: string; label: string; value: number }>;
  format: (v: number) => string;
  formatAxis: (v: number) => string;
  label: string;
}) {
  const [active, setActive] = useState<number | null>(null);
  const max = niceMax(Math.max(...points.map((p) => p.value), 0));
  const ticks = [max, max / 2, 0];
  const every = Math.max(1, Math.ceil(points.length / 8));
  const n = points.length;
  const last = n - 1;
  const x = (i: number) => (n <= 1 ? 50 : (i / (n - 1)) * 100);
  const y = (v: number) => 100 - (v / max) * 100;
  const line = points
    .map((p, i) => `${i ? "L" : "M"}${x(i)},${y(p.value)}`)
    .join(" ");
  const area = `${line} L${x(last)},100 L${x(0)},100 Z`;
  const pick = (e: React.PointerEvent<HTMLDivElement>) => {
    const box = e.currentTarget.getBoundingClientRect();
    const ratio = (e.clientX - box.left) / box.width;
    setActive(Math.max(0, Math.min(last, Math.round(ratio * last))));
  };
  return (
    <div
      className="viz-columns viz-line"
      aria-label={label}
      role="group"
      dir="ltr"
    >
      <div className="viz-y-axis" aria-hidden="true">
        {ticks.map((tick) => (
          <span key={tick} style={{ bottom: `${(tick / max) * 100}%` }}>
            {formatAxis(tick)}
          </span>
        ))}
      </div>
      <div
        className="viz-plot viz-line-plot"
        tabIndex={0}
        aria-label={
          active === null
            ? label
            : `${points[active].label}: ${format(points[active].value)}`
        }
        onPointerMove={pick}
        onPointerLeave={() => setActive(null)}
        onFocus={() => setActive(last)}
        onBlur={() => setActive(null)}
        onKeyDown={(e) => {
          if (e.key === "ArrowLeft")
            setActive((a) => Math.max(0, (a ?? last) - 1));
          if (e.key === "ArrowRight")
            setActive((a) => Math.min(last, (a ?? last) + 1));
        }}
      >
        {ticks.map((tick) => (
          <i
            key={tick}
            className="viz-grid"
            style={{ bottom: `${(tick / max) * 100}%` }}
          />
        ))}
        <svg
          viewBox="0 0 100 100"
          preserveAspectRatio="none"
          aria-hidden="true"
        >
          <path d={area} className="viz-line-area" />
          <path
            d={line}
            className="viz-line-path"
            vectorEffect="non-scaling-stroke"
          />
        </svg>
        {/* The latest value is the one the story is about: end marker plus its label. */}
        <span
          className="viz-line-dot"
          style={{ left: `${x(last)}%`, top: `${y(points[last].value)}%` }}
        />
        <span
          className="viz-line-end"
          style={{ left: `${x(last)}%`, top: `${y(points[last].value)}%` }}
        >
          {format(points[last].value)}
        </span>
        {active !== null && (
          <>
            <i className="viz-crosshair" style={{ left: `${x(active)}%` }} />
            <span
              className="viz-line-dot is-active"
              style={{
                left: `${x(active)}%`,
                top: `${y(points[active].value)}%`,
              }}
            />
            <div
              className="viz-tooltip"
              style={{
                left: `${x(active)}%`,
                top: `${y(points[active].value)}%`,
              }}
            >
              <strong>{format(points[active].value)}</strong>
              <span>{points[active].label}</span>
            </div>
          </>
        )}
      </div>
      <span />
      <div className="viz-line-x" aria-hidden="true">
        {points.map((point, i) =>
          i % every === 0 ? (
            <span key={point.key} style={{ left: `${x(i)}%` }}>
              {point.label}
            </span>
          ) : null,
        )}
      </div>
    </div>
  );
}

// Part-to-whole at a glance (<= 6 segments): a donut with a surface gap between segments, the total in the
// middle and a legend carrying name, value and share.
export function DonutChart({
  items,
  format,
  centerLabel,
}: {
  items: Array<{ key: string; label: string; value: number }>;
  format: (v: number) => string;
  centerLabel: string;
}) {
  const { frame, show, hide, node } = useTooltip();
  const total = items.reduce((sum, x) => sum + x.value, 0) || 1;
  const pct = (v: number) => `${Math.round((v / total) * 100)}%`;
  // Circumference 100 (r = 15.9155) so dash lengths read directly as percentages.
  const gap = items.length > 1 ? 0.8 : 0;
  let offset = 0;
  return (
    <div className="viz-donut" ref={frame}>
      <div className="viz-donut-figure">
        <svg viewBox="0 0 42 42" role="img" aria-label={centerLabel}>
          <circle cx="21" cy="21" r="15.9155" className="viz-donut-track" />
          {items.map((item, i) => {
            const share = (item.value / total) * 100;
            const dash = Math.max(share - gap, 0.1);
            const segment = (
              <circle
                key={item.key}
                cx="21"
                cy="21"
                r="15.9155"
                className={`viz-donut-segment ${slotClass(item.key, i)}`}
                strokeDasharray={`${dash} ${100 - dash}`}
                strokeDashoffset={25 - offset}
                {...markHandlers(
                  show,
                  hide,
                  `${format(item.value)} · ${pct(item.value)}`,
                  item.label,
                )}
              />
            );
            offset += share;
            return segment;
          })}
        </svg>
        <div className="viz-donut-center" aria-hidden="true">
          <strong>{format(total)}</strong>
          <span>{centerLabel}</span>
        </div>
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

// One value against a target on a half-circle dial: the arc fills with the status colour, the track is a
// lighter step of the same hue, a tick marks the target, and the state is also written out.
export function Gauge({
  value,
  max,
  target,
  warnAt,
  display,
  caption,
  states,
}: {
  value: number;
  max: number;
  target: number;
  warnAt: number;
  display: string;
  caption: string;
  states: { good: string; warn: string; danger: string };
}) {
  const level = value <= target ? "good" : value <= warnAt ? "warn" : "danger";
  const ratio = Math.max(0, Math.min(1, value / max));
  const targetAngle = Math.PI * (1 - Math.max(0, Math.min(1, target / max)));
  // Semicircle of radius 40 centred at (50,50).
  const arc = "M10,50 A40,40 0 0 1 90,50";
  const length = Math.PI * 40;
  const at = (radius: number) => ({
    x: 50 + radius * Math.cos(targetAngle),
    y: 50 - radius * Math.sin(targetAngle),
  });
  const inner = at(32);
  const outer = at(48);
  return (
    <div className={`viz-gauge viz-meter-${level}`}>
      <div className="viz-gauge-dial" dir="ltr">
        <svg
          viewBox="0 0 100 56"
          role="meter"
          aria-valuemin={0}
          aria-valuemax={max}
          aria-valuenow={value}
          aria-label={`${display} · ${caption}`}
        >
          <path d={arc} className="viz-gauge-track" />
          <path
            d={arc}
            className="viz-gauge-fill"
            strokeDasharray={`${ratio * length} ${length}`}
          />
          <line
            x1={inner.x}
            y1={inner.y}
            x2={outer.x}
            y2={outer.y}
            className="viz-gauge-target"
          />
        </svg>
        <div className="viz-gauge-center">
          <strong>{display}</strong>
          <span className="viz-meter-state">
            <i aria-hidden="true" />
            {states[level]}
          </span>
        </div>
      </div>
      <p className="viz-muted viz-gauge-caption">{caption}</p>
    </div>
  );
}
