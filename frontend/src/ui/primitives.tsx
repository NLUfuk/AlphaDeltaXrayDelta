import { useEffect, useRef, useState } from 'react'
import type {
  ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, Ref, SelectHTMLAttributes, TextareaHTMLAttributes,
} from 'react'
import { loadErrorText } from '../lib/messages'

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

/**
 * Dropdown. Existed as the same 60-character Tailwind class string copied into 12 places across 11
 * screens; one of them had drifted to a different padding. A primitive makes "all our selects look
 * the same" true by construction instead of by everyone remembering the string.
 */
export function Select({ className = '', ...props }: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      className={`rounded-md border border-line bg-surface px-2 py-2 text-sm text-ink outline-none transition-colors focus:border-primary focus:ring-2 focus:ring-primary/20 ${className}`}
      {...props}
    />
  )
}

/**
 * Multi-line input. Same story as Select: seven hand-written copies, three different paddings.
 * Takes `ref` because the template editor needs it to insert a placeholder at the caret — React 19
 * passes ref as an ordinary prop, but the type has to say so.
 */
export function Textarea({ className = '', rows = 3, ...props }: TextareaHTMLAttributes<HTMLTextAreaElement> & { ref?: Ref<HTMLTextAreaElement> }) {
  return (
    <textarea
      rows={rows}
      className={`w-full rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink outline-none transition-colors focus:border-primary focus:ring-2 focus:ring-primary/20 ${className}`}
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

/**
 * The one "this query failed" block. Ten screens each wrote their own sentence and their own markup
 * (`<p className="text-red-600">Rapor yüklenemedi (yetki gerekebilir)</p>`), which had two problems:
 * the wording lived in the component instead of the message catalog, and — worse — the sentence was a
 * GUESS. The server had already said why (`report.forbidden`, `settings.forbidden`, …) and nobody
 * looked. This renders the real reason and falls back to "<what> yüklenemedi" only for codes the
 * catalog does not know.
 */
export function LoadError({ error, what }: { error: unknown; what: string }) {
  return <Alert>{loadErrorText(error, what)}</Alert>
}

/** Shown by the four screens that only make sense for one company (board, onay kuyruğu, sütun ve form
 *  düzenleyicileri) when the super admin's pick is "Tüm şirketler". Not an error: the reports answer
 *  that question, a board cannot. */
export function PickCompany({ what }: { what: string }) {
  return (
    <p className="flex items-center gap-2 text-sm text-muted">
      <Icon name="domain" className="text-lg" />
      {what} tek bir şirket üzerinde çalışır — üstteki seçiciden bir şirket seçin.
    </p>
  )
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

/**
 * Dialog. Built on the native `<dialog showModal()>` rather than a hand-rolled overlay, so Esc to
 * close, the focus trap, inertness of the page behind it and the top-layer stacking (no z-index
 * arithmetic against the sticky navbar) all come from the browser instead of from code we would have
 * to maintain. What is left to write is the frame and the backdrop click.
 *
 * <p>It exists because screens were growing by stacking every secondary action inline — a create form
 * living permanently inside a kanban column, an invite form inside a company card — until the primary
 * content was a minority of the page. Anything that is a detour from what the screen is for belongs
 * in here.</p>
 */
export function Modal({
  open, onClose, title, children, width = 'max-w-lg',
}: {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  width?: string
}) {
  const ref = useRef<HTMLDialogElement>(null)
  // Offset from the centred resting position; the drag ref holds the pointer-to-offset delta so the
  // card does not jump to the cursor on grab.
  const [pos, setPos] = useState({ x: 0, y: 0 })
  const drag = useRef<{ x: number; y: number } | null>(null)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    // showModal() throws if it is already open, and close() on a closed dialog is a no-op that still
    // fires nothing — so both directions are guarded rather than called blindly on every render.
    if (open && !dialog.open) { setPos({ x: 0, y: 0 }); dialog.showModal() }
    else if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      // Tailwind's preflight zeroes the margin the UA sheet uses to centre a modal dialog, hence m-auto.
      style={{ transform: `translate(${pos.x}px, ${pos.y}px)` }}
      // Fires for Esc and for the form's method="dialog" alike, so the parent's state cannot drift
      // out of sync with what is actually on screen.
      onClose={onClose}
      // The dialog element itself is the full-viewport box; the visible card is the div inside it.
      // A click that lands on the element and not on the card is therefore a backdrop click.
      onClick={(e) => { if (e.target === ref.current) onClose() }}
      className={`m-auto w-[calc(100%-2rem)] ${width} rounded-xl border border-line bg-surface p-0 text-ink shadow-lg backdrop:bg-black/40`}
    >
      <div
        // The header doubles as the drag handle. Pointer capture keeps the move/up events coming here
        // even when the cursor outruns the card, so a fast drag cannot strand it mid-move.
        onPointerDown={(e) => {
          if ((e.target as HTMLElement).closest('button')) return
          drag.current = { x: e.clientX - pos.x, y: e.clientY - pos.y }
          e.currentTarget.setPointerCapture(e.pointerId)
        }}
        onPointerMove={(e) => {
          if (!drag.current) return
          setPos({ x: e.clientX - drag.current.x, y: e.clientY - drag.current.y })
        }}
        onPointerUp={() => { drag.current = null }}
        onPointerCancel={() => { drag.current = null }}
        className="flex cursor-move touch-none select-none items-center justify-between border-b border-line px-5 py-3"
      >
        <h2 className="text-sm font-semibold text-ink">{title}</h2>
        <button
          type="button"
          onClick={onClose}
          aria-label="Kapat"
          className="grid h-8 w-8 place-items-center rounded-lg text-muted hover:bg-canvas hover:text-ink"
        >
          <Icon name="close" className="text-lg" />
        </button>
      </div>
      <div className="p-5">{children}</div>
    </dialog>
  )
}

/**
 * A row action as a labelled icon button. Rows that carry three or four actions rendered as full
 * buttons stop reading as data and start reading as a toolbar with a record attached — that is what
 * the company list and the user table both looked like. The label is not dropped, it moves to the
 * accessible name and the tooltip, so nothing is hidden from a screen reader or from a hover.
 */
export function IconAction({ icon, label, onClick, danger = false, disabled = false }: {
  icon: string
  label: string
  onClick: () => void
  danger?: boolean
  disabled?: boolean
}) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      title={label}
      aria-label={label}
      className={`grid h-9 w-9 place-items-center rounded-lg transition-colors disabled:opacity-40 ${
        danger ? 'text-muted hover:bg-danger/10 hover:text-danger' : 'text-muted hover:bg-canvas hover:text-ink'
      }`}
    >
      <Icon name={icon} className="text-lg" />
    </button>
  )
}

export function Badge({ label, color }: { label: string; color: string }) {
  return (
    <span className="rounded-full px-2 py-0.5 text-xs font-medium text-white" style={{ backgroundColor: color }}>
      {label}
    </span>
  )
}
