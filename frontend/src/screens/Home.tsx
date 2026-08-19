import { Link } from 'react-router-dom'
import { useAuth, type User } from '../lib/auth'
import { useReportCompany, useSelectableCompanies } from '../lib/company'
import { greeting, priority as priorityOf, statusCategory } from '../lib/messages'
import { useNotifications } from '../lib/notifications'
import { useReport } from '../lib/reports'
import { useAssignedTickets, useModeration, useMyTickets, type TicketListItem } from '../lib/tickets'
import { BarList } from '../ui/charts'
import { MarkAllSeen, NotificationList } from '../ui/NotificationList'
import { Badge, LoadError, Loading, Panel, Tile } from '../ui/primitives'

// The landing screen. It used to be a two-line dispatcher that dropped staff straight onto the board
// and customers onto a bare list — the same screen for a super admin, a company admin, a support agent
// and a customer, none of whom open the app for the same reason. Now each of the four gets the summary
// their role actually answers to, and the board/list they used to land on is one click away.
//
// Every panel below reads an endpoint that already existed (report, moderation queue, ticket list,
// notification feed); no summary endpoint was added for this screen.

/** Open = not in a terminal category (Closed=4 / Cancelled=5) — the category is the engine, never the
 *  display name (spec §4.3). Same rule the report screen uses. */
const isOpen = (t: TicketListItem) => t.category !== 4 && t.category !== 5

export default function Home() {
  const { user } = useAuth()
  if (!user) return null // Shell already redirected; this only satisfies the type

  return (
    <div className="space-y-5">
      <Hello user={user} />
      <Overview user={user} />
      <Notifications />
    </div>
  )
}

function Hello({ user }: { user: User }) {
  const firstName = user.name.split(' ')[0] || user.name
  return (
    <div>
      <h1 className="text-xl font-semibold text-ink">{greeting()}, {firstName}</h1>
      <p className="text-sm text-muted">{roleLabel(user)}</p>
    </div>
  )
}

/** What the person is here as. Read from the same fields the rest of the app authorizes on, so the
 *  line cannot claim a role the session does not carry. */
function roleLabel(user: User): string {
  if (user.isSuperAdmin) return 'Süper yönetici — tüm şirketler'
  if (user.companies.some((c) => c.role === 1)) return 'Şirket yöneticisi'
  if (user.companies.length > 0) return 'Personel'
  return 'Müşteri'
}

function Overview({ user }: { user: User }) {
  if (user.isSuperAdmin) return <ManagerOverview superAdmin />
  if (user.companies.some((c) => c.role === 1)) return <ManagerOverview superAdmin={false} />
  if (user.companies.length > 0) return <StaffOverview userId={user.id} />
  return <CustomerOverview />
}

// ---- customer ----------------------------------------------------------------------------------

function CustomerOverview() {
  const { data, isLoading, error } = useMyTickets()
  if (isLoading) return <Loading />
  if (error) return <LoadError error={error} what="Talepleriniz" />

  const items = data?.items ?? []
  const open = items.filter(isOpen)
  const answered = items.filter((t) => t.category === 2).length

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        <Tile label="Açık talebiniz" value={open.length} accent="#2563eb" icon="folder-open-outline" to="/taleplerim" />
        <Tile label="Yanıtlanan" value={answered} accent="#7c3aed" icon="comment-check-outline" to="/taleplerim" />
        <Tile label="Toplam talebiniz" value={items.length} accent="#16a34a" icon="ticket-outline" to="/taleplerim" />
      </div>

      <Panel
        title="Son talepleriniz"
        action={<Link to="/taleplerim" className="text-sm font-medium text-primary hover:underline">Tümü →</Link>}
      >
        {items.length === 0 ? (
          <p className="text-sm text-muted">
            Henüz bir talebiniz yok. <Link to="/taleplerim" className="font-medium text-primary hover:underline">Taleplerim</Link>{' '}
            ekranından bir firmaya yazabilirsiniz.
          </p>
        ) : (
          <TicketRows items={items.slice(0, 5)} />
        )}
      </Panel>
    </div>
  )
}

// ---- personel ----------------------------------------------------------------------------------

function StaffOverview({ userId }: { userId: string }) {
  const { data, isLoading, error } = useAssignedTickets(userId)
  if (isLoading) return <Loading />
  if (error) return <LoadError error={error} what="Talepleriniz" />

  const items = data?.items ?? []
  const open = items.filter(isOpen)
  const urgent = open.filter((t) => t.priority >= 2)

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-3">
        <Tile label="Üzerinizdeki açık talep" value={open.length} accent="#2563eb" icon="clipboard-account-outline" to="/pano" />
        <Tile label="Yüksek/acil öncelikli" value={urgent.length} accent="#dc2626" icon="flag-outline" to="/pano" />
        <Tile label="Size atanan toplam" value={items.length} accent="#6b7280" icon="ticket-outline" to="/pano" />
      </div>

      <Panel
        title="Sıradaki işleriniz"
        action={<Link to="/pano" className="text-sm font-medium text-primary hover:underline">Panoya git →</Link>}
      >
        {open.length === 0 ? (
          <p className="text-sm text-muted">Üzerinizde açık talep yok. Panodan yeni bir talep alabilirsiniz.</p>
        ) : (
          // Most urgent first; the board orders by column, this list orders by what to pick up next.
          <TicketRows items={[...open].sort((a, b) => b.priority - a.priority).slice(0, 6)} />
        )}
      </Panel>
    </div>
  )
}

// ---- admin & super admin -----------------------------------------------------------------------

/** Both manage rather than work tickets, so both get the company's pulse. The difference is scope: a
 *  super admin's company picker also offers "Tüm şirketler", and only they see how many companies the
 *  system carries. The report call follows the picker exactly like the report screen does. */
function ManagerOverview({ superAdmin }: { superAdmin: boolean }) {
  const companyId = useReportCompany()
  const companies = useSelectableCompanies()
  const { data: r, isLoading, error } = useReport(companyId)
  // Moderation is per company; with "Tüm şirketler" picked there is no single queue to count.
  const { data: pending } = useModeration(companyId ?? undefined)

  if (isLoading) return <Loading />
  if (error || !r) return <LoadError error={error} what="Özet" />

  const open = r.byStatusCategory.filter((c) => c.category !== 4 && c.category !== 5).reduce((n, c) => n + c.count, 0)
  const staffRows = r.staffLoad
    .filter((s) => s.openCount > 0)
    .slice(0, 6)
    .map((s) => ({ label: s.assignedToName ?? 'Atanmamış', value: s.openCount, color: '#714b67' }))

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <Tile label="Açık talep" value={open} accent="#2563eb" icon="folder-open-outline" to="/pano" />
        <Tile label="Toplam talep" value={r.totalTickets} accent="#6b7280" icon="ticket-outline" to="/reports" />
        {companyId ? (
          <Tile
            label="Onay bekleyen" value={pending?.length ?? '—'} accent="#d97706"
            icon="inbox-arrow-down-outline" to="/moderation"
          />
        ) : (
          <Tile label="Şirket" value={companies.length} accent="#d97706" icon="domain" to="/admin/companies" />
        )}
        <Tile
          label="Ort. ilk yanıt (saat)"
          value={r.avgFirstResponseHours === null ? '—' : r.avgFirstResponseHours.toLocaleString('tr-TR', { maximumFractionDigits: 1 })}
          sub={`${r.firstResponseCount} yanıtlanan talep`}
          accent="#16a34a" icon="timer-outline" to="/reports"
        />
      </div>

      {superAdmin && companyId && (
        <p className="text-xs text-muted">
          {companies.find((c) => c.id === companyId)?.name ?? 'Seçili şirket'} kapsamında. Tüm şirketler için üstteki
          seçiciden <b>Tüm şirketler</b>'i seçin.
        </p>
      )}

      <Panel
        title="Personel yükü (açık talep)"
        action={<Link to="/reports" className="text-sm font-medium text-primary hover:underline">Rapora git →</Link>}
      >
        {staffRows.length === 0
          ? <p className="text-sm text-muted">Açık talep yok.</p>
          : <BarList rows={staffRows} />}
      </Panel>
    </div>
  )
}

// ---- shared pieces -----------------------------------------------------------------------------

function TicketRows({ items }: { items: TicketListItem[] }) {
  return (
    <div className="space-y-2">
      {items.map((t) => {
        const cat = statusCategory(t.category)
        const p = priorityOf(t.priority)
        return (
          <Link
            key={t.id} to={`/tickets/${t.id}`}
            className="flex items-center justify-between gap-3 rounded-lg border border-line p-3 transition hover:border-primary"
          >
            <div className="min-w-0">
              <span className="text-xs font-medium text-muted">{t.number}</span>
              <p className="truncate text-sm text-ink">{t.title}</p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Badge label={t.statusName || cat.label} color={t.statusColor || cat.color} />
              <Badge label={p.label} color={p.color} />
            </div>
          </Link>
        )
      })}
    </div>
  )
}

/** The in-app half of the notification pipeline (spec §14): the same events that go out by e-mail,
 *  listed for whoever they were addressed to. The rows themselves live in ui/NotificationList so the
 *  navbar bell shows exactly this list, not a second implementation of it. */
function Notifications() {
  const { data } = useNotifications()
  const unread = data?.unreadCount ?? 0
  return (
    <Panel
      title={unread > 0 ? `Bildirimler (${unread} yeni)` : 'Bildirimler'}
      action={<MarkAllSeen unread={unread} />}
    >
      <NotificationList />
    </Panel>
  )
}
