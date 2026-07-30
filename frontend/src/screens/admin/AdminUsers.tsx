import { useState } from 'react'
import { toApiError } from '../../lib/api'
import { errorMessage } from '../../lib/messages'
import { useCreateAdmin, useUsers } from '../../lib/admin'
import { Alert, Button, Field, Input } from '../../ui/primitives'

// Super-admin: create admin accounts + list users (spec §9). The invite token is shown after creation
// because email is a dev log sender in this environment — it's how the admin completes signup.
export default function AdminUsers() {
  const { data: users, error } = useUsers()
  const create = useCreateAdmin()
  const [form, setForm] = useState({ email: '', firstName: '', lastName: '' })
  const [token, setToken] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    setToken(null)
    try {
      const res = await create.mutateAsync(form)
      setToken(res.rawToken)
      setForm({ email: '', firstName: '', lastName: '' })
    } catch (e2) {
      const { code, message } = toApiError(e2)
      setErr(errorMessage(code, message))
    }
  }

  if (error) return <p className="text-red-600">Kullanıcılar yüklenemedi (süper admin gerekli).</p>

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold text-slate-800">Kullanıcılar & Admin oluşturma</h1>

      <form onSubmit={submit} className="space-y-3 rounded-lg bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-600">Yeni admin</h2>
        {err && <Alert>{err}</Alert>}
        {token && (
          <div className="rounded-md bg-green-50 p-3 text-sm text-green-800">
            Admin oluşturuldu. Davet (şifre belirleme) token'ı:
            <code className="mt-1 block break-all font-mono text-xs">{token}</code>
          </div>
        )}
        <div className="flex gap-3">
          <Field label="Ad"><Input value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} required /></Field>
          <Field label="Soyad"><Input value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} required /></Field>
        </div>
        <Field label="E-posta"><Input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required /></Field>
        <Button type="submit" disabled={create.isPending}>Admin oluştur</Button>
      </form>

      <div className="rounded-lg bg-white p-4 shadow-sm">
        <h2 className="mb-2 text-sm font-semibold text-slate-600">Tüm kullanıcılar</h2>
        <table className="w-full text-sm">
          <thead className="text-left text-xs text-slate-400">
            <tr><th className="py-1">E-posta</th><th>Ad</th><th>Rol</th><th>Durum</th></tr>
          </thead>
          <tbody>
            {users?.map((u) => (
              <tr key={u.id} className="border-t">
                <td className="py-1">{u.email}</td>
                <td>{u.name}</td>
                <td>{u.isSuperAdmin ? 'Süper Admin' : u.canCreateCompany ? 'Admin' : 'Kullanıcı'}</td>
                <td>{u.isActive ? 'Aktif' : 'Bekliyor'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
