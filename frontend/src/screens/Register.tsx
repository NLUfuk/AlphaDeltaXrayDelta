import { useState } from 'react'
import { Link } from 'react-router-dom'
import { errorText } from '../lib/messages'
import { useRegister } from '../lib/public'
import { Alert, Button, Field, Icon, Input } from '../ui/primitives'

// Self-service customer registration (spec §18.5). Collects identity only; the password is set on the
// emailed activation link (/invite). The response is uniform (no enumeration), so we always show the
// "check your email" state on success.
export default function Register() {
  const register = useRegister()
  const [form, setForm] = useState({ email: '', firstName: '', lastName: '' })
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  function set(k: keyof typeof form) {
    return (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [k]: e.target.value })
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await register.mutateAsync(form)
      setDone(true)
    } catch (err) {
      setError(errorText(err))
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas p-4">
      <div className="w-full max-w-sm rounded-lg bg-surface p-8 shadow-card">
        {done ? (
          <div className="text-center">
            <Icon name="email-check-outline" className="text-4xl text-emerald-500" />
            <h1 className="mt-2 text-lg font-semibold text-ink">E-postanı kontrol et</h1>
            <p className="mt-1 text-sm text-muted">
              <b>{form.email}</b> adresine hesabını etkinleştirme bağlantısı gönderdik. Bağlantıya tıklayıp
              parolanı belirledikten sonra giriş yapabilirsin.
            </p>
            <Link to="/login" className="mt-4 inline-block font-medium text-primary hover:underline">Giriş ekranına dön →</Link>
          </div>
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <h1 className="text-xl font-semibold text-ink">Kayıt ol</h1>
            <p className="text-sm text-muted">Kendi e-postanla hesap oluştur, taleplerini takip et ve firmalarla yazış.</p>
            {error && <Alert>{error}</Alert>}
            <div className="flex gap-3">
              <Field label="Ad"><Input value={form.firstName} onChange={set('firstName')} required autoFocus /></Field>
              <Field label="Soyad"><Input value={form.lastName} onChange={set('lastName')} required /></Field>
            </div>
            <Field label="E-posta"><Input type="email" value={form.email} onChange={set('email')} required /></Field>
            <Button type="submit" className="w-full" disabled={register.isPending}>
              {register.isPending ? 'Gönderiliyor…' : 'Kayıt ol'}
            </Button>
            <p className="text-center text-sm text-muted">
              Zaten hesabın var mı? <Link to="/login" className="font-medium text-primary hover:underline">Giriş yap</Link>
            </p>
          </form>
        )}
      </div>
    </div>
  )
}
