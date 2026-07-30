import { useState } from 'react'
import { useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api, toApiError } from '../lib/api'
import { errorMessage } from '../lib/messages'
import { Alert, Button, Field, Input } from '../ui/primitives'

type FormConfig = { companyName: string; kvkkText: string; brandName: string; primaryColor: string; logoUrl: string | null }

// Anonymous public ticket form (spec §10). Reads super-admin-editable KVKK text + branding from the
// config endpoint (which sources the Settings store), then submits. KVKK consent is a hard gate.
export default function PublicForm() {
  const { slug = '' } = useParams()
  const { data: cfg, isLoading } = useQuery({
    queryKey: ['form-config', slug],
    queryFn: async () => (await api.get<FormConfig>(`/public/form/${slug}`)).data,
  })

  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', title: '', body: '' })
  const [consent, setConsent] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [result, setResult] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function set(k: keyof typeof form) {
    return (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [k]: e.target.value })
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      const { data } = await api.post(`/public/form/${slug}`, { ...form, kvkkConsent: consent })
      setResult(data.ticketNumber)
    } catch (err) {
      const { code, message } = toApiError(err)
      setError(errorMessage(code, message))
    } finally {
      setBusy(false)
    }
  }

  if (isLoading) return <p className="p-8 text-slate-500">Yükleniyor…</p>

  return (
    <div className="mx-auto max-w-lg p-6">
      <h1 className="text-xl font-semibold" style={{ color: cfg?.primaryColor }}>
        {cfg?.brandName} — {cfg?.companyName}
      </h1>

      {result ? (
        <Alert>
          <span className="text-green-700">Talebiniz alındı. Ticket no: <b>{result}</b>. E-postanıza kayıt bağlantısı gönderildi.</span>
        </Alert>
      ) : (
        <form onSubmit={submit} className="mt-4 space-y-3 rounded-lg bg-white p-6 shadow">
          {error && <Alert>{error}</Alert>}
          <div className="flex gap-3">
            <Field label="Ad"><Input value={form.firstName} onChange={set('firstName')} required /></Field>
            <Field label="Soyad"><Input value={form.lastName} onChange={set('lastName')} required /></Field>
          </div>
          <Field label="E-posta"><Input type="email" value={form.email} onChange={set('email')} required /></Field>
          <Field label="Konu"><Input value={form.title} onChange={set('title')} required /></Field>
          <Field label="Açıklama"><Input value={form.body} onChange={set('body')} required /></Field>

          <label className="flex items-start gap-2 text-xs text-slate-600">
            <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} className="mt-0.5" />
            <span>{cfg?.kvkkText}</span>
          </label>

          <Button type="submit" className="w-full" disabled={busy || !consent}>
            {busy ? 'Gönderiliyor…' : 'Talep oluştur'}
          </Button>
        </form>
      )}
    </div>
  )
}
