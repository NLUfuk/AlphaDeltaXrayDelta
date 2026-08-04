import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toApiError } from '../lib/api'
import { useAuth } from '../lib/auth'
import { errorMessage } from '../lib/messages'
import { useCustomerFormSubmit, useCustomerRegister, useFormConfig, useVerifyCode, type PublicField } from '../lib/public'
import { Alert, Button, Field, Icon, Input } from '../ui/primitives'

// A company's own customer entrance (/c/{slug}) — the link staff generate and send over WhatsApp.
// Sign up → 6-digit code from the email → session → first request. The request is what binds the
// customer to this company (relationship scope), after which the portal takes over.
type Step = 'signup' | 'code' | 'login' | 'compose' | 'done'

export default function CustomerAccess() {
  const { slug = '' } = useParams()
  const { data: cfg, isLoading } = useFormConfig(slug)
  const { user, login, adoptSession } = useAuth()

  const register = useCustomerRegister(slug)
  const verify = useVerifyCode(slug)
  const submit = useCustomerFormSubmit(slug)

  const [step, setStep] = useState<Step>(user ? 'compose' : 'signup')
  const [form, setForm] = useState({ firstName: '', lastName: '', email: '', password: '' })
  const [code, setCode] = useState('')
  const [request, setRequest] = useState({ title: '', body: '' })
  const [customFields, setCustomFields] = useState<Record<string, string>>({})
  const [consent, setConsent] = useState(false)
  const [ticketNumber, setTicketNumber] = useState('')
  const [error, setError] = useState<string | null>(null)

  const accent = cfg?.primaryColor ?? '#1F3BB3'
  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm({ ...form, [k]: e.target.value })

  async function run(fn: () => Promise<void>) {
    setError(null)
    try {
      await fn()
    } catch (err) {
      const { code: c, message } = toApiError(err)
      setError(errorMessage(c, message))
    }
  }

  const onSignup = (e: React.FormEvent) => {
    e.preventDefault()
    return run(async () => {
      await register.mutateAsync(form)
      setStep('code')
    })
  }

  const onVerify = (e: React.FormEvent) => {
    e.preventDefault()
    return run(async () => {
      adoptSession(await verify.mutateAsync({ email: form.email, code }))
      setStep('compose')
    })
  }

  const onLogin = (e: React.FormEvent) => {
    e.preventDefault()
    return run(async () => {
      await login(form.email, form.password)
      setStep('compose')
    })
  }

  const onSubmitRequest = (e: React.FormEvent) => {
    e.preventDefault()
    return run(async () => {
      const result = await submit.mutateAsync({ ...request, customFields })
      setTicketNumber(result.ticketNumber)
      setStep('done')
    })
  }

  if (isLoading) return <p className="p-8 text-muted">Yükleniyor…</p>
  if (!cfg) return <p className="p-8 text-muted">Bu bağlantıya ait firma bulunamadı.</p>

  return (
    <div className="min-h-screen bg-canvas p-4">
      <div className="mx-auto max-w-lg pt-8">
        <div className="mb-4 flex items-center gap-3">
          {cfg.logoUrl && <img src={cfg.logoUrl} alt="" className="h-10 w-10 rounded-lg object-contain" />}
          <div>
            <h1 className="text-xl font-semibold" style={{ color: accent }}>{cfg.companyName}</h1>
            <p className="text-sm text-muted">Müşteri portalı — {cfg.brandName}</p>
          </div>
        </div>

        <div className="rounded-xl border border-line bg-surface p-6 shadow-card">
          {error && <div className="mb-3"><Alert>{error}</Alert></div>}

          {step === 'signup' && (
            <form onSubmit={onSignup} className="space-y-3">
              <h2 className="text-lg font-semibold text-ink">Kayıt ol</h2>
              <p className="text-sm text-muted">
                Kaydolduktan sonra e-postanıza gelen 6 haneli kodu girerek hesabınızı doğrulayacaksınız.
              </p>
              <div className="flex gap-3">
                <Field label="Ad"><Input value={form.firstName} onChange={set('firstName')} required autoFocus /></Field>
                <Field label="Soyad"><Input value={form.lastName} onChange={set('lastName')} required /></Field>
              </div>
              <Field label="E-posta"><Input type="email" value={form.email} onChange={set('email')} required /></Field>
              <Field label="Parola">
                <Input type="password" value={form.password} onChange={set('password')} required minLength={8}
                  placeholder="En az 8 karakter, büyük/küçük harf ve rakam" />
              </Field>
              <label className="flex items-start gap-2 text-xs text-muted">
                <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} className="mt-0.5" />
                <span>{cfg.kvkkText}</span>
              </label>
              <Button type="submit" className="w-full" disabled={register.isPending || !consent} style={{ backgroundColor: accent }}>
                {register.isPending ? 'Gönderiliyor…' : 'Kayıt ol ve kod gönder'}
              </Button>
              <p className="text-center text-sm text-muted">
                Hesabınız var mı?{' '}
                <button type="button" onClick={() => { setError(null); setStep('login') }} className="font-medium text-primary hover:underline">
                  Giriş yapın
                </button>
              </p>
            </form>
          )}

          {step === 'code' && (
            <form onSubmit={onVerify} className="space-y-3">
              <Icon name="email-check-outline" className="text-3xl" />
              <h2 className="text-lg font-semibold text-ink">Doğrulama kodu</h2>
              <p className="text-sm text-muted">
                <b>{form.email}</b> adresine 6 haneli bir kod gönderdik. Kod 15 dakika geçerlidir.
              </p>
              <Input
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                inputMode="numeric" autoComplete="one-time-code" required autoFocus
                className="text-center text-2xl tracking-[0.5em]"
                placeholder="000000"
              />
              <Button type="submit" className="w-full" disabled={verify.isPending || code.length !== 6} style={{ backgroundColor: accent }}>
                {verify.isPending ? 'Doğrulanıyor…' : 'Doğrula ve devam et'}
              </Button>
              <button
                type="button"
                onClick={() => run(async () => { await register.mutateAsync(form); setCode('') })}
                className="w-full text-center text-sm text-muted hover:text-ink"
              >
                Kod gelmedi mi? Yeniden gönder
              </button>
            </form>
          )}

          {step === 'login' && (
            <form onSubmit={onLogin} className="space-y-3">
              <h2 className="text-lg font-semibold text-ink">Giriş yap</h2>
              <Field label="E-posta"><Input type="email" value={form.email} onChange={set('email')} required autoFocus /></Field>
              <Field label="Parola"><Input type="password" value={form.password} onChange={set('password')} required /></Field>
              <Button type="submit" className="w-full" style={{ backgroundColor: accent }}>Giriş yap</Button>
              <p className="text-center text-sm text-muted">
                Hesabınız yok mu?{' '}
                <button type="button" onClick={() => { setError(null); setStep('signup') }} className="font-medium text-primary hover:underline">
                  Kayıt olun
                </button>
              </p>
            </form>
          )}

          {step === 'compose' && (
            <form onSubmit={onSubmitRequest} className="space-y-3">
              <h2 className="text-lg font-semibold text-ink">{cfg.companyName} firmasına talebiniz</h2>
              <Field label="Konu">
                <Input value={request.title} onChange={(e) => setRequest({ ...request, title: e.target.value })} required autoFocus />
              </Field>
              <Field label="Mesajınız">
                <textarea
                  className="w-full rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink"
                  rows={5} value={request.body}
                  onChange={(e) => setRequest({ ...request, body: e.target.value })} required
                />
              </Field>
              {cfg.fields?.map((f) => (
                <CustomField key={f.id} field={f} value={customFields[f.id] ?? ''}
                  onChange={(v) => setCustomFields((prev) => ({ ...prev, [f.id]: v }))} />
              ))}
              <Button type="submit" className="w-full" disabled={submit.isPending} style={{ backgroundColor: accent }}>
                {submit.isPending ? 'Gönderiliyor…' : 'Talebi gönder'}
              </Button>
            </form>
          )}

          {step === 'done' && (
            <div className="text-center">
              <Icon name="check-circle-outline" className="text-4xl text-emerald-500" />
              <p className="mt-2 text-ink">Talebiniz <b>{cfg.companyName}</b> firmasına iletildi.</p>
              <p className="mt-1 text-sm text-muted">Talep no: <b>{ticketNumber}</b></p>
              <Link to="/" className="mt-4 inline-block font-medium text-primary hover:underline">Taleplerime git →</Link>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

// The company's configurable extra fields (spec §4.6) — same types the anonymous form renders.
function CustomField({ field, value, onChange }: { field: PublicField; value: string; onChange: (v: string) => void }) {
  const label = field.label + (field.required ? ' *' : '')
  if (field.type === 3)
    return (
      <Field label={label}>
        <select
          className="w-full rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink"
          value={value} onChange={(e) => onChange(e.target.value)} required={field.required}
        >
          <option value="">Seçiniz…</option>
          {field.options.map((o) => <option key={o} value={o}>{o}</option>)}
        </select>
      </Field>
    )
  if (field.type === 1)
    return (
      <Field label={label}>
        <textarea
          className="w-full rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink"
          value={value} onChange={(e) => onChange(e.target.value)} required={field.required}
        />
      </Field>
    )
  return (
    <Field label={label}>
      <Input type={field.type === 2 ? 'number' : 'text'} value={value}
        onChange={(e) => onChange(e.target.value)} required={field.required} />
    </Field>
  )
}
