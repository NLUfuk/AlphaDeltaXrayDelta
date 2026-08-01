import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { toApiError } from '../../lib/api'
import { errorMessage } from '../../lib/messages'
import { useAuth } from '../../lib/auth'
import { useCreateAdmin, useUsers, type UserRow } from '../../lib/admin'
import { Alert, Button, Field, Input } from '../../ui/primitives'

const roleLabel = (r: number) => (r === 1 ? 'Admin' : 'Personel')
const globalRole = (u: UserRow) => (u.isSuperAdmin ? 'Süper Admin' : u.canCreateCompany ? 'Admin' : 'Müşteri')

// Group the flat user list by company. A user in several companies appears under each; users with no
// membership (customers, super admins) fall into the "unassigned" bucket.
function groupByCompany(users: UserRow[]) {
  const map = new Map<string, { name: string; rows: { u: UserRow; role: number }[] }>()
  const unassigned: UserRow[] = []
  for (const u of users) {
    if (u.companies.length === 0) { unassigned.push(u); continue }
    for (const c of u.companies) {
      const g = map.get(c.companyId) ?? { name: c.companyName, rows: [] }
      g.rows.push({ u, role: c.role })
      map.set(c.companyId, g)
    }
  }
  return {
    companies: [...map.values()].sort((a, b) => a.name.localeCompare(b.name, 'tr')),
    unassigned,
  }
}

// Super-admin: create admin accounts + list users (spec §9). The invite token is shown after creation
// because email is a dev log sender in this environment — it's how the admin completes signup.
export default function AdminUsers() {
  const { data: users, error } = useUsers()
  const { user, impersonate } = useAuth()
  const navigate = useNavigate()
  const create = useCreateAdmin()
  const [form, setForm] = useState({ email: '', firstName: '', lastName: '' })
  const [token, setToken] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)

  async function stepInto(userId: string) {
    setErr(null)
    try {
      await impersonate(userId)
      navigate('/', { replace: true })
    } catch (e) {
      const { code, message } = toApiError(e)
      setErr(errorMessage(code, message))
    }
  }

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

      {(() => {
        const { companies, unassigned } = groupByCompany(users ?? [])

        const row = (u: UserRow, roleText: string) => (
          <tr key={u.id} className="border-t">
            <td className="py-1">{u.email}</td>
            <td>{u.name}</td>
            <td>{roleText}</td>
            <td>{u.isActive ? 'Aktif' : 'Bekliyor'}</td>
            <td className="text-right">
              {/* Impersonation is SuperAdmin-only and never targets another super admin or an inactive/self account. */}
              {!u.isSuperAdmin && u.isActive && u.id !== user?.id && (
                <button onClick={() => stepInto(u.id)} className="text-xs font-medium text-primary hover:underline">
                  Kimliğine gir
                </button>
              )}
            </td>
          </tr>
        )

        const table = (rows: React.ReactNode) => (
          <table className="w-full text-sm">
            <thead className="text-left text-xs text-slate-400">
              <tr><th className="py-1">E-posta</th><th>Ad</th><th>Rol</th><th>Durum</th><th></th></tr>
            </thead>
            <tbody>{rows}</tbody>
          </table>
        )

        return (
          <div className="space-y-4">
            {companies.map((g) => (
              <div key={g.name} className="rounded-lg bg-white p-4 shadow-sm">
                <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold text-slate-700">
                  {g.name}
                  <span className="rounded-full bg-slate-100 px-2 text-xs font-normal text-slate-500">{g.rows.length}</span>
                </h2>
                {table(g.rows.map(({ u, role }) => row(u, roleLabel(role))))}
              </div>
            ))}

            {unassigned.length > 0 && (
              <div className="rounded-lg bg-white p-4 shadow-sm">
                <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold text-slate-700">
                  Şirkete bağlı olmayan (müşteri / süper admin)
                  <span className="rounded-full bg-slate-100 px-2 text-xs font-normal text-slate-500">{unassigned.length}</span>
                </h2>
                {table(unassigned.map((u) => row(u, globalRole(u))))}
              </div>
            )}
          </div>
        )
      })()}
    </div>
  )
}
