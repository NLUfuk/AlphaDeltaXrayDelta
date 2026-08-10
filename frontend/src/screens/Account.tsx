import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { errorText, PASSWORD_HINT, passwordProblem } from '../lib/messages'
import { useAuth } from '../lib/auth'
import { useChangePassword, useDeleteAccount } from '../lib/account'
import { Alert, Badge, Button, Card, Field, Icon, Input } from '../ui/primitives'

// The logged-in user's own account: identity, password change, and (KVKK) account deletion.
export default function Account() {
  const { user } = useAuth()
  if (!user) return null

  const accountType = user.isSuperAdmin ? 'Süper Admin' : user.companies.length > 0 ? 'Personel / Yönetici' : 'Müşteri'

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <h1 className="text-lg font-semibold text-ink">Hesabım</h1>

      <Card className="p-5">
        <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm">
          <dt className="text-muted">Ad Soyad</dt><dd className="text-ink">{user.name}</dd>
          <dt className="text-muted">E-posta</dt><dd className="text-ink">{user.email}</dd>
          <dt className="text-muted">Hesap türü</dt><dd><Badge label={accountType} color="#4f46e5" /></dd>
          {user.companies.length > 0 && (
            <>
              <dt className="text-muted">Bağlı firma</dt>
              <dd className="text-ink">{user.companies.length} firma</dd>
            </>
          )}
        </dl>
      </Card>

      <ChangePasswordCard />

      {!user.isSuperAdmin && <DeleteAccountCard />}
    </div>
  )
}

function ChangePasswordCard() {
  const change = useChangePassword()
  const [f, setF] = useState({ currentPassword: '', newPassword: '', confirm: '' })
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null); setDone(false)
    // Was length-only, so an 8-character all-lowercase password passed here and was rejected by the
    // server's full rule set — as an unexplained 400. One shared check, same rules as the backend.
    const problem = passwordProblem(f.newPassword)
    if (problem) return setError(problem)
    if (f.newPassword !== f.confirm) return setError('Yeni parolalar eşleşmiyor.')
    try {
      await change.mutateAsync({ currentPassword: f.currentPassword, newPassword: f.newPassword })
      setDone(true); setF({ currentPassword: '', newPassword: '', confirm: '' })
    } catch (err) {
      setError(errorText(err))
    }
  }

  return (
    <Card className="p-5">
      <h2 className="mb-3 text-sm font-semibold text-ink">Parolayı değiştir</h2>
      <form onSubmit={submit} className="space-y-3">
        {error && <Alert>{error}</Alert>}
        {done && <div className="rounded-md bg-emerald-50 px-3 py-2 text-sm text-emerald-700">Parolanız güncellendi. Diğer oturumlar kapatıldı.</div>}
        <Field label="Mevcut parola"><Input type="password" value={f.currentPassword} onChange={(e) => setF({ ...f, currentPassword: e.target.value })} required /></Field>
        <Field label="Yeni parola">
          <Input type="password" value={f.newPassword} onChange={(e) => setF({ ...f, newPassword: e.target.value })} required autoComplete="new-password" />
          <span className="text-xs text-muted">{PASSWORD_HINT}</span>
        </Field>
        <Field label="Yeni parola (tekrar)"><Input type="password" value={f.confirm} onChange={(e) => setF({ ...f, confirm: e.target.value })} required /></Field>
        <Button type="submit" disabled={change.isPending}><Icon name="lock-reset" className="mr-1" />{change.isPending ? 'Kaydediliyor…' : 'Parolayı güncelle'}</Button>
      </form>
    </Card>
  )
}

function DeleteAccountCard() {
  const del = useDeleteAccount()
  const { logout } = useAuth()
  const navigate = useNavigate()
  const [open, setOpen] = useState(false)
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function confirmDelete(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await del.mutateAsync({ password })
      await logout() // tokens already revoked server-side; clear client + redirect
      navigate('/login', { replace: true })
    } catch (err) {
      setError(errorText(err))
    }
  }

  return (
    <Card className="border-red-200 p-5">
      <h2 className="mb-1 text-sm font-semibold text-red-700">Hesabı sil</h2>
      <p className="mb-3 text-sm text-muted">
        Hesabınız kapatılır ve kişisel bilgileriniz anonimleştirilir (talep geçmişi kayıt bütünlüğü için saklanır). Bu işlem geri alınamaz.
      </p>
      {!open ? (
        <Button variant="secondary" onClick={() => setOpen(true)}><Icon name="account-remove-outline" className="mr-1" />Hesabımı sil</Button>
      ) : (
        <form onSubmit={confirmDelete} className="space-y-3">
          {error && <Alert>{error}</Alert>}
          <Field label="Onaylamak için parolanızı girin">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </Field>
          <div className="flex gap-2">
            <Button variant="danger" type="submit" disabled={del.isPending}>
              <Icon name="alert-outline" className="mr-1" />{del.isPending ? 'Siliniyor…' : 'Kalıcı olarak sil'}
            </Button>
            <Button variant="secondary" type="button" onClick={() => { setOpen(false); setPassword(''); setError(null) }}>Vazgeç</Button>
          </div>
        </form>
      )}
    </Card>
  )
}
