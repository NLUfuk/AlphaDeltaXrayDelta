import { useMemo, useState } from 'react'
import { errorText } from '../lib/messages'
import {
  GROUP_LABELS, OPTION_LABELS, SETTING_LABELS, useSettings, useUpdateSetting,
  type Setting, type SettingMeta,
} from '../lib/settings'
import { Alert, Button, Card, Icon, Input, LoadError, Loading, Select, Textarea } from '../ui/primitives'

/**
 * Super-admin settings (spec §13). One group is on screen at a time, picked from a dropdown, and every
 * row renders the control its declared `type` deserves — a number spinner, a colour swatch, a text area,
 * a real dropdown for the values the server enumerates. The store has carried `type` since Faz 1; this
 * screen used to throw it away and paint eleven identical text boxes, which is how "10 MB" ended up in
 * an int row and how "acil" could be typed into an enum the server then silently read back as Normal.
 *
 * Dirty rows are counted across ALL groups, not just the visible one: with a dropdown it is one click
 * to leave an unsaved edit behind, and the old per-group count reported "0 değişiklik" while holding it.
 */
export default function Settings() {
  const { data, isLoading, error } = useSettings()
  const update = useUpdateSetting()
  const [group, setGroup] = useState<string | null>(null)
  const [edits, setEdits] = useState<Record<string, string>>({})
  // The server rejects a value its type cannot hold (a unit typed into a number, CSV into a JSON list).
  // Without this the rejection was silent: the box kept the text and the save just did nothing.
  const [saveError, setSaveError] = useState<string | null>(null)

  const groups = useMemo(() => [...new Set(data?.map((s) => s.group) ?? [])], [data])
  const active = group ?? groups[0] ?? ''
  const rows = data?.filter((s) => s.group === active) ?? []
  const isDirty = (s: Setting) => edits[s.key] !== undefined && edits[s.key] !== s.value
  const dirty = data?.filter(isDirty) ?? []
  const dirtyElsewhere = dirty.filter((s) => s.group !== active)

  if (isLoading) return <Loading />
  if (error) return <LoadError error={error} what="Ayarlar" />

  async function save() {
    setSaveError(null)
    try {
      // Sequential, not Promise.all: on a rejection the rows before it are already persisted, and the
      // edits kept below are then exactly the ones still unsaved.
      for (const s of dirty) {
        await update.mutateAsync({ key: s.key, value: edits[s.key] })
        setEdits((prev) => {
          const { [s.key]: _saved, ...rest } = prev
          return rest
        })
      }
    } catch (err) {
      setSaveError(errorText(err))
    }
  }

  return (
    <div className="max-w-3xl space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-lg font-semibold text-ink">Ayarlar</h1>
        <label className="flex items-center gap-1.5" title="Ayar grubu">
          <Icon name="tune-variant" className="text-lg text-muted" />
          <Select value={active} onChange={(e) => setGroup(e.target.value)} aria-label="Ayar grubu" className="min-w-[10rem]">
            {groups.map((g) => (
              <option key={g} value={g}>
                {GROUP_LABELS[g] ?? g}
                {dirty.some((s) => s.group === g) ? ' •' : ''}
              </option>
            ))}
          </Select>
        </label>
        <div className="ml-auto flex items-center gap-2">
          {dirty.length > 0 && (
            <Button variant="secondary" onClick={() => { setEdits({}); setSaveError(null) }}>Vazgeç</Button>
          )}
          <Button disabled={dirty.length === 0 || update.isPending} onClick={save}>
            Kaydet{dirty.length > 0 ? ` (${dirty.length})` : ''}
          </Button>
        </div>
      </div>

      {saveError && <Alert>{saveError}</Alert>}
      {dirtyElsewhere.length > 0 && (
        <p className="text-xs text-muted">
          Başka gruplarda da kaydedilmemiş {dirtyElsewhere.length} değişiklik var
          ({[...new Set(dirtyElsewhere.map((s) => GROUP_LABELS[s.group] ?? s.group))].join(', ')}) — Kaydet hepsini yazar.
        </p>
      )}

      <Card className="divide-y divide-line">
        {rows.map((s) => {
          const meta = SETTING_LABELS[s.key] ?? { label: s.key, help: '' }
          return (
            <div key={s.key} className="grid gap-2 p-5 sm:grid-cols-[16rem_1fr] sm:items-start">
              <div>
                <div className="flex items-center gap-1.5 text-sm font-medium text-ink">
                  {meta.label}
                  {isDirty(s) && <span className="text-xs font-normal text-primary">• değişti</span>}
                </div>
                <p className="text-xs text-muted">{meta.help}</p>
              </div>
              <SettingControl
                setting={s}
                meta={meta}
                value={edits[s.key] ?? s.value}
                onChange={(v) => setEdits({ ...edits, [s.key]: v })}
              />
            </div>
          )
        })}
      </Card>
    </div>
  )
}

/**
 * The control for one row. Keyed on the row's declared `type` (plus `options` from the catalog), so a
 * new setting gets the right widget by seeding the right type — not by editing a switch in here.
 */
function SettingControl({
  setting, meta, value, onChange,
}: {
  setting: Setting
  meta: SettingMeta
  value: string
  onChange: (value: string) => void
}) {
  // A closed set the server enforces: the box must not be able to hold anything else.
  if (meta.options && meta.strict)
    return (
      <Select value={value} onChange={(e) => onChange(e.target.value)} className="w-full" aria-label={meta.label}>
        {meta.options.map((o) => <option key={o} value={o}>{OPTION_LABELS[o] ?? o}</option>)}
      </Select>
    )

  if (setting.type === 'color')
    return (
      <div className="flex items-center gap-2">
        {/* Native colour picker (no dependency) next to the literal value: the operator picks, and
            still sees the #rrggbb the server stores and validates. */}
        <input
          type="color"
          value={/^#[0-9a-fA-F]{6}$/.test(value) ? value : '#1f3bb3'}
          onChange={(e) => onChange(e.target.value)}
          aria-label={meta.label}
          className="h-9 w-12 cursor-pointer rounded-md border border-line bg-surface p-1"
        />
        <Input value={value} onChange={(e) => onChange(e.target.value)} className="max-w-[10rem]" spellCheck={false} />
      </div>
    )

  if (setting.type === 'int')
    return (
      <Input
        type="number" min={1} inputMode="numeric"
        value={value} onChange={(e) => onChange(e.target.value)}
        aria-label={meta.label} className="max-w-[10rem]"
      />
    )

  // json + html are the multi-line values (the MIME list, the KVKK notice).
  if (setting.type === 'json' || setting.type === 'html')
    return (
      <Textarea
        rows={setting.type === 'json' ? 4 : 6}
        value={value} onChange={(e) => onChange(e.target.value)}
        aria-label={meta.label}
        className={setting.type === 'json' ? 'font-mono text-xs' : ''}
        spellCheck={setting.type === 'html'}
      />
    )

  // Free text. `options` here is a suggestion list, not a limit — see SETTING_LABELS.
  const listId = meta.options ? `opts-${setting.key}` : undefined
  return (
    <>
      <Input
        value={value} onChange={(e) => onChange(e.target.value)}
        aria-label={meta.label} list={listId} spellCheck={false}
      />
      {listId && (
        <datalist id={listId}>
          {meta.options?.map((o) => <option key={o} value={o} />)}
        </datalist>
      )}
    </>
  )
}
