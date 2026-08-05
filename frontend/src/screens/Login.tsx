import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { errorText } from '../lib/messages'
import { Alert, Button, Field, Input } from '../ui/primitives'

export default function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    setBusy(true)
    try {
      await login(email, password)
      navigate('/', { replace: true })
    } catch (err) {
      setError(errorText(err))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-canvas">
      <form onSubmit={submit} className="w-full max-w-sm space-y-4 rounded-lg bg-surface p-8 shadow-card">
        <h1 className="text-xl font-semibold text-ink">Giriş</h1>
        {error && <Alert>{error}</Alert>}
        <Field label="E-posta">
          <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required autoFocus />
        </Field>
        <Field label="Parola">
          <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </Field>
        <Button type="submit" className="w-full" disabled={busy}>
          {busy ? 'Giriş yapılıyor…' : 'Giriş yap'}
        </Button>
        <p className="text-center text-sm text-muted">
          <Link to="/forgot-password" className="font-medium text-primary hover:underline">Parolamı unuttum</Link>
        </p>
        <p className="text-center text-sm text-muted">
          Hesabın yok mu? <Link to="/register" className="font-medium text-primary hover:underline">Kayıt ol</Link>
        </p>
      </form>
    </div>
  )
}
