import { useAuth } from '../lib/auth'
import { statusCategory } from '../lib/messages'
import { downloadCsv, useReport } from '../lib/reports'
import { Badge, Button } from '../ui/primitives'

// Report dashboard (spec §15). Super admin sees the global report; an admin sees their company.
export default function Dashboard() {
  const { user } = useAuth()
  const companyId = user?.isSuperAdmin ? null : (user?.companies[0]?.companyId ?? null)
  const { data: r, isLoading, error } = useReport(companyId)

  if (isLoading) return <p className="text-slate-500">Yükleniyor…</p>
  if (error || !r) return <p className="text-red-600">Rapor yüklenemedi (yetki gerekebilir).</p>

  return (
    <div className="max-w-3xl space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-slate-800">{companyId ? 'Şirket Raporu' : 'Global Rapor'}</h1>
        <Button variant="secondary" onClick={() => downloadCsv(companyId)}>CSV indir</Button>
      </div>

      <div className="grid grid-cols-3 gap-3">
        <Tile label="Toplam ticket" value={r.totalTickets} />
        <Tile label="Ort. ilk yanıt (saat)" value={r.avgFirstResponseHours ?? '—'} />
        <Tile label="Ort. çözüm (saat)" value={r.avgResolutionHours ?? '—'} />
      </div>

      <Section title="Statü dağılımı">
        {r.byStatusCategory.map((c) => {
          const cat = statusCategory(c.category)
          return (
            <div key={c.category} className="flex items-center gap-2 text-sm">
              <Badge label={cat.label} color={cat.color} />
              <span className="text-slate-600">{c.count}</span>
            </div>
          )
        })}
      </Section>

      <Section title="Personel yükü (açık ticket)">
        {r.staffLoad.length === 0 && <span className="text-sm text-slate-400">Kayıt yok.</span>}
        {r.staffLoad.map((s) => (
          <div key={s.assignedToId ?? 'none'} className="flex justify-between text-sm text-slate-600">
            <span>{s.assignedToId ?? 'Atanmamış'}</span>
            <span>{s.openCount}</span>
          </div>
        ))}
      </Section>
    </div>
  )
}

function Tile({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-lg bg-white p-4 shadow-sm">
      <div className="text-2xl font-semibold text-slate-800">{value}</div>
      <div className="text-xs text-slate-400">{label}</div>
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-2 rounded-lg bg-white p-4 shadow-sm">
      <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-400">{title}</h2>
      {children}
    </section>
  )
}
