import { useState } from 'react'
import { errorText } from '../../lib/messages'
import {
  useCompanies, useCreateCompany, useDeleteCompany, useInvite, useMembers, useRemoveMember,
  useUpdateCompany, type Company, type CompanyInfo,
} from '../../lib/admin'
import { useAuth } from '../../lib/auth'
import { Alert, Button, Field, Icon, IconAction, Input, LoadError, Modal, Select, Textarea } from '../../ui/primitives'
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
//
// The screen is a LIST. It used to be a list with a permanently open create form on top of it and,
// inside every row, an inline edit form, a member list, an invite form and a delete panel that could
// all be expanded at once — five forms competing for one column. Each of those is a detour from
// "which companies do we have", so each is now a dialog opened from the row it belongs to. Nothing
// was removed; the customer link even gained a home here, moved off the kanban board where it was
// daily furniture for a once-per-company action.
export default function Companies() {
  const { data: companies, error } = useCompanies()
  const [creating, setCreating] = useState(false)

  if (error) return <LoadError error={error} what="Şirketler" />

  return (
    <div className="max-w-3xl space-y-4">
      <header className="flex items-center justify-between gap-3">
        <h1 className="text-lg font-semibold text-ink">Şirketler</h1>
        <Button onClick={() => setCreating(true)} className="gap-1.5"><Icon name="plus" />Yeni şirket</Button>
      </header>

      <div className="space-y-3">
        {companies?.map((c) => <CompanyCard key={c.id} company={c} />)}
        {companies?.length === 0 && (
          <p className="rounded-lg border border-dashed border-line p-6 text-center text-sm text-muted">
            Henüz şirket yok. <b>Yeni şirket</b> ile ilkini açın.
          </p>
        )}
      </div>

      <CreateCompany open={creating} onClose={() => setCreating(false)} />
    </div>
  )
}

function CreateCompany({ open, onClose }: { open: boolean; onClose: () => void }) {
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
      onClose()
    } catch (e2) {
      setErr(errorText(e2))
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Yeni şirket">
      <form onSubmit={submit} className="space-y-3">
        {err && <Alert>{err}</Alert>}
        <CompanyFields value={form} onChange={(v) => setForm({ ...form, ...v })} />
        <Field label="Slug (form linki)">
          <Input value={form.slug} onChange={(e) => setForm({ ...form, slug: e.target.value })} required />
        </Field>
        {/* Said out loud on the create form because it is the one field that can never be corrected
            later: the customer link is built from it. */}
        <p className="text-xs text-muted">Slug daha sonra değiştirilemez — müşteri bağlantısı bu adrese kurulur.</p>
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" disabled={create.isPending}>Şirket oluştur</Button>
        </div>
      </form>
    </Modal>
  )
}

/** One row: identity, contact lines, and the actions — each of which opens a dialog rather than
 *  growing the row. `panel` holds which one, so two can never be open at once. */
function CompanyCard({ company }: { company: Company }) {
  const { user } = useAuth()
  const [panel, setPanel] = useState<'edit' | 'members' | 'link' | 'delete' | null>(null)
  // Same rule as the server: only the owner admin or a super admin may delete the tenant.
  const canDelete = !!user && (user.isSuperAdmin || user.id === company.ownerAdminId)
  // Editing is wider than deleting — any Admin of this company, exactly like the server's gate.
  const canEdit = !!user && (user.isSuperAdmin || user.companies.some((c) => c.companyId === company.id && c.role === 1))
  const close = () => setPanel(null)

  return (
    <div className="rounded-lg bg-surface p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <span className="font-medium text-ink">{company.name}</span>
          <span className="ml-2 text-xs text-muted">/{company.slug} · {company.ticketNumberPrefix}</span>
          {company.isArchived && <span className="ml-2 text-xs text-red-500">arşivli</span>}
          <CompanyContact company={company} />
        </div>
        <div className="flex items-center gap-1">
          {canEdit && (
            <IconAction icon="pencil-outline" label="Düzenle" onClick={() => setPanel('edit')} />
          )}
          <IconAction icon="account-multiple-outline" label="Üyeler" onClick={() => setPanel('members')} />
          {!company.isArchived && (
            <IconAction icon="link-variant" label="Müşteri bağlantısı" onClick={() => setPanel('link')} />
          )}
          {canDelete && <IconAction icon="delete-outline" label="Sil" onClick={() => setPanel('delete')} danger />}
        </div>
      </div>

      <EditCompany company={company} open={panel === 'edit'} onClose={close} />
      <Members company={company} open={panel === 'members'} onClose={close} />
      <Modal open={panel === 'link'} onClose={close} title={`${company.name} — müşteri bağlantısı`}>
        <CustomerLink companyId={company.id} companyName={company.name} />
      </Modal>
      <DeleteCompany company={company} open={panel === 'delete'} onClose={close} />
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
    <dl className="mt-2 grid gap-1.5 text-sm text-muted sm:grid-cols-2">
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

/** Edit of name + contact card. The slug and the ticket prefix are not here: both are baked into
 *  links and ticket numbers customers already hold. */
function EditCompany({ company, open, onClose }: { company: Company; open: boolean; onClose: () => void }) {
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
      onClose()
    } catch (e2) {
      setErr(errorText(e2))
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={`${company.name} — bilgileri`}>
      <form onSubmit={submit} className="space-y-3">
        {err && <Alert>{err}</Alert>}
        <CompanyFields value={form} onChange={setForm} />
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button type="submit" disabled={update.isPending}>Kaydet</Button>
        </div>
      </form>
    </Modal>
  )
}

/** Members + invite. The list is only fetched while the dialog is open — it used to load for every
 *  expanded row on the page. */
function Members({ company, open, onClose }: { company: Company; open: boolean; onClose: () => void }) {
  const { data: members } = useMembers(open ? company.id : undefined)
  const invite = useInvite()
  const removeMember = useRemoveMember()
  const [inv, setInv] = useState({ email: '', firstName: '', lastName: '', role: 2 })
  const [token, setToken] = useState<string | null>(null)
  const [err, setErr] = useState<string | null>(null)

  async function sendInvite(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    try {
      const res = await invite.mutateAsync({ ...inv, companyId: company.id })
      setToken(res.rawToken || '(kullanıcı zaten aktif)')
      setInv({ email: '', firstName: '', lastName: '', role: 2 })
    } catch (e2) {
      setErr(errorText(e2))
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={`${company.name} — üyeler`} width="max-w-xl">
      <ul className="space-y-1 text-sm text-muted">
        {members?.map((m) => (
          <li key={m.userId} className="flex items-center justify-between gap-2 rounded-md px-2 py-1 hover:bg-canvas">
            <span>{m.name} — {m.email} <span className="text-muted">({m.role === 1 ? 'Admin' : 'Personel'})</span></span>
            {m.userId !== company.ownerAdminId && (
              <button
                onClick={() => removeMember.mutate({ companyId: company.id, userId: m.userId })}
                disabled={removeMember.isPending}
                className="shrink-0 text-xs text-red-600 hover:underline"
              >
                Çıkar
              </button>
            )}
          </li>
        ))}
        {members?.length === 0 && <li className="px-2 py-3 text-center text-xs">Henüz üye yok.</li>}
      </ul>

      <form onSubmit={sendInvite} className="mt-4 space-y-3 border-t border-line pt-4">
        <h3 className="text-sm font-semibold text-ink">Yeni üye davet et</h3>
        {err && <Alert>{err}</Alert>}
        <div className="flex flex-wrap gap-2">
          <Field label="Ad"><Input value={inv.firstName} onChange={(e) => setInv({ ...inv, firstName: e.target.value })} required /></Field>
          <Field label="Soyad"><Input value={inv.lastName} onChange={(e) => setInv({ ...inv, lastName: e.target.value })} required /></Field>
        </div>
        <div className="flex flex-wrap items-end gap-2">
          <Field label="E-posta"><Input type="email" value={inv.email} onChange={(e) => setInv({ ...inv, email: e.target.value })} required /></Field>
          <Field label="Rol">
            <Select value={inv.role} onChange={(e) => setInv({ ...inv, role: Number(e.target.value) })}>
              <option value={2}>Personel</option>
              <option value={1}>Admin</option>
            </Select>
          </Field>
        </div>
        <div className="flex justify-end">
          <Button type="submit" disabled={invite.isPending}>Davet et</Button>
        </div>
        {token && <div className="rounded-md bg-green-50 p-2 text-xs text-green-800">Davet token: <code className="break-all">{token}</code></div>}
      </form>
    </Modal>
  )
}

/** Two confirmations for a destructive action: opening this dialog is the first, typing the company
 *  name exactly (the server re-checks it) is the second. */
function DeleteCompany({ company, open, onClose }: { company: Company; open: boolean; onClose: () => void }) {
  const del = useDeleteCompany()
  const { refreshSession } = useAuth()
  const [typed, setTyped] = useState('')
  const [err, setErr] = useState<string | null>(null)

  async function remove() {
    setErr(null)
    try {
      await del.mutateAsync({ companyId: company.id, confirmName: typed })
      await refreshSession() // drop the deleted company from the session's tenant scope
      onClose()
    } catch (e) {
      setErr(errorText(e))
    }
  }

  return (
    <Modal open={open} onClose={onClose} title={`${company.name} silinsin mi?`}>
      <div className="space-y-3">
        <p className="text-sm text-ink">
          <b>{company.name}</b> silinecek. Talepler ve geçmiş veritabanında kalır, ancak şirket listelerden
          kalkar, müşteri linki kapanır ve tüm üyelikleri sona erer.
        </p>
        <p className="text-xs text-muted">Onaylamak için şirket adını yazın:</p>
        <Input value={typed} onChange={(e) => setTyped(e.target.value)} placeholder={company.name} autoFocus />
        {err && <Alert>{err}</Alert>}
        <div className="flex justify-end gap-2">
          <Button variant="secondary" onClick={onClose}>Vazgeç</Button>
          <Button
            variant="danger"
            onClick={remove}
            disabled={del.isPending || typed.trim().toLowerCase() !== company.name.toLowerCase()}
          >
            Kalıcı olarak sil
          </Button>
        </div>
      </div>
    </Modal>
  )
}
