import { useState } from 'react'
import { errorText } from '../../lib/messages'
import { useCompanies, useCreateCompany, useDeleteCompany, useInvite, useMembers, useRemoveMember, type Company } from '../../lib/admin'
import { useAuth } from '../../lib/auth'
import { Alert, Button, Field, Icon, Input } from '../../ui/primitives'
import { CustomerLink } from '../../ui/CustomerLink'

// Companies (spec §8/§9): an admin opens as many as they need; the list + members drive assignment/
// permission UIs. Deleting one is double-confirmed (see DeleteCompany) and soft — nothing cascades.
export default function Companies() {
  const { data: companies, error } = useCompanies()
  const create = useCreateCompany()
  const { refreshSession } = useAuth()
  const [form, setForm] = useState({ name: '', slug: '' })
  const [err, setErr] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    try {
      await create.mutateAsync(form)
      await refreshSession() // the new company's membership only reaches the app through a fresh token
      setForm({ name: '', slug: '' })
    } catch (e2) {
      setErr(errorText(e2))
    }
  }

  if (error) return <p className="text-red-600">Şirketler yüklenemedi.</p>

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold text-ink">Şirketler</h1>

      <form onSubmit={submit} className="space-y-3 rounded-lg bg-surface p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-muted">Yeni şirket</h2>
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
  const { user } = useAuth()
  const [open, setOpen] = useState(false)
  // Same rule as the server: only the owner admin or a super admin may delete the tenant.
  const canDelete = !!user && (user.isSuperAdmin || user.id === company.ownerAdminId)
  const { data: members } = useMembers(open ? company.id : undefined)
  const invite = useInvite()
  const removeMember = useRemoveMember()
  const [inv, setInv] = useState({ email: '', firstName: '', lastName: '', role: 2 })
  const [token, setToken] = useState<string | null>(null)

  async function sendInvite(e: React.FormEvent) {
    e.preventDefault()
    const res = await invite.mutateAsync({ ...inv, companyId: company.id })
    setToken(res.rawToken || '(kullanıcı zaten aktif)')
    setInv({ email: '', firstName: '', lastName: '', role: 2 })
  }

  return (
    <div className="rounded-lg bg-surface p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <div>
          <span className="font-medium text-ink">{company.name}</span>
          <span className="ml-2 text-xs text-muted">/{company.slug} · {company.ticketNumberPrefix}</span>
          {company.isArchived && <span className="ml-2 text-xs text-red-500">arşivli</span>}
        </div>
        <div className="relative flex items-center gap-2">
          <Button variant="secondary" onClick={() => setOpen(!open)}>{open ? 'Gizle' : 'Üyeler'}</Button>
          {canDelete && <DeleteCompany company={company} />}
        </div>
      </div>

      {!company.isArchived && (
        <div className="mt-3">
          <CustomerLink companyId={company.id} companyName={company.name} />
        </div>
      )}

      {open && (
        <div className="mt-3 space-y-3 border-t pt-3">
          <ul className="text-sm text-muted">
            {members?.map((m) => (
              <li key={m.userId} className="flex items-center gap-2 py-0.5">
                <span>{m.name} — {m.email} <span className="text-muted">({m.role === 1 ? 'Admin' : 'Personel'})</span></span>
                {m.userId !== company.ownerAdminId && (
                  <button
                    onClick={() => removeMember.mutate({ companyId: company.id, userId: m.userId })}
                    disabled={removeMember.isPending}
                    className="text-xs text-red-600 hover:underline"
                  >
                    Çıkar
                  </button>
                )}
              </li>
            ))}
          </ul>

          <form onSubmit={sendInvite} className="flex flex-wrap items-end gap-2">
            <Field label="Ad"><Input className="w-28" value={inv.firstName} onChange={(e) => setInv({ ...inv, firstName: e.target.value })} required /></Field>
            <Field label="Soyad"><Input className="w-28" value={inv.lastName} onChange={(e) => setInv({ ...inv, lastName: e.target.value })} required /></Field>
            <Field label="E-posta"><Input className="w-48" type="email" value={inv.email} onChange={(e) => setInv({ ...inv, email: e.target.value })} required /></Field>
            <Field label="Rol">
              <select className="rounded-md border border-line px-2 py-2" value={inv.role} onChange={(e) => setInv({ ...inv, role: Number(e.target.value) })}>
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

/** Two confirmations for a destructive action: "Sil" opens the panel, then the company name must be
 * typed exactly (the server re-checks it) before the delete fires. */
function DeleteCompany({ company }: { company: Company }) {
  const del = useDeleteCompany()
  const { refreshSession } = useAuth()
  const [confirming, setConfirming] = useState(false)
  const [typed, setTyped] = useState('')
  const [err, setErr] = useState<string | null>(null)

  async function remove() {
    setErr(null)
    try {
      await del.mutateAsync({ companyId: company.id, confirmName: typed })
      await refreshSession() // drop the deleted company from the session's tenant scope
    } catch (e) {
      setErr(errorText(e))
    }
  }

  if (!confirming)
    return (
      <Button variant="secondary" onClick={() => setConfirming(true)}>
        <Icon name="delete-outline" className="mr-1" />Sil
      </Button>
    )

  return (
    <div className="absolute right-0 top-full z-10 mt-2 w-80 space-y-2 rounded-lg border border-danger bg-surface p-3 shadow-lg">
      <p className="text-sm text-ink">
        <b>{company.name}</b> silinecek. Talepler ve geçmiş veritabanında kalır, ancak şirket listelerden
        kalkar, müşteri linki kapanır ve tüm üyelikleri sona erer.
      </p>
      <p className="text-xs text-muted">Onaylamak için şirket adını yazın:</p>
      <Input value={typed} onChange={(e) => setTyped(e.target.value)} placeholder={company.name} autoFocus />
      {err && <Alert>{err}</Alert>}
      <div className="flex gap-2">
        <Button
          variant="danger"
          onClick={remove}
          disabled={del.isPending || typed.trim().toLowerCase() !== company.name.toLowerCase()}
        >
          Kalıcı olarak sil
        </Button>
        <Button variant="secondary" onClick={() => { setConfirming(false); setTyped(''); setErr(null) }}>Vazgeç</Button>
      </div>
    </div>
  )
}
