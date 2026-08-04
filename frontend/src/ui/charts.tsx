// Dependency-free inline-SVG charts (ponytail: no chart lib for a donut + a line).
// Colors follow the dataviz method: trend series use validated categorical blue/orange; status bars
// carry each category's own semantic color and are direct-labeled, so identity is never color-alone.

const SERIES = { opened: '#2a78d6', closed: '#eb6834' } // validated categorical pair (blue, orange)

/** Labeled horizontal bars for magnitude-by-category (status distribution, staff load). */
export function BarList({ rows }: { rows: { label: string; value: number; color: string }[] }) {
  const max = Math.max(1, ...rows.map((r) => r.value))
  if (rows.length === 0) return <p className="text-sm text-muted">Veri yok.</p>
  return (
    <div className="space-y-2">
      {rows.map((r) => (
        <div key={r.label} className="flex items-center gap-2 text-sm">
          <span className="w-36 shrink-0 truncate text-muted">{r.label}</span>
          <div className="h-4 flex-1 overflow-hidden rounded bg-canvas">
            <div className="h-full rounded" style={{ width: `${(r.value / max) * 100}%`, backgroundColor: r.color }} title={String(r.value)} />
          </div>
          <span className="w-8 text-right tabular-nums text-muted">{r.value}</span>
        </div>
      ))}
    </div>
  )
}

/** Opened vs closed over time — two-series line with a legend (identity by color + legend, not color alone). */
export function TrendChart({ data }: { data: { date: string; opened: number; closed: number }[] }) {
  if (data.length === 0) return <p className="text-sm text-muted">Trend verisi yok.</p>
  const w = 360, h = 150, pad = { l: 8, r: 8, t: 8, b: 20 }
  const iw = w - pad.l - pad.r, ih = h - pad.t - pad.b
  const max = Math.max(1, ...data.flatMap((d) => [d.opened, d.closed]))
  const x = (i: number) => pad.l + (data.length <= 1 ? iw / 2 : (i / (data.length - 1)) * iw)
  const y = (v: number) => pad.t + ih - (v / max) * ih

  const series = (key: 'opened' | 'closed', color: string) => (
    <g>
      <polyline points={data.map((d, i) => `${x(i)},${y(d[key])}`).join(' ')} fill="none" stroke={color} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
      {data.map((d, i) => (
        <circle key={i} cx={x(i)} cy={y(d[key])} r={4} fill={color}>
          <title>{d.date} · {key === 'opened' ? 'açılan' : 'kapanan'}: {d[key]}</title>
        </circle>
      ))}
    </g>
  )

  return (
    <div>
      <div className="mb-2 flex gap-4 text-xs text-muted">
        <span className="flex items-center gap-1"><i className="h-2 w-2 rounded-full" style={{ backgroundColor: SERIES.opened }} /> Açılan</span>
        <span className="flex items-center gap-1"><i className="h-2 w-2 rounded-full" style={{ backgroundColor: SERIES.closed }} /> Kapanan</span>
      </div>
      <svg viewBox={`0 0 ${w} ${h}`} className="h-auto w-full">
        <line x1={pad.l} y1={y(0)} x2={w - pad.r} y2={y(0)} stroke="#e5e7eb" strokeWidth={1} />
        {series('opened', SERIES.opened)}
        {series('closed', SERIES.closed)}
      </svg>
    </div>
  )
}
