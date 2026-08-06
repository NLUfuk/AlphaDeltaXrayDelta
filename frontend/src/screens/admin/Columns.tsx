import { useState } from 'react'
import { useCompanies } from '../../lib/admin'
import { useAuth } from '../../lib/auth'
import { STATUS_CATEGORIES } from '../../lib/messages'
import {
  useColumns, useCreateColumn, useDeleteColumn, useReorderColumns, useUpdateColumn, type StatusColumn,
} from '../../lib/tickets'
import { errorText } from '../../lib/messages'
import { Alert, Button, Card, Field, Icon, Input, Select } from '../../ui/primitives'

// Kanban column manager (spec §12/§18.9): an admin adds a column anywhere in the chain, renames /
// recolors it, reorders the board, or removes an empty one. The first change forks this company's
// board off the shared defaults (handled server-side).
export default function Columns() {
  const { user } = useAuth()
  const { data: companies } = useCompanies()
  const [companyId, setCompanyId] = useState<string | undefined>(user?.companies[0]?.companyId)
  const cid = companyId ?? companies?.[0]?.id
  const { data: columns, isLoading } = useColumns(cid)
  const create = useCreateColumn(cid)
  const update = useUpdateColumn(cid)
  const reorder = useReorderColumns(cid)
  const remove = useDeleteColumn(cid)
  const [error, setError] = useState<string | null>(null)

  const count = columns?.length ?? 0
  const [draft, setDraft] = useState({ name: '', category: 1, color: '#6366f1', position: count })

  function fail(err: unknown) {
    setError(errorText(err))
  }

  function move(cols: StatusColumn[], from: number, to: number) {
    if (to < 0 || to >= cols.length) return
    const ids = cols.map((c) => c.id)
    ;[ids[from], ids[to]] = [ids[to], ids[from]]
    setError(null)
    reorder.mutate(ids, { onError: fail })
  }

  async function add(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    if (!draft.name.trim()) return
    create.mutate(
      { name: draft.name.trim(), category: draft.category, color: draft.color, position: draft.position },
      { onSuccess: () => setDraft({ name: '', category: 1, color: '#6366f1', position: count + 1 }), onError: fail },
    )
  }

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-lg font-semibold text-ink">Kanban sütunları</h1>
          <p className="text-sm text-muted">Sütunları istediğiniz sıraya zincir gibi ekleyin ve düzenleyin.</p>
        </div>
        {companies && companies.length > 1 && (
          <Select value={cid} onChange={(e) => setCompanyId(e.target.value)}>
            {companies.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </Select>
        )}
      </header>

      {error && <Alert>{error}</Alert>}

      <Card className="divide-y divide-line">
        {isLoading && <p className="p-4 text-sm text-muted">Yükleniyor…</p>}
        {columns?.map((col, i) => (
          <div key={col.id} className="flex items-center gap-3 px-4 py-3">
            <span className="text-xs tabular-nums text-muted w-5">{i + 1}</span>
            <input
              type="color"
              value={col.color}
              disabled={!col.editable}
              onChange={(e) => update.mutate({ id: col.id, color: e.target.value }, { onError: fail })}
              className="h-6 w-6 cursor-pointer rounded border border-line disabled:cursor-not-allowed"
              title="Renk"
            />
            <input
              defaultValue={col.name}
              disabled={!col.editable}
              onBlur={(e) => e.target.value.trim() && e.target.value !== col.name &&
                update.mutate({ id: col.id, name: e.target.value.trim() }, { onError: fail })}
              className="flex-1 rounded-md border border-transparent bg-transparent px-2 py-1 text-sm hover:border-line focus:border-primary focus:outline-none disabled:text-muted"
            />
            <span className="rounded-full bg-canvas px-2 py-0.5 text-xs text-muted">{STATUS_CATEGORIES[col.category]?.label}</span>
            {col.editable ? (
              <div className="flex items-center gap-1 text-muted">
                <button onClick={() => move(columns, i, i - 1)} disabled={i === 0} className="p-1 hover:text-ink disabled:opacity-30" title="Yukarı"><Icon name="arrow-up" /></button>
                <button onClick={() => move(columns, i, i + 1)} disabled={i === count - 1} className="p-1 hover:text-ink disabled:opacity-30" title="Aşağı"><Icon name="arrow-down" /></button>
                <button onClick={() => { setError(null); remove.mutate(col.id, { onError: fail }) }} className="p-1 hover:text-red-600" title="Sil"><Icon name="trash-can-outline" /></button>
              </div>
            ) : (
              <span className="text-xs text-muted" title="Paylaşılan varsayılan — özelleştirmek için bir sütun ekleyin"><Icon name="lock-outline" /></span>
            )}
          </div>
        ))}
      </Card>

      <Card className="p-4">
        <form onSubmit={add} className="flex flex-wrap items-end gap-3">
          <Field label="Yeni sütun"><Input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} placeholder="ör. Teklif verildi" /></Field>
          <Field label="Tür">
            <Select value={draft.category} onChange={(e) => setDraft({ ...draft, category: Number(e.target.value) })}>
              {STATUS_CATEGORIES.map((c, i) => <option key={c.key} value={i}>{c.label}</option>)}
            </Select>
          </Field>
          <label className="flex flex-col gap-1">
            <span className="text-xs font-semibold uppercase tracking-wide text-muted">Renk</span>
            <input type="color" value={draft.color} onChange={(e) => setDraft({ ...draft, color: e.target.value })} className="h-9 w-12 rounded border border-line" />
          </label>
          <Field label="Konum">
            <Select value={draft.position} onChange={(e) => setDraft({ ...draft, position: Number(e.target.value) })}>
              {Array.from({ length: count + 1 }, (_, i) => <option key={i} value={i}>{i + 1}. sıra</option>)}
            </Select>
          </Field>
          <Button type="submit" disabled={create.isPending || !draft.name.trim()}><Icon name="plus" className="mr-1" />Ekle</Button>
        </form>
      </Card>
    </div>
  )
}
