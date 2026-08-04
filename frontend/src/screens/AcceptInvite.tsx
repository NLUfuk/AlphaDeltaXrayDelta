import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, toApiError } from '../lib/api'
import { errorMessage } from '../lib/messages'
import { Alert, Button, Field, Icon, Input } from '../ui/primitives'

// Account activation (spec §9). Reached from the emailed link (?token=...) — for both a first-time
// customer and an invited staff member. Setting a password here proves the address and activates the
// account; then they log in. Same endpoint for both; the token decides who they are server-side.
export default function AcceptInvite() {
  const [params] = useSearchParams()
  const token = params.get('token') ?? ''
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [done, setDone] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    if (password !== confirm) return setError('Parolalar eşleşmiyor.')
    setError(null)
    setBusy(true)
    try {
      await api.post('/invitations/accept', { token, newPassword: password })
      setDone(true)
    } catch (err) {
      const { code, message } = toApiError(err)
      setError(errorMessage(code, message))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas p-4">
      <div className="w-full max-w-sm rounded-lg bg-surface p-8 shadow-card">
        {done ? (
          <div className="text-center">
            <Icon name="check-circle-outline" className="text-4xl text-emerald-500" />
            <h1 className="mt-2 text-lg font-semibold text-ink">Hesabınız etkinleştirildi</h1>
            <p className="mt-1 text-sm text-muted">Artık parolanızla giriş yapabilirsiniz.</p>
            <Link to="/login" className="mt-4 inline-block font-medium text-primary hover:underline">Giriş yap →</Link>
          </div>
        ) : !token ? (
          <Alert>Geçersiz bağlantı — token bulunamadı. Lütfen e-postanızdaki bağlantıyı kullanın.</Alert>
        ) : (
          <form onSubmit={submit} className="space-y-4">
            <h1 className="text-xl font-semibold text-ink">Parola belirle</h1>
            <p className="text-sm text-muted">Hesabınızı etkinleştirmek için bir parola belirleyin.</p>
            {error && <Alert>{error}</Alert>}
            <Field label="Parola">
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required autoFocus minLength={8} />
            </Field>
            <Field label="Parola (tekrar)">
              <Input type="password" value={confirm} onChange={(e) => setConfirm(e.target.value)} required minLength={8} />
            </Field>
            <Button type="submit" className="w-full" disabled={busy}>
              {busy ? 'Kaydediliyor…' : 'Hesabımı etkinleştir'}
            </Button>
          </form>
        )}
      </div>
    </div>
  )
}
