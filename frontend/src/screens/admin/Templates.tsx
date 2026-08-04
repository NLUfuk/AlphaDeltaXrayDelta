import { useState } from 'react'
import { TEMPLATE_LABELS, useEmailTemplates, useUpdateTemplate } from '../../lib/templates'
import { Button, Card, Input } from '../../ui/primitives'

// Super-admin email template editor (spec §14/§9). Left list + editor for the selected template. Bodies
// carry {{placeholder}} tokens filled at send time; a small hint lists the common ones. Backend enforces
// the SuperAdmin gate (403 → error).
export default function Templates() {
  const { data, isLoading, error } = useEmailTemplates()
  const update = useUpdateTemplate()
  const [selected, setSelected] = useState<string | null>(null)
  const [draft, setDraft] = useState<{ subject: string; body: string } | null>(null)

  if (isLoading) return <p className="text-muted">Yükleniyor…</p>
  if (error) return <p className="text-red-600">Şablonlar yüklenemedi (süper admin gerekli).</p>

  const activeKey = selected ?? data?.[0]?.key ?? ''
  const current = data?.find((t) => t.key === activeKey)
  const view = draft ?? (current ? { subject: current.subject, body: current.body } : { subject: '', body: '' })
  const dirty = current != null && (view.subject !== current.subject || view.body !== current.body)

  async function save() {
    if (current) await update.mutateAsync({ key: current.key, subject: view.subject, body: view.body })
    setDraft(null)
  }

  return (
    <div className="space-y-4">
      <h1 className="text-lg font-semibold text-ink">E-posta şablonları</h1>
      <div className="flex gap-4">
        <nav className="w-56 shrink-0">
          <Card className="overflow-hidden">
            {data?.map((t) => (
              <button
                key={t.key}
                onClick={() => { setSelected(t.key); setDraft(null) }}
                className={`block w-full border-l-2 px-4 py-2 text-left text-sm transition-colors ${
                  t.key === activeKey ? 'border-primary bg-primary/5 font-medium text-primary' : 'border-transparent text-muted hover:bg-canvas'
                }`}
              >
                {TEMPLATE_LABELS[t.key] ?? t.key}
              </button>
            ))}
          </Card>
        </nav>

        <Card className="flex-1 space-y-4 p-5">
          <div>
            <div className="text-sm font-medium text-ink">Konu</div>
            <Input value={view.subject} onChange={(e) => setDraft({ ...view, subject: e.target.value })} />
          </div>
          <div>
            <div className="text-sm font-medium text-ink">Gövde (HTML)</div>
            <textarea
              className="mt-1 h-64 w-full rounded-md border border-line bg-surface px-3 py-2 font-mono text-sm text-ink"
              value={view.body}
              onChange={(e) => setDraft({ ...view, body: e.target.value })}
            />
            <p className="mt-1 text-xs text-muted">
              Yer tutucular: <code>{'{{ticketNumber}}'}</code> <code>{'{{title}}'}</code> <code>{'{{newValue}}'}</code>{' '}
              <code>{'{{name}}'}</code> <code>{'{{companyName}}'}</code> <code>{'{{link}}'}</code>
            </p>
          </div>
          <div className="flex gap-2">
            <Button disabled={!dirty || update.isPending} onClick={save}>Kaydet</Button>
            {dirty && <Button variant="secondary" onClick={() => setDraft(null)}>Vazgeç</Button>}
          </div>
        </Card>
      </div>
    </div>
  )
}
