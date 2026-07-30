import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'

// Atomic primitives (spec §4.2), Odoo-inspired: purple primary, understated surfaces, soft cards.
// Screens compose these; they never re-write button/input markup inline.

type Variant = 'primary' | 'secondary' | 'danger'
const VARIANTS: Record<Variant, string> = {
  primary: 'bg-primary text-white hover:bg-primary-hover',
  secondary: 'bg-white text-ink border border-line hover:bg-canvas',
  danger: 'bg-red-600 text-white hover:bg-red-700',
}

export function Button({ variant = 'primary', className = '', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant }) {
  return (
    <button
      className={`inline-flex items-center justify-center rounded-md px-4 py-2 text-sm font-medium transition-colors disabled:opacity-50 ${VARIANTS[variant]} ${className}`}
      {...props}
    />
  )
}

export function Input({ className = '', ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={`w-full rounded-md border border-line bg-white px-3 py-2 text-sm outline-none transition-colors focus:border-primary focus:ring-2 focus:ring-primary/20 ${className}`}
      {...props}
    />
  )
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex flex-1 flex-col gap-1">
      <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</span>
      {children}
    </label>
  )
}

export function Card({ className = '', children }: { className?: string; children: ReactNode }) {
  return <div className={`rounded-lg border border-line bg-white shadow-sm ${className}`}>{children}</div>
}

export function Alert({ children }: { children: ReactNode }) {
  return <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{children}</div>
}

// Material Design Icons (reused from the StarAdmin template's mdi webfont). Usage: <Icon name="cog" />.
export function Icon({ name, className = '' }: { name: string; className?: string }) {
  return <i className={`mdi mdi-${name} ${className}`} aria-hidden="true" />
}

export function Badge({ label, color }: { label: string; color: string }) {
  return (
    <span className="rounded-full px-2 py-0.5 text-xs font-medium text-white" style={{ backgroundColor: color }}>
      {label}
    </span>
  )
}
