import { useAuth } from '../lib/auth'
import { useActiveCompany } from '../lib/company'
import { statusCategory } from '../lib/messages'
import { downloadCsv, useReport } from '../lib/reports'
import { Button, Card, Icon, Loading } from '../ui/primitives'
import { BarList, TrendChart } from '../ui/charts'

// Report dashboard (spec §15), StarAdmin-inspired: stat tiles + charts. Super admin sees the global
// report; an admin sees their company. Metrics branch on status category, never the display name (§4.3).
export default function Dashboard() {
  const { user } = useAuth()
  const active = useActiveCompany()
  const companyId = user?.isSuperAdmin ? null : (active ?? null)
  const { data: r, isLoading, error } = useReport(companyId)

  if (isLoading) return <Loading />
  if (error || !r) return <p className="text-red-600">Rapor yüklenemedi (yetki gerekebilir).</p>

  // Open = not in a terminal category (Closed=4 / Cancelled=5).
  const openCount = r.byStatusCategory.filter((c) => c.category !== 4 && c.category !== 5).reduce((n, c) => n + c.count, 0)
  const statusRows = r.byStatusCategory.map((c) => ({ label: statusCategory(c.category).label, value: c.count, color: statusCategory(c.category).color }))
  const staffRows = r.staffLoad.map((s) => ({ label: s.assignedToId ? s.assignedToId.slice(0, 8) : 'Atanmamış', value: s.openCount, color: '#714b67' }))

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold text-ink">{companyId ? 'Şirket Raporu' : 'Global Rapor'}</h1>
          <p className="text-sm text-muted">Ticket performans özeti</p>
        </div>
        <Button variant="secondary" onClick={() => downloadCsv(companyId)}><Icon name="download" className="mr-1" />CSV indir</Button>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <Tile label="Toplam ticket" value={r.totalTickets} accent="#2a78d6" icon="ticket-outline" />
        <Tile label="Açık ticket" value={openCount} accent="#eda100" icon="folder-open-outline" />
        <Tile label="Ort. ilk yanıt (saat)" value={r.avgFirstResponseHours ?? '—'} accent="#1baf7a" icon="timer-outline" />
        <Tile label="Ort. çözüm (saat)" value={r.avgResolutionHours ?? '—'} accent="#4a3aa7" icon="check-circle-outline" />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Panel title="Statü dağılımı"><BarList rows={statusRows} /></Panel>
        <Panel title="Açılış / kapanış trendi"><TrendChart data={r.trend} /></Panel>
      </div>

      <Panel title="Personel yükü (açık ticket)"><BarList rows={staffRows} /></Panel>
    </div>
  )
}

function Tile({ label, value, accent, icon }: { label: string; value: number | string; accent: string; icon: string }) {
  return (
    <Card className="flex items-center gap-3 p-4">
      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg text-xl" style={{ backgroundColor: `${accent}1a`, color: accent }}>
        <Icon name={icon} />
      </span>
      <div>
        <div className="text-2xl font-semibold text-ink tabular-nums">{value}</div>
        <div className="text-xs text-muted">{label}</div>
      </div>
    </Card>
  )
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Card className="p-5">
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-muted">{title}</h2>
      {children}
    </Card>
  )
}
