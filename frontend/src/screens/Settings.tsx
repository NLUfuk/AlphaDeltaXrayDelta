import { useState } from 'react'
import { useSettings, useUpdateSetting, type Setting } from '../lib/settings'
import { Button, Input } from '../ui/primitives'

// Super-admin settings screen (spec §13). Groups the generic key/value rows; each row edits its value.
// Backend enforces the SuperAdmin gate — a non-admin gets 403 and sees the error below.
export default function Settings() {
  const { data, isLoading, error } = useSettings()
  if (isLoading) return <p className="text-slate-500">Yükleniyor…</p>
  if (error) return <p className="text-red-600">Ayarlar yüklenemedi (yetki gerekebilir).</p>

  const groups = [...new Set(data!.map((s) => s.group))]
  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold text-slate-800">Ayarlar</h1>
      {groups.map((g) => (
        <section key={g}>
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-slate-400">{g}</h2>
          <div className="space-y-2">
            {data!.filter((s) => s.group === g).map((s) => <Row key={s.key} setting={s} />)}
          </div>
        </section>
      ))}
    </div>
  )
}

function Row({ setting }: { setting: Setting }) {
  const [value, setValue] = useState(setting.value)
  const update = useUpdateSetting()
  const dirty = value !== setting.value
  return (
    <div className="flex items-center gap-3 rounded-md bg-white p-3 shadow-sm">
      <span className="w-56 shrink-0 text-sm text-slate-600">{setting.key}</span>
      <Input value={value} onChange={(e) => setValue(e.target.value)} />
      <Button
        className="shrink-0"
        disabled={!dirty || update.isPending}
        onClick={() => update.mutate({ key: setting.key, value })}
      >
        Kaydet
      </Button>
    </div>
  )
}
