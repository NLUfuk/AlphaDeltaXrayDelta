import { useState } from 'react'
import { toApiError } from '../../lib/api'
import { errorMessage } from '../../lib/messages'
import { useCompanies, useCreateCompany, useInvite, useMembers, type Company } from '../../lib/admin'
import { Alert, Button, Field, Input } from '../../ui/primitives'

// Companies (spec §8/§9): an admin opens their own; the list + members drive assignment/permission UIs.
export default function Companies() {
  const { data: companies, error } = useCompanies()
  const create = useCreateCompany()
  const [form, setForm] = useState({ name: '', slug: '' })
  const [err, setErr] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    try {
      await create.mutateAsync(form)
      setForm({ name: '', slug: '' })
    } catch (e2) {
      const { code, message } = toApiError(e2)
      setErr(errorMessage(code, message))
    }
  }

  if (error) return <p className="text-red-600">Şirketler yüklenemedi.</p>

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold text-slate-800">Şirketler</h1>

      <form onSubmit={submit} className="space-y-3 rounded-lg bg-white p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-slate-600">Yeni şirket</h2>
        {err && <Alert>{err}</Alert>}
        <div className="flex gap-3">
          <Field label="Ad"><Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required /></Field>
          <Field label="Slug (form linki)"><Input value={form.slug} onChange={(e) => setForm({ ...form, slug: e.target.value })} required /></Field>
        </div>
        <Button type="submit" disabled={create.isPending}>Şirket oluştur</Button>
      </form>

      <div className="space-y-3">
        {companies?.map((c) => <CompanyCard key={c.id} company={c} />)}
      </div>
    </div>
  )
}

function CompanyCard({ company }: { company: Company }) {
  const [open, setOpen] = useState(false)
  const { data: members } = useMembers(open ? company.id : undefined)
  const invite = useInvite()
  const [inv, setInv] = useState({ email: '', firstName: '', lastName: '', role: 2 })
  const [token, setToken] = useState<string | null>(null)

  async function sendInvite(e: React.FormEvent) {
    e.preventDefault()
    const res = await invite.mutateAsync({ ...inv, companyId: company.id })
    setToken(res.rawToken || '(kullanıcı zaten aktif)')
    setInv({ email: '', firstName: '', lastName: '', role: 2 })
  }

  return (
    <div className="rounded-lg bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <div>
          <span className="font-medium text-slate-800">{company.name}</span>
          <span className="ml-2 text-xs text-slate-400">/{company.slug} · {company.ticketNumberPrefix}</span>
          {company.isArchived && <span className="ml-2 text-xs text-red-500">arşivli</span>}
        </div>
        <Button className="bg-slate-200 text-slate-700 hover:bg-slate-300" onClick={() => setOpen(!open)}>
          {open ? 'Gizle' : 'Üyeler'}
        </Button>
      </div>

      {open && (
        <div className="mt-3 space-y-3 border-t pt-3">
          <ul className="text-sm text-slate-600">
            {members?.map((m) => (
              <li key={m.userId}>{m.name} — {m.email} <span className="text-slate-400">({m.role === 1 ? 'Admin' : 'Personel'})</span></li>
            ))}
          </ul>

          <form onSubmit={sendInvite} className="flex flex-wrap items-end gap-2">
            <Field label="Ad"><Input className="w-28" value={inv.firstName} onChange={(e) => setInv({ ...inv, firstName: e.target.value })} required /></Field>
            <Field label="Soyad"><Input className="w-28" value={inv.lastName} onChange={(e) => setInv({ ...inv, lastName: e.target.value })} required /></Field>
            <Field label="E-posta"><Input className="w-48" type="email" value={inv.email} onChange={(e) => setInv({ ...inv, email: e.target.value })} required /></Field>
            <Field label="Rol">
              <select className="rounded-md border border-slate-300 px-2 py-2" value={inv.role} onChange={(e) => setInv({ ...inv, role: Number(e.target.value) })}>
                <option value={2}>Personel</option>
                <option value={1}>Admin</option>
              </select>
            </Field>
            <Button type="submit" disabled={invite.isPending}>Davet et</Button>
          </form>
          {token && <div className="rounded-md bg-green-50 p-2 text-xs text-green-800">Davet token: <code className="break-all">{token}</code></div>}
        </div>
      )}
    </div>
  )
}
