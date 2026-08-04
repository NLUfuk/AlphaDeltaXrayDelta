import { useState } from 'react'
import { useCompanies } from '../../lib/admin'
import { useAuth } from '../../lib/auth'
import { toApiError } from '../../lib/api'
import { errorMessage } from '../../lib/messages'
import {
  FIELD_TYPES, useCreateField, useDeleteField, useFormFields, useUpdateField, type FormField,
} from '../../lib/formfields'
import { Alert, Button, Card, Field, Icon, Input } from '../../ui/primitives'

// Configurable public-form fields (spec §4.6): a company admin adds extra fields (text / number / select)
// that appear on the public request form and are captured on the ticket. Backend gates on company admin.
export default function FormFields() {
  const { user } = useAuth()
  const { data: companies } = useCompanies()
  const [companyId, setCompanyId] = useState<string | undefined>(user?.companies[0]?.companyId)
  const cid = companyId ?? companies?.[0]?.id
  const { data: fields, isLoading } = useFormFields(cid)
  const create = useCreateField(cid)
  const update = useUpdateField(cid)
  const remove = useDeleteField(cid)
  const [error, setError] = useState<string | null>(null)
  const [draft, setDraft] = useState({ label: '', type: 0, required: false, options: '' })

  function fail(err: unknown) {
    const { code, message } = toApiError(err)
    setError(errorMessage(code, message))
  }

  function add(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (!draft.label.trim()) return
    create.mutate(
      { label: draft.label.trim(), type: draft.type, required: draft.required, options: draft.type === 3 ? draft.options : null },
      { onSuccess: () => setDraft({ label: '', type: 0, required: false, options: '' }), onError: fail },
    )
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-lg font-semibold text-ink">Form alanları</h1>
        {(companies?.length ?? 0) > 1 && (
          <select
            className="rounded-md border border-line bg-surface px-2 py-2 text-sm"
            value={cid} onChange={(e) => setCompanyId(e.target.value)}
          >
            {companies?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        )}
      </header>
      <p className="text-sm text-muted">Bu alanlar müşterinin talep formunda görünür ve talebe kaydedilir.</p>

      {error && <Alert>{error}</Alert>}
      {isLoading ? (
        <p className="text-muted">Yükleniyor…</p>
      ) : (
        <Card className="divide-y divide-line">
          {fields?.length === 0 && <p className="p-4 text-sm text-muted">Henüz özel alan yok.</p>}
          {fields?.map((f) => <FieldRow key={f.id} field={f} companyId={cid} onError={fail} onDelete={() => remove.mutate(f.id, { onError: fail })} onUpdate={(u) => update.mutate(u, { onError: fail })} />)}
        </Card>
      )}

      <Card className="p-4">
        <h2 className="mb-3 font-semibold text-ink">Yeni alan</h2>
        <form onSubmit={add} className="flex flex-wrap items-end gap-3">
          <Field label="Etiket"><Input className="w-48" value={draft.label} onChange={(e) => setDraft({ ...draft, label: e.target.value })} required /></Field>
          <Field label="Tip">
            <select className="rounded-md border border-line bg-surface px-2 py-2 text-sm" value={draft.type} onChange={(e) => setDraft({ ...draft, type: Number(e.target.value) })}>
              {FIELD_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
            </select>
          </Field>
          <label className="flex items-center gap-1.5 pb-2 text-sm text-muted">
            <input type="checkbox" checked={draft.required} onChange={(e) => setDraft({ ...draft, required: e.target.checked })} /> Zorunlu
          </label>
          {draft.type === 3 && (
            <Field label="Seçenekler (her satıra bir)"><textarea className="h-20 w-56 rounded-md border border-line bg-surface px-2 py-1 text-sm" value={draft.options} onChange={(e) => setDraft({ ...draft, options: e.target.value })} /></Field>
          )}
          <Button type="submit" disabled={create.isPending}>Ekle</Button>
        </form>
      </Card>
    </div>
  )
}

function FieldRow({ field, onDelete, onUpdate }: {
  field: FormField; companyId: string | undefined; onError: (e: unknown) => void
  onDelete: () => void; onUpdate: (f: FormField) => void
}) {
  const typeLabel = FIELD_TYPES.find((t) => t.value === field.type)?.label ?? '—'
  return (
    <div className="flex items-center gap-3 p-3 text-sm">
      <span className="flex-1 font-medium text-ink">{field.label}{field.required && <span className="text-red-500"> *</span>}</span>
      <span className="text-muted">{typeLabel}</span>
      <button
        onClick={() => onUpdate({ ...field, isActive: !field.isActive })}
        className={`rounded-full px-2 py-0.5 text-xs ${field.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-canvas text-muted'}`}
      >
        {field.isActive ? 'Aktif' : 'Pasif'}
      </button>
      <button onClick={onDelete} className="text-muted hover:text-red-600"><Icon name="delete-outline" /></button>
    </div>
  )
}
