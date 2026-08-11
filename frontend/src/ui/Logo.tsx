// Brand mark, from kanby-logo.svg (frontend/public/kanby-logo.svg keeps the original lockup for
// exports and brand.logo_url). Redrawn inline here for two reasons: an <img> cannot follow the dark
// theme, and the sidebar needs the icon alone when collapsed.
//
// The graph strokes use currentColor rather than the source's navy #0F2540 — navy on a dark
// background is invisible. Only the accent node keeps its literal amber: it is the one mark that
// must stay the same colour in both themes to still read as the logo.
import { useBrand } from '../lib/brand'

const ACCENT = '#F59E0B'

export function LogoMark({ className = '' }: { className?: string }) {
  return (
    <svg viewBox="0 0 130 135" className={className} fill="none" aria-hidden="true">
      <g stroke="currentColor" strokeWidth={7} fill="currentColor">
        <line x1={15} y1={20} x2={95} y2={55} strokeLinecap="round" />
        <line x1={95} y1={55} x2={25} y2={105} strokeLinecap="round" />
        <line x1={25} y1={105} x2={115} y2={115} strokeLinecap="round" />
        <line x1={95} y1={55} x2={115} y2={115} strokeLinecap="round" />
        <circle cx={15} cy={20} r={13} />
        <circle cx={95} cy={55} r={16} fill={ACCENT} stroke={ACCENT} />
        <circle cx={25} cy={105} r={13} />
        <circle cx={115} cy={115} r={13} />
      </g>
    </svg>
  )
}

/**
 * Full lockup: mark + system name + the slogan. `compact` drops the slogan for tight spots (sidebar).
 *
 * The name comes from the `brand.system_name` setting, not from a literal — an operator who renames the
 * system in Ayarlar sees it here, in the tab title and on the sign-in screen. The MARK stays inline:
 * `brand.logo_url` is an <img>, which cannot follow the dark theme, and it already drives the surfaces
 * customers see (public form, müşteri giriş sayfası).
 */
export function Logo({ compact = false, className = '' }: { compact?: boolean; className?: string }) {
  const { systemName } = useBrand()
  return (
    <span className={`flex items-center gap-2.5 ${className}`}>
      <LogoMark className="h-8 w-8 shrink-0 text-ink" />
      <span className="min-w-0">
        <span className="block truncate text-lg font-extrabold leading-none tracking-tight text-ink">{systemName}</span>
        {!compact && (
          <span className="mt-1 block text-[10px] font-medium uppercase leading-none tracking-[0.28em] text-muted">
            Connect · Convert
          </span>
        )}
      </span>
    </span>
  )
}
