import { useState } from 'react'
import { errorText } from '../../lib/messages'
import {
  useCompanies, useCreateCompany, useDeleteCompany, useInvite, useMembers, useRemoveMember,
  useUpdateCompany, type Company, type CompanyInfo,
} from '../../lib/admin'
import { useAuth } from '../../lib/auth'
import { Alert, Button, Field, Icon, Input, LoadError, Select, Textarea } from '../../ui/primitives'
import { CustomerLink } from '../../ui/CustomerLink'

const EMPTY: CompanyInfo = { name: '', phone: '', email: '', website: '', address: '' }

/** The contact card, shared by the create and the edit form — the second real case, so it is one
 *  component rather than two drifting copies. Name is required; everything else is optional. */
function CompanyFields({ value, onChange }: { value: CompanyInfo; onChange: (v: CompanyInfo) => void }) {
  const set = (patch: Partial<CompanyInfo>) => onChange({ ...value, ...patch })
  return (
    <>
      <div className="flex flex-wrap gap-3">
        <Field label="Ad"><Input value={value.name} onChange={(e) => set({ name: e.target.value })} required /></Field>
        <Field label="Telefon">
          <Input type="tel" placeholder="0212 555 00 00" value={value.phone ?? ''} onChange={(e) => set({ phone: e.target.value })} />
        </Field>
      </div>
      <div className="flex flex-wrap gap-3">
        <Field label="E-posta">
          <Input type="email" placeholder="info@firma.com" value={value.email ?? ''} onChange={(e) => set({ email: e.target.value })} />
        </Field>
        <Field label="Web sitesi">
          <Input placeholder="www.firma.com" value={value.website ?? ''} onChange={(e) => set({ website: e.target.value })} />
        </Field>
      </div>
      <Field label="Adres">
        <Textarea rows={2} value={value.address ?? ''} onChange={(e) => set({ address: e.target.value })} />
      </Field>
    </>
  )
}

// Companies (spec §8/§9): an admin opens as many as they need; the list + members drive assignment/
// permission UIs. Deleting one is double-confirmed (see DeleteCompany) and soft — nothing cascades.
export default function Companies() {
  const { data: companies, error } = useCompanies()
  const create = useCreateCompany()
  const { refreshSession } = useAuth()
  const [form, setForm] = useState<CompanyInfo & { slug: string }>({ ...EMPTY, slug: '' })
  const [err, setErr] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    try {
      await create.mutateAsync(form)
      await refreshSession() // the new company's membership only reaches the app through a fresh token
      setForm({ ...EMPTY, slug: '' })
    } catch (e2) {
      setErr(errorText(e2))
    }
  }

  if (error) return <LoadError error={error} what="Şirketler" />

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-lg font-semibold text-ink">Şirketler</h1>

      <form onSubmit={submit} className="space-y-3 rounded-lg bg-surface p-4 shadow-sm">
        <h2 className="text-sm font-semibold text-muted">Yeni şirket</h2>
        {err && <Alert>{err}</Alert>}
        <CompanyFields value={form} onChange={(v) => setForm({ ...form, ...v })} />
        <Field label="Slug (form linki)">
          <Input value={form.slug} onChange={(e) => setForm({ ...form, slug: e.target.value })} required />
        </Field>
        {/* Said out loud on the create form because it is the one field that can never be corrected
            later: the customer link is built from it. */}
        <p className="text-xs text-muted">Slug daha sonra değiştirilemez — müşteri bağlantısı bu adrese kurulur.</p>
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
  const [editing, setEditing] = useState(false)
  // Same rule as the server: only the owner admin or a super admin may delete the tenant.
  const canDelete = !!user && (user.isSuperAdmin || user.id === company.ownerAdminId)
  // Editing is wider than deleting — any Admin of this company, exactly like the server's gate.
  const canEdit = !!user && (user.isSuperAdmin || user.companies.some((c) => c.companyId === company.id && c.role === 1))
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
          {canEdit && (
            <Button variant="secondary" onClick={() => setEditing(!editing)}>
              <Icon name="pencil-outline" className="mr-1" />{editing ? 'Kapat' : 'Düzenle'}
            </Button>
          )}
          <Button variant="secondary" onClick={() => setOpen(!open)}>{open ? 'Gizle' : 'Üyeler'}</Button>
          {canDelete && <DeleteCompany company={company} />}
        </div>
      </div>

      {editing
        ? <EditCompany company={company} onDone={() => setEditing(false)} />
        : <CompanyContact company={company} />}

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
              <Select value={inv.role} onChange={(e) => setInv({ ...inv, role: Number(e.target.value) })}>
                <option value={2}>Personel</option>
                <option value={1}>Admin</option>
              </Select>
            </Field>
            <Button type="submit" disabled={invite.isPending}>Davet et</Button>
          </form>
          {token && <div className="rounded-md bg-green-50 p-2 text-xs text-green-800">Davet token: <code className="break-all">{token}</code></div>}
        </div>
      )}
    </div>
  )
}

/** The contact card as read-only lines. Rows with no value are dropped rather than shown empty — an
 *  empty "Telefon: —" line is noise on a company that simply has no phone. Phone and e-mail are click-
 *  to-call/mail; the website gets its scheme prefixed here, since people type "www.acme.com". */
function CompanyContact({ company }: { company: Company }) {
  const rows: { icon: string; text: string; href?: string }[] = [
    company.phone ? { icon: 'phone-outline', text: company.phone, href: `tel:${company.phone.replace(/\s/g, '')}` } : null,
    company.email ? { icon: 'email-outline', text: company.email, href: `mailto:${company.email}` } : null,
    company.website
      ? { icon: 'web', text: company.website, href: /^https?:\/\//i.test(company.website) ? company.website : `https://${company.website}` }
      : null,
    company.address ? { icon: 'map-marker-outline', text: company.address } : null,
  ].filter((r) => r !== null)

  if (rows.length === 0) return null
  return (
    <dl className="mt-3 grid gap-1.5 text-sm text-muted sm:grid-cols-2">
      {rows.map((r) => (
        <div key={r.icon} className="flex items-start gap-2">
          <Icon name={r.icon} className="mt-0.5 text-base" />
          {r.href
            ? <a href={r.href} target="_blank" rel="noreferrer" className="break-all hover:text-primary hover:underline">{r.text}</a>
            : <span className="whitespace-pre-line">{r.text}</span>}
        </div>
      ))}
    </dl>
  )
}

/** Inline edit of name + contact card. The slug and the ticket prefix are not here: both are baked into
 *  links and ticket numbers customers already hold. */
function EditCompany({ company, onDone }: { company: Company; onDone: () => void }) {
  const update = useUpdateCompany()
  const [form, setForm] = useState<CompanyInfo>({
    name: company.name,
    phone: company.phone ?? '',
    email: company.email ?? '',
    website: company.website ?? '',
    address: company.address ?? '',
  })
  const [err, setErr] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    try {
      await update.mutateAsync({ ...form, id: company.id })
      onDone()
    } catch (e2) {
      setErr(errorText(e2))
    }
  }

  return (
    <form onSubmit={submit} className="mt-3 space-y-3 border-t border-line pt-3">
      {err && <Alert>{err}</Alert>}
      <CompanyFields value={form} onChange={setForm} />
      <div className="flex gap-2">
        <Button type="submit" disabled={update.isPending}>Kaydet</Button>
        <Button type="button" variant="secondary" onClick={onDone}>Vazgeç</Button>
      </div>
    </form>
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
