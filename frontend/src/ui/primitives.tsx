import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'

// Atomic primitives (spec §4.2): StarAdmin look — deep-blue primary, soft-shadow surfaces,
// Manrope type. Screens compose these; they never re-write button/input markup inline.

type Variant = 'primary' | 'secondary' | 'danger'
const VARIANTS: Record<Variant, string> = {
  primary: 'bg-primary text-white shadow-sm hover:bg-primary-hover',
  secondary: 'bg-surface text-ink border border-line hover:bg-canvas',
  danger: 'bg-danger text-white shadow-sm hover:brightness-95',
}

export function Button({ variant = 'primary', className = '', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant }) {
  return (
    <button
      className={`inline-flex items-center justify-center rounded-lg px-4 py-2 text-sm font-medium transition-colors disabled:opacity-50 ${VARIANTS[variant]} ${className}`}
      {...props}
    />
  )
}

export function Input({ className = '', ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={`w-full rounded-md border border-line bg-surface px-3 py-2 text-sm outline-none transition-colors focus:border-primary focus:ring-2 focus:ring-primary/20 ${className}`}
      {...props}
    />
  )
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex flex-1 flex-col gap-1">
      <span className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</span>
      {children}
    </label>
  )
}

export function Card({ className = '', children }: { className?: string; children: ReactNode }) {
  return <div className={`rounded-xl border border-line bg-surface shadow-card ${className}`}>{children}</div>
}

/** The one "loading" block. Screens pass layout (padding/size) in, never re-type the label. */
export function Loading({ className = '' }: { className?: string }) {
  return <p className={`text-muted ${className}`}>Yükleniyor…</p>
}

export function Alert({ children }: { children: ReactNode }) {
  return <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{children}</div>
}

// Material Design Icons (reused from the StarAdmin template's mdi webfont). Usage: <Icon name="cog" />.
export function Icon({ name, className = '' }: { name: string; className?: string }) {
  return <i className={`mdi mdi-${name} ${className}`} aria-hidden="true" />
}

/**
 * On/off switch — the StarAdmin (Bootstrap `.form-switch`) shape in our tokens: a pill track with a
 * knob that slides right and fills with `primary` when on. A real `<input type="checkbox">` underneath,
 * so keyboard, focus ring and screen readers work without re-implementing any of it.
 */
export function Switch({
  checked, onChange, disabled = false, label, describedBy,
}: {
  checked: boolean
  onChange: (next: boolean) => void
  disabled?: boolean
  label: string
  describedBy?: string
}) {
  return (
    <label className={`relative inline-flex shrink-0 items-center ${disabled ? 'cursor-not-allowed opacity-50' : 'cursor-pointer'}`}>
      <input
        type="checkbox" role="switch" className="peer sr-only"
        checked={checked} disabled={disabled} aria-label={label} aria-describedby={describedBy}
        onChange={(e) => onChange(e.target.checked)}
      />
      <span
        className="h-6 w-11 rounded-full bg-line transition-colors peer-checked:bg-primary
                   peer-focus-visible:ring-2 peer-focus-visible:ring-primary/40 peer-focus-visible:ring-offset-2"
      />
      <span
        className="pointer-events-none absolute left-0.5 h-5 w-5 rounded-full bg-white shadow-sm
                   transition-transform peer-checked:translate-x-5"
      />
    </label>
  )
}

export function Badge({ label, color }: { label: string; color: string }) {
  return (
    <span className="rounded-full px-2 py-0.5 text-xs font-medium text-white" style={{ backgroundColor: color }}>
      {label}
    </span>
  )
}
