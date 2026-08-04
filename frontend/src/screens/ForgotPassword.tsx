import { useState } from 'react'
import { Link } from 'react-router-dom'
import { toApiError } from '../lib/api'
import { errorMessage } from '../lib/messages'
import { useForgotPassword } from '../lib/public'
import { Alert, Button, Field, Icon, Input } from '../ui/primitives'

// Self-service password reset request (spec §1.12). Collects the email only; the reset link is emailed
// and reuses the /invite set-password page. The response is uniform (no enumeration), so we always show
// the "check your email" state on success.
export default function ForgotPassword() {
  const forgot = useForgotPassword()
  const [email, setEmail] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await forgot.mutateAsync({ email })
      setDone(true)
    } catch (err) {
      const { code, message } = toApiError(err)
      setError(errorMessage(code, message))
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
              Bu adrese kayıtlı bir hesap varsa <b>{email}</b> adresine parola sıfırlama bağlantısı gönderdik.
              Bağlantıya tıklayıp yeni parolanı belirleyebilirsin.
            </p>
            <Link to="/login" className="mt-4 inline-block font-medium text-primary hover:underline">Giriş ekranına dön →</Link>
          </div>
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <h1 className="text-xl font-semibold text-ink">Parolamı unuttum</h1>
            <p className="text-sm text-muted">E-posta adresini gir; sana bir sıfırlama bağlantısı gönderelim.</p>
            {error && <Alert>{error}</Alert>}
            <Field label="E-posta">
              <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
            </Field>
            <Button type="submit" className="w-full" disabled={forgot.isPending}>
              {forgot.isPending ? 'Gönderiliyor…' : 'Sıfırlama bağlantısı gönder'}
            </Button>
            <p className="text-center text-sm text-muted">
              <Link to="/login" className="font-medium text-primary hover:underline">Giriş yap</Link>
            </p>
          </form>
        )}
      </div>
    </div>
  )
}
