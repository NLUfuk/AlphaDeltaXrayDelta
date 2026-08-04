import { useRef, useState } from 'react'
import { formatDateTime } from '../../lib/messages'
import {
  renderPreview, TEMPLATE_GROUPS, TEMPLATE_LABELS, tokensFor, useEmailTemplates, useUpdateTemplate,
} from '../../lib/templates'
import { Button, Card, Icon, Input } from '../../ui/primitives'

// Super-admin email template editor (spec §14/§9): list → editor → live preview. Bodies carry
// {{placeholder}} tokens filled at send time; only the tokens this mail's payload actually carries are
// offered (an unknown one would ship literally). Backend enforces the SuperAdmin gate (403 → error).
export default function Templates() {
  const { data, isLoading, error } = useEmailTemplates()
  const update = useUpdateTemplate()
  const [selected, setSelected] = useState<string | null>(null)
  const [draft, setDraft] = useState<{ subject: string; body: string } | null>(null)
  const [saved, setSaved] = useState(false)
  const bodyRef = useRef<HTMLTextAreaElement>(null)

  if (isLoading) return <p className="text-muted">Yükleniyor…</p>
  if (error) return <p className="text-red-600">Şablonlar yüklenemedi (süper admin gerekli).</p>

  const activeKey = selected ?? data?.[0]?.key ?? ''
  const current = data?.find((t) => t.key === activeKey)
  const view = draft ?? (current ? { subject: current.subject, body: current.body } : { subject: '', body: '' })
  const dirty = current != null && (view.subject !== current.subject || view.body !== current.body)

  async function save() {
    if (!current) return
    await update.mutateAsync({ key: current.key, subject: view.subject, body: view.body })
    setDraft(null)
    setSaved(true)
    setTimeout(() => setSaved(false), 2500)
  }

  // Insert a token where the cursor is — beats hunting for the right spot in raw HTML.
  function insertToken(token: string) {
    const el = bodyRef.current
    const text = `{{${token}}}`
    if (!el) return setDraft({ ...view, body: view.body + text })
    const { selectionStart: from, selectionEnd: to } = el
    setDraft({ ...view, body: view.body.slice(0, from) + text + view.body.slice(to) })
    requestAnimationFrame(() => {
      el.focus()
      el.setSelectionRange(from + text.length, from + text.length)
    })
  }

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between gap-3">
        <h1 className="text-lg font-semibold text-ink">E-posta şablonları</h1>
        {current?.updatedAt && (
          <span className="text-xs text-muted">Son güncelleme: {formatDateTime(current.updatedAt)}</span>
        )}
      </header>

      <div className="flex gap-4 max-lg:flex-col">
        <nav className="w-60 shrink-0 space-y-3">
          {TEMPLATE_GROUPS.map((g) => {
            const items = data?.filter((t) => g.match(t.key)) ?? []
            if (items.length === 0) return null
            return (
              <Card key={g.title} className="overflow-hidden">
                <div className="border-b border-line px-4 py-2 text-xs font-semibold uppercase tracking-wide text-muted">
                  {g.title}
                </div>
                {items.map((t) => (
                  <button
                    key={t.key}
                    onClick={() => { setSelected(t.key); setDraft(null) }}
                    title={t.key}
                    className={`block w-full border-l-2 px-4 py-2 text-left text-sm transition-colors ${
                      t.key === activeKey
                        ? 'border-primary bg-primary/5 font-medium text-primary'
                        : 'border-transparent text-muted hover:bg-canvas'
                    }`}
                  >
                    {TEMPLATE_LABELS[t.key] ?? t.key}
                  </button>
                ))}
              </Card>
            )
          })}
        </nav>

        <Card className="flex-1 space-y-4 p-5">
          <div>
            <div className="text-sm font-medium text-ink">Konu</div>
            <Input value={view.subject} onChange={(e) => setDraft({ ...view, subject: e.target.value })} />
          </div>

          <div>
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-sm font-medium text-ink">Gövde (HTML)</span>
              <span className="flex flex-wrap items-center gap-1">
                <span className="text-xs text-muted">Ekle:</span>
                {tokensFor(activeKey).map((token) => (
                  <button
                    key={token}
                    type="button"
                    onClick={() => insertToken(token)}
                    className="rounded-full border border-line px-2 py-0.5 font-mono text-xs text-muted hover:border-primary hover:text-primary"
                  >
                    {`{{${token}}}`}
                  </button>
                ))}
              </span>
            </div>
            <textarea
              ref={bodyRef}
              className="mt-1 h-64 w-full rounded-md border border-line bg-surface px-3 py-2 font-mono text-sm text-ink"
              value={view.body}
              onChange={(e) => setDraft({ ...view, body: e.target.value })}
            />
            <p className="mt-1 text-xs text-muted">
              Yalnız yukarıdaki yer tutucular doldurulur; başka bir <code>{'{{ad}}'}</code> e-postada olduğu gibi görünür.
            </p>
          </div>

          <div>
            <div className="text-sm font-medium text-ink">Önizleme (örnek verilerle)</div>
            <div className="mt-1 overflow-hidden rounded-md border border-line">
              <div className="border-b border-line bg-canvas px-3 py-2 text-sm">
                <span className="text-xs text-muted">Konu:</span>{' '}
                <span className="font-medium text-ink">{renderPreview(view.subject)}</span>
              </div>
              {/* sandbox="" → the preview can't run scripts or navigate; it is only rendered HTML. */}
              <iframe
                title="E-posta önizleme"
                sandbox=""
                className="h-64 w-full bg-white"
                srcDoc={`<body style="font-family:Manrope,Arial,sans-serif;font-size:14px;color:#1f2937;padding:16px">${renderPreview(view.body)}</body>`}
              />
            </div>
          </div>

          <div className="flex items-center gap-2">
            <Button disabled={!dirty || update.isPending} onClick={save}>
              {update.isPending ? 'Kaydediliyor…' : 'Kaydet'}
            </Button>
            {dirty && <Button variant="secondary" onClick={() => setDraft(null)}>Vazgeç</Button>}
            {saved && !dirty && (
              <span className="flex items-center gap-1 text-sm text-emerald-600"><Icon name="check" />Kaydedildi</span>
            )}
          </div>
        </Card>
      </div>
    </div>
  )
}
