import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'

// Atomic primitives (spec §4.2). Screens compose these; they never re-write button/input markup inline.

export function Button({ className = '', ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      className={`rounded-md bg-blue-600 px-4 py-2 font-medium text-white hover:bg-blue-700 disabled:opacity-50 ${className}`}
      {...props}
    />
  )
}

export function Input({ className = '', ...props }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={`w-full rounded-md border border-slate-300 px-3 py-2 outline-none focus:border-blue-500 ${className}`}
      {...props}
    />
  )
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="font-medium text-slate-700">{label}</span>
      {children}
    </label>
  )
}

export function Alert({ children }: { children: ReactNode }) {
  return <div className="rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{children}</div>
}

export function Badge({ label, color }: { label: string; color: string }) {
  return (
    <span className="rounded-full px-2 py-0.5 text-xs font-medium text-white" style={{ backgroundColor: color }}>
      {label}
    </span>
  )
}
