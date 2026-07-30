import { useMemo, useState } from 'react'
import { GROUP_LABELS, SETTING_LABELS, useSettings, useUpdateSetting } from '../lib/settings'
import { Button, Card, Input } from '../ui/primitives'

// Super-admin settings (spec §13), Odoo-style: left group sidebar + a top save bar that persists all
// dirty rows at once. Backend enforces the SuperAdmin gate (403 → error below). Text lives in the catalog.
export default function Settings() {
  const { data, isLoading, error } = useSettings()
  const update = useUpdateSetting()
  const [group, setGroup] = useState<string | null>(null)
  const [edits, setEdits] = useState<Record<string, string>>({})

  const groups = useMemo(() => [...new Set(data?.map((s) => s.group) ?? [])], [data])
  const active = group ?? groups[0] ?? ''
  const rows = data?.filter((s) => s.group === active) ?? []
  const dirty = rows.filter((s) => edits[s.key] !== undefined && edits[s.key] !== s.value)

  if (isLoading) return <p className="text-slate-500">Yükleniyor…</p>
  if (error) return <p className="text-red-600">Ayarlar yüklenemedi (süper admin gerekli).</p>

  async function save() {
    await Promise.all(dirty.map((s) => update.mutateAsync({ key: s.key, value: edits[s.key] })))
    setEdits({})
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <h1 className="text-lg font-semibold text-ink">Ayarlar</h1>
        <Button disabled={dirty.length === 0 || update.isPending} onClick={save}>
          Kaydet{dirty.length > 0 ? ` (${dirty.length})` : ''}
        </Button>
        {dirty.length > 0 && <Button variant="secondary" onClick={() => setEdits({})}>Vazgeç</Button>}
      </div>

      <div className="flex gap-4">
        <nav className="w-48 shrink-0">
          <Card className="overflow-hidden">
            {groups.map((g) => (
              <button
                key={g}
                onClick={() => setGroup(g)}
                className={`block w-full border-l-2 px-4 py-2 text-left text-sm transition-colors ${
                  g === active ? 'border-primary bg-primary/5 font-medium text-primary' : 'border-transparent text-slate-600 hover:bg-canvas'
                }`}
              >
                {GROUP_LABELS[g] ?? g}
              </button>
            ))}
          </Card>
        </nav>

        <Card className="flex-1 p-5">
          <h2 className="mb-4 font-semibold text-ink">{GROUP_LABELS[active] ?? active}</h2>
          <div className="grid gap-5 sm:grid-cols-2">
            {rows.map((s) => {
              const meta = SETTING_LABELS[s.key] ?? { label: s.key, help: '' }
              return (
                <div key={s.key}>
                  <div className="text-sm font-medium text-ink">{meta.label}</div>
                  <p className="mb-1 text-xs text-slate-400">{meta.help}</p>
                  <Input
                    value={edits[s.key] ?? s.value}
                    onChange={(e) => setEdits({ ...edits, [s.key]: e.target.value })}
                  />
                </div>
              )
            })}
          </div>
        </Card>
      </div>
    </div>
  )
}
