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
              style={
                {
                  height: `${(point.value / max) * 100}%`,
                  "--i": i,
                } as React.CSSProperties
              }
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
              style={
                {
                  width: `${Math.max(2, (item.value / max) * 100)}%`,
                  "--i": i,
                } as React.CSSProperties
              }
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
            style={{ flexGrow: item.value, "--i": i } as React.CSSProperties}
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
  const [hovered, setActive] = useState<number | null>(null);
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
  // A hovered index can outlive the points it pointed at (the range changed to fewer days).
  const active =
    hovered !== null && hovered >= 0 && hovered <= last ? hovered : null;
  const pick = (e: React.PointerEvent<HTMLDivElement>) => {
    const box = e.currentTarget.getBoundingClientRect();
    if (!box.width) return;
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
                style={{ "--i": i } as React.CSSProperties}
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

// One value against a target on a speedometer dial: the arc is split into good / near / over zones (the
// zone holding the value at full strength, the others recessive), a needle points at the value, the scale
// is labelled at 0, the target (with a tick) and the maximum, and the state is also written out.
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
  const clamp = (v: number) => Math.max(0, Math.min(1, v / max));
  const ratio = clamp(value);
  // Ratio 0 is the left end of the dial, 1 the right end; centre (50,50).
  const at = (radius: number, r: number) => ({
    x: 50 - radius * Math.cos(Math.PI * r),
    y: 50 - radius * Math.sin(Math.PI * r),
  });
  const band = (from: number, to: number) => {
    const a = at(40, from);
    const b = at(40, to);
    return `M${a.x},${a.y} A40,40 0 0 1 ${b.x},${b.y}`;
  };
  const zones = [
    { key: "good", from: 0, to: clamp(target) },
    { key: "warn", from: clamp(target), to: clamp(warnAt) },
    { key: "danger", from: clamp(warnAt), to: 1 },
  ].filter((z) => z.to > z.from);
  const ticks = [0, target, max];
  return (
    <div className={`viz-gauge viz-meter-${level}`}>
      <div className="viz-gauge-dial" dir="ltr">
        <svg
          viewBox="-2 2 104 54"
          role="meter"
          aria-valuemin={0}
          aria-valuemax={max}
          aria-valuenow={value}
          aria-label={`${display} · ${caption}`}
        >
          {zones.map((zone) => (
            <path
              key={zone.key}
              d={band(zone.from, zone.to)}
              className={`viz-gauge-zone viz-gauge-zone-${zone.key} ${zone.key === level ? "is-active" : ""}`}
            />
          ))}
          {ticks.map((tick) => {
            const r = clamp(tick);
            const inner = at(31, r);
            const outer = at(34, r);
            const text = at(26, r);
            return (
              <g key={tick} className="viz-gauge-tick">
                {tick === target && (
                  <line x1={inner.x} y1={inner.y} x2={outer.x} y2={outer.y} />
                )}
                <text
                  x={text.x}
                  y={text.y}
                  textAnchor={r < 0.25 ? "start" : r > 0.75 ? "end" : "middle"}
                  dominantBaseline="middle"
                >
                  {tick}
                </text>
              </g>
            );
          })}
          <g
            className="viz-gauge-needle"
            style={{ transform: `rotate(${ratio * 180}deg)` }}
          >
            <path d="M50,47.8 L14,50 L50,52.2 Z" />
          </g>
          <circle cx="50" cy="50" r="4" className="viz-gauge-hub" />
        </svg>
      </div>
      <div className="viz-gauge-center">
        <strong>{display}</strong>
        <span className="viz-meter-state">
          <i aria-hidden="true" />
          {states[level]}
        </span>
      </div>
      <p className="viz-muted viz-gauge-caption">{caption}</p>
    </div>
  );
}

// A rate against warning / critical limits as a thermometer: the column rises from the bulb in the status
// colour, the scale runs to twice the critical limit, and both limits are marked across the tube.
export function Thermometer({
  value,
  warnAt,
  dangerAt,
  label,
  states,
  children,
}: {
  value: number;
  warnAt: number;
  dangerAt: number;
  label: string;
  states: { good: string; warn: string; danger: string };
  children?: ReactNode;
}) {
  const id = useId();
  const level =
    value >= dangerAt ? "danger" : value >= warnAt ? "warn" : "good";
  const max = Math.max(dangerAt * 2, 0.0001);
  const pos = (v: number) => `${Math.max(0, Math.min(1, v / max)) * 100}%`;
  const pct = (v: number) => `${Math.round(v * 1000) / 10}%`;
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((s) => s * max);
  return (
    <div className={`viz-thermo viz-meter-${level}`}>
      <div className="viz-thermo-figure" dir="ltr">
        <div className="viz-thermo-scale" aria-hidden="true">
          {ticks.map((tick) => (
            <span key={tick} style={{ bottom: pos(tick) }}>
              {pct(tick)}
            </span>
          ))}
        </div>
        <div className="viz-thermo-body">
          <div
            className="viz-thermo-tube"
            role="meter"
            aria-labelledby={id}
            aria-label={label}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={Math.round(value * 1000) / 10}
          >
            <div className="viz-thermo-fill" style={{ height: pos(value) }} />
            <i className="viz-thermo-limit" style={{ bottom: pos(warnAt) }} />
            <i className="viz-thermo-limit" style={{ bottom: pos(dangerAt) }} />
          </div>
          <div className="viz-thermo-bulb" />
        </div>
      </div>
      <div className="viz-thermo-readout">
        <strong id={id}>{pct(value)}</strong>
        <span className="viz-meter-state">
          <i aria-hidden="true" />
          {states[level]}
        </span>
        {children}
      </div>
    </div>
  );
}

// Ranked items as a funnel: centred stages narrowing with the value (the leader is the full width), each
// stage tapering into the next, the value inside the stage and the name beside it.
export function FunnelChart({
  items,
  format,
}: {
  items: Array<{ key: string; label: string; value: number }>;
  format: (v: number) => string;
}) {
  const { frame, show, hide, node } = useTooltip();
  const max = Math.max(...items.map((x) => x.value), 0) || 1;
  // Stages never shrink below 38% so the value inside stays readable.
  const width = (v: number) => 38 + 62 * (v / max);
  const inset = (w: number) => (100 - w) / 2;
  return (
    <div className="viz-funnel" ref={frame}>
      {items.map((item, i) => {
        const top = width(item.value);
        const next = items[i + 1];
        const bottom = next ? width(next.value) : top * 0.88;
        const shape = `polygon(${inset(top)}% 0, ${100 - inset(top)}% 0, ${100 - inset(bottom)}% 100%, ${inset(bottom)}% 100%)`;
        return (
          <div key={item.key} className="viz-funnel-row">
            <span className="viz-funnel-label" title={item.label}>
              <b>{i + 1}</b>
              {item.label}
            </span>
            <div className="viz-funnel-track">
              <div
                className="viz-funnel-stage"
                style={
                  {
                    clipPath: shape,
                    "--i": i,
                    "--step": `${100 - i * 6}%`,
                  } as React.CSSProperties
                }
                {...markHandlers(show, hide, format(item.value), item.label)}
              />
              <span className="viz-funnel-value">{format(item.value)}</span>
            </div>
          </div>
        );
      })}
      {node}
    </div>
  );
}
