import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { errorText } from '../../lib/messages'
import { useAuth } from '../../lib/auth'
import { useAnonymizeUser, useCreateAdmin, useUsers, type UserRow } from '../../lib/admin'
import { Alert, Button, Card, Field, Icon, Input, LoadError, Select } from '../../ui/primitives'

// One vocabulary for "what kind of account is this" — the table, the filter and the badges all read it.
type Kind = 'super' | 'admin' | 'staff' | 'customer'
const KINDS: Record<Kind, { label: string; className: string }> = {
  super: { label: 'Süper Admin', className: 'bg-primary/10 text-primary' },
  admin: { label: 'Admin', className: 'bg-indigo-100 text-indigo-700' },
  staff: { label: 'Personel', className: 'bg-canvas text-muted' },
  customer: { label: 'Müşteri', className: 'bg-emerald-100 text-emerald-700' },
}

const kindOf = (u: UserRow, role?: number): Kind =>
  u.isSuperAdmin ? 'super' : role === 1 || (role === undefined && u.canCreateCompany) ? 'admin' : role === 2 ? 'staff' : 'customer'

/// Company groups (staff, by membership) + one bucket for everyone else, split by kind.
function group(users: UserRow[]) {
  const companies = new Map<string, { name: string; rows: { u: UserRow; role: number }[] }>()
  const customers: UserRow[] = []
  const others: UserRow[] = []
  for (const u of users) {
    if (u.companies.length > 0) {
      for (const c of u.companies) {
        const g = companies.get(c.companyId) ?? { name: c.companyName, rows: [] }
        g.rows.push({ u, role: c.role })
        companies.set(c.companyId, g)
      }
    } else if (u.isSuperAdmin || u.canCreateCompany) others.push(u)
    else customers.push(u)
  }
  return {
    companies: [...companies.values()].sort((a, b) => a.name.localeCompare(b.name, 'tr')),
    customers,
    others,
  }
}

// Super-admin console: create admin accounts, search every account, step into one, or erase it (KVKK).
export default function AdminUsers() {
  const [search, setSearch] = useState('')
  const [kindFilter, setKindFilter] = useState<'' | Kind>('')
  const [statusFilter, setStatusFilter] = useState<'' | 'active' | 'pending'>('')
  const { data: users, error, isFetching } = useUsers(search)
  const { user, impersonate } = useAuth()
  const navigate = useNavigate()
  const create = useCreateAdmin()
  const anonymize = useAnonymizeUser()
  const [form, setForm] = useState({ email: '', firstName: '', lastName: '' })
  const [invited, setInvited] = useState<{ email: string; link: string } | null>(null)
  const [err, setErr] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)

  const filtered = useMemo(() => (users ?? []).filter((u) => {
    if (statusFilter === 'active' && !u.isActive) return false
    if (statusFilter === 'pending' && u.isActive) return false
    if (!kindFilter) return true
    if (kindFilter === 'staff' || kindFilter === 'admin')
      return u.companies.some((c) => kindOf(u, c.role) === kindFilter)
    return kindOf(u) === kindFilter
  }), [users, kindFilter, statusFilter])

  const groups = group(filtered)

  async function run(fn: () => Promise<void>) {
    setErr(null)
    try {
      await fn()
    } catch (e) {
      setErr(errorText(e))
    }
  }

  const stepInto = (userId: string) => run(async () => {
    await impersonate(userId)
    navigate('/', { replace: true })
  })

  const erase = (u: UserRow) => run(() => anonymize.mutateAsync(u.id).then(() => {}))

  const submit = (e: React.FormEvent) => {
    e.preventDefault()
    setInvited(null)
    return run(async () => {
      const res = await create.mutateAsync(form)
      setInvited({ email: form.email, link: `${window.location.origin}/invite?token=${res.rawToken}` })
      setForm({ email: '', firstName: '', lastName: '' })
    })
  }

  if (error) return <LoadError error={error} what="Kullanıcılar" />

  return (
    <div className="space-y-4">
      <header className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-baseline gap-2">
          <h1 className="text-lg font-semibold text-ink">Kullanıcılar</h1>
          <span className="text-sm text-muted">{users?.length ?? 0} hesap{isFetching && ' · yükleniyor…'}</span>
        </div>
        <Button onClick={() => setShowCreate(!showCreate)} className="gap-1.5">
          <Icon name="account-plus-outline" />Yeni admin
        </Button>
      </header>

      {err && <Alert>{err}</Alert>}

      {showCreate && (
        <Card className="space-y-3 p-4">
          <h2 className="text-sm font-semibold text-muted">Yeni admin hesabı</h2>
          <p className="text-xs text-muted">
            Admin hesabını yalnız süper admin açar; davet e-postası gönderilir, admin parolasını belirleyip
            kendi şirketini oluşturur.
          </p>
          <form onSubmit={submit} className="space-y-3">
            <div className="flex gap-3">
              <Field label="Ad"><Input value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} required /></Field>
              <Field label="Soyad"><Input value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} required /></Field>
            </div>
            <Field label="E-posta"><Input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} required /></Field>
            <Button type="submit" disabled={create.isPending}>{create.isPending ? 'Oluşturuluyor…' : 'Oluştur ve davet gönder'}</Button>
          </form>
          {invited && <InviteResult email={invited.email} link={invited.link} />}
        </Card>
      )}

      <div className="flex flex-wrap items-center gap-2">
        <div className="w-64">
          <Input placeholder="Ara (ad / e-posta)…" value={search} onChange={(e) => setSearch(e.target.value)} />
        </div>
        <Select
          value={kindFilter} onChange={(e) => setKindFilter(e.target.value as '' | Kind)}
        >
          <option value="">Tür: tümü</option>
          {(Object.keys(KINDS) as Kind[]).map((k) => <option key={k} value={k}>{KINDS[k].label}</option>)}
        </Select>
        <Select
          value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as '' | 'active' | 'pending')}
        >
          <option value="">Durum: tümü</option>
          <option value="active">Aktif</option>
          <option value="pending">Davet bekliyor</option>
        </Select>
        {(search || kindFilter || statusFilter) && (
          <button onClick={() => { setSearch(''); setKindFilter(''); setStatusFilter('') }} className="text-sm text-muted hover:text-ink">
            Temizle
          </button>
        )}
      </div>

      {filtered.length === 0 && <p className="text-sm text-muted">Bu filtrelere uyan hesap yok.</p>}

      <div className="space-y-4">
        {groups.companies.map((g) => (
          <UserGroup key={g.name} title={g.name} icon="office-building-outline" count={g.rows.length}>
            {g.rows.map(({ u, role }) => (
              <UserTableRow key={u.id + role} u={u} kind={kindOf(u, role)} self={u.id === user?.id}
                onStepInto={stepInto} onErase={erase} />
            ))}
          </UserGroup>
        ))}

        {groups.customers.length > 0 && (
          <UserGroup title="Müşteriler" icon="account-multiple-outline" count={groups.customers.length}>
            {groups.customers.map((u) => (
              <UserTableRow key={u.id} u={u} kind="customer" self={u.id === user?.id}
                onStepInto={stepInto} onErase={erase} />
            ))}
          </UserGroup>
        )}

        {groups.others.length > 0 && (
          <UserGroup title="Şirketi olmayan yöneticiler" icon="shield-account-outline" count={groups.others.length}>
            {groups.others.map((u) => (
              <UserTableRow key={u.id} u={u} kind={kindOf(u)} self={u.id === user?.id}
                onStepInto={stepInto} onErase={erase} />
            ))}
          </UserGroup>
        )}
      </div>
    </div>
  )
}

function UserGroup({ title, icon, count, children }: { title: string; icon: string; count: number; children: React.ReactNode }) {
  return (
    <Card className="overflow-hidden">
      <h2 className="flex items-center gap-2 border-b border-line px-4 py-3 text-sm font-semibold text-ink">
        <Icon name={icon} className="text-muted" />{title}
        <span className="rounded-full bg-canvas px-2 text-xs font-normal text-muted">{count}</span>
      </h2>
      {/* Wide table scrolls inside the card instead of pushing the page sideways. */}
      <div className="overflow-x-auto">
        <table className="w-full min-w-[46rem] text-sm">
          <thead className="text-left text-xs uppercase tracking-wide text-muted">
            <tr className="border-b border-line">
              <th className="px-4 py-2 font-medium">Kullanıcı</th>
              <th className="px-2 py-2 font-medium">Tür</th>
              <th className="px-2 py-2 font-medium">Durum</th>
              <th className="px-2 py-2 font-medium">İlişkili firmalar</th>
              <th className="px-4 py-2" />
            </tr>
          </thead>
          <tbody>{children}</tbody>
        </table>
      </div>
    </Card>
  )
}

function UserTableRow({
  u, kind, self, onStepInto, onErase,
}: {
  u: UserRow
  kind: Kind
  self: boolean
  onStepInto: (id: string) => void
  onErase: (u: UserRow) => void
}) {
  const k = KINDS[kind]
  const [confirming, setConfirming] = useState(false)
  // Impersonation and erasure are SuperAdmin-only and never target a super admin or the caller itself
  // (the backend enforces both; the UI just doesn't offer what would be refused).
  const actionable = !u.isSuperAdmin && !self
  return (
    <tr className="border-b border-line last:border-0 hover:bg-canvas/60">
      <td className="px-4 py-2.5">
        <div className="flex items-center gap-2.5">
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-semibold text-primary">
            {u.name.split(' ').filter(Boolean).slice(0, 2).map((w) => w[0]).join('').toUpperCase()}
          </span>
          <span>
            <span className="block font-medium text-ink">{u.name}</span>
            <span className="block text-xs text-muted">{u.email}</span>
          </span>
        </div>
      </td>
      <td className="px-2 py-2.5">
        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${k.className}`}>{k.label}</span>
      </td>
      <td className="px-2 py-2.5">
        <span className={`text-xs font-medium ${u.isActive ? 'text-emerald-600' : 'text-amber-600'}`}>
          {u.isActive ? 'Aktif' : 'Davet bekliyor'}
        </span>
      </td>
      <td className="px-2 py-2.5 text-xs text-muted">
        {u.customerOf.length > 0 ? u.customerOf.join(', ') : u.companies.length > 0 ? '—' : 'henüz talep yok'}
      </td>
      <td className="px-4 py-2.5 text-right whitespace-nowrap">
        {/* Erasure is irreversible, so it takes a second click — inline, not a browser dialog. */}
        {confirming ? (
          <span className="inline-flex items-center gap-2">
            <span className="text-xs text-muted">Kişisel bilgiler maskelenir, talep geçmişi kalır.</span>
            <Button variant="danger" onClick={() => { setConfirming(false); onErase(u) }} className="gap-1 px-2.5 py-1 text-xs">
              <Icon name="check" />Onayla
            </Button>
            <Button variant="secondary" onClick={() => setConfirming(false)} className="px-2.5 py-1 text-xs">
              Vazgeç
            </Button>
          </span>
        ) : (
          <span className="inline-flex items-center gap-2">
            {actionable && u.isActive && (
              <Button
                variant="secondary" onClick={() => onStepInto(u.id)} className="gap-1 px-2.5 py-1 text-xs"
                title="Bu kullanıcının gördüğü ekrana geç"
              >
                <Icon name="account-switch-outline" />Kimliğine gir
              </Button>
            )}
            {actionable && (
              <Button
                variant="secondary" onClick={() => setConfirming(true)}
                className="gap-1 border-danger/40 px-2.5 py-1 text-xs text-danger hover:bg-danger/5"
                title="KVKK: kişisel bilgileri maskele ve hesabı kapat"
              >
                <Icon name="account-remove-outline" />Hesabı sil
              </Button>
            )}
          </span>
        )}
      </td>
    </tr>
  )
}

/// The invite link is emailed; it is shown here too because a super admin often onboards someone
/// over the phone — and because a bounced mail would otherwise leave no way to complete signup.
function InviteResult({ email, link }: { email: string; link: string }) {
  const [copied, setCopied] = useState(false)
  return (
    <div className="space-y-2 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
      <p><b>{email}</b> için hesap oluşturuldu; parola belirleme daveti e-postayla gönderildi.</p>
      <div className="flex flex-wrap items-center gap-2">
        <input readOnly value={link} onFocus={(e) => e.currentTarget.select()}
          className="min-w-0 flex-1 rounded-md border border-emerald-200 bg-surface px-2 py-1.5 text-xs text-ink" />
        <Button
          variant="secondary" type="button"
          onClick={async () => { await navigator.clipboard.writeText(link); setCopied(true); setTimeout(() => setCopied(false), 2000) }}
          className="gap-1.5 py-1.5"
        >
          <Icon name={copied ? 'check' : 'content-copy'} />{copied ? 'Kopyalandı' : 'Bağlantıyı kopyala'}
        </Button>
      </div>
    </div>
  )
}
