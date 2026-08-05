import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import { errorText } from '../lib/messages'
import { Alert, Button, Field, Icon, Input, Loading } from '../ui/primitives'

type PublicField = { id: string; label: string; type: number; required: boolean; options: string[] }
type FormConfig = { companyName: string; kvkkText: string; brandName: string; primaryColor: string; logoUrl: string | null; fields: PublicField[] }
type Descriptor = { key: string; fileName: string; contentType: string; size: number }

const ACCEPT = '.png,.jpg,.jpeg,.webp,.pdf,.txt,.doc,.docx'

// Anonymous public ticket form (spec §10). Branding + KVKK text come from the config endpoint. Files
// are uploaded through the API, which inspects the bytes (pdf/txt/doc/docx only) before storing —
// nothing else a customer picks is accepted. A first-time customer's ticket is held for staff approval.
export default function PublicForm() {
  const { slug = '' } = useParams()
  const { data: cfg, isLoading } = useQuery({
    queryKey: ['form-config', slug],
    queryFn: async () => (await api.get<FormConfig>(`/public/form/${slug}`)).data,
  })

  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', title: '', body: '' })
  const [customFields, setCustomFields] = useState<Record<string, string>>({})
  const [consent, setConsent] = useState(false)
  const [files, setFiles] = useState<Descriptor[]>([])
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<{ ticketNumber: string; newAccount: boolean } | null>(null)
  const [busy, setBusy] = useState(false)
  const [uploading, setUploading] = useState(false)

  function set(k: keyof typeof form) {
    return (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [k]: e.target.value })
  }

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const picked = Array.from(e.target.files ?? [])
    e.target.value = ''
    setError(null)
    setUploading(true)
    try {
      for (const file of picked) {
        const fd = new FormData()
        fd.append('file', file)
        const { data } = await api.post<Descriptor>(`/public/form/${slug}/upload`, fd, {
          headers: { 'Content-Type': 'multipart/form-data' },
        })
        setFiles((prev) => [...prev, data])
      }
    } catch (err) {
      setError(errorText(err))
    } finally {
      setUploading(false)
    }
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const { data } = await api.post(`/public/form/${slug}`, { ...form, kvkkConsent: consent, attachments: files, customFields })
      setResult({ ticketNumber: data.ticketNumber, newAccount: !!data.newAccount })
    } catch (err) {
      setError(errorText(err))
    } finally {
      setBusy(false)
    }
  }

  if (isLoading) return <Loading className="p-8" />
  const accent = cfg?.primaryColor ?? '#4f46e5'

  return (
    <div className="mx-auto max-w-lg p-6">
      <h1 className="text-xl font-semibold" style={{ color: accent }}>{cfg?.brandName} — {cfg?.companyName}</h1>

      {result ? (
        <div className="mt-4 rounded-xl border border-line bg-surface p-6">
          <Icon name="check-circle-outline" className="text-3xl text-emerald-500" />
          <p className="mt-2 text-ink">Talebiniz alındı. Ticket no: <b>{result.ticketNumber}</b>.</p>
          <p className="mt-1 text-sm text-muted">
            {result.newAccount
              ? 'Talebiniz ekibimizin onayının ardından işleme alınacaktır. Hesabınızı etkinleştirip taleplerinizi takip edebilmeniz için e-postanıza bir bağlantı gönderdik.'
              : 'Talebiniz mevcut hesabınıza eklendi. Giriş yaparak takip edebilirsiniz.'}
          </p>
        </div>
      ) : (
        <form onSubmit={submit} className="mt-4 space-y-3 rounded-xl border border-line bg-surface p-6">
          {error && <Alert>{error}</Alert>}
          <div className="flex gap-3">
            <Field label="Ad"><Input value={form.firstName} onChange={set('firstName')} required /></Field>
            <Field label="Soyad"><Input value={form.lastName} onChange={set('lastName')} required /></Field>
          </div>
          <Field label="E-posta"><Input type="email" value={form.email} onChange={set('email')} required /></Field>
          <Field label="Konu"><Input value={form.title} onChange={set('title')} required /></Field>
          <Field label="Açıklama"><Input value={form.body} onChange={set('body')} required /></Field>

          {cfg?.fields?.map((f) => {
            const val = customFields[f.id] ?? ''
            const onChange = (v: string) => setCustomFields((prev) => ({ ...prev, [f.id]: v }))
            return (
              <Field key={f.id} label={f.label + (f.required ? ' *' : '')}>
                {f.type === 3 ? (
                  <select
                    className="w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink"
                    value={val} onChange={(e) => onChange(e.target.value)} required={f.required}
                  >
                    <option value="">Seçiniz…</option>
                    {f.options.map((o) => <option key={o} value={o}>{o}</option>)}
                  </select>
                ) : f.type === 1 ? (
                  <textarea
                    className="w-full rounded-md border border-line bg-canvas px-3 py-2 text-sm text-ink"
                    value={val} onChange={(e) => onChange(e.target.value)} required={f.required}
                  />
                ) : (
                  <Input type={f.type === 2 ? 'number' : 'text'} value={val} onChange={(e) => onChange(e.target.value)} required={f.required} />
                )}
              </Field>
            )
          })}

          <div>
            <label className="flex cursor-pointer items-center gap-2 rounded-md border border-dashed border-line px-3 py-2 text-sm text-muted hover:border-primary">
              <Icon name="paperclip" />
              <span>{uploading ? 'Yükleniyor…' : 'Dosya/görsel ekle (PNG, JPG, WEBP, PDF, TXT, DOC, DOCX)'}</span>
              <input type="file" accept={ACCEPT} multiple onChange={onPick} disabled={uploading} className="hidden" />
            </label>
            {files.length > 0 && (
              <ul className="mt-2 space-y-1">
                {files.map((f, i) => (
                  <li key={f.key} className="flex items-center gap-2 text-xs text-ink">
                    <Icon name="file-document-outline" className="text-muted" />
                    <span className="flex-1 truncate">{f.fileName}</span>
                    <button type="button" onClick={() => setFiles(files.filter((_, j) => j !== i))} className="text-muted hover:text-red-600"><Icon name="close" /></button>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <label className="flex items-start gap-2 text-xs text-muted">
            <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} className="mt-0.5" />
            <span>{cfg?.kvkkText}</span>
          </label>

          <Button type="submit" className="w-full" disabled={busy || uploading || !consent} style={{ backgroundColor: accent }}>
            {busy ? 'Gönderiliyor…' : 'Talep oluştur'}
          </Button>
        </form>
      )}
    </div>
  )
}
