import { STATUS_CATEGORIES } from '../lib/messages'
import { useReportCompany } from '../lib/company'
import { useReport } from '../lib/reports'
import { Card, Icon, LoadError, Loading } from '../ui/primitives'
import { BarList, RevenueChart } from '../ui/charts'

// "Açık hat" is the open (undecided) pipeline, so it borrows the 'open' status-category blue instead of
// an amber that read as a third traffic-light next to won-green and lost-red.
const OPEN = STATUS_CATEGORIES[0].color

// Revenue tab (Faz 39). The board is an opportunity pipeline, so won/lost is a projection of the
// status category — Closed = won, Cancelled = lost — not a second field someone has to keep in sync.
// Every figure here comes from the report endpoint, which withholds `revenue` entirely when the
// caller lacks ticket.value; there is nothing to hide client-side.
export default function Revenue() {
  const companyId = useReportCompany()
  const { data: r, isLoading, error } = useReport(companyId)

  // Currency arrives with the figures (the /settings endpoint is super-admin only, and an amount
  // without its unit is not an amount).
  const money = (n: number) =>
    new Intl.NumberFormat('tr-TR', {
      style: 'currency', currency: r?.revenue?.currency ?? 'TRY', maximumFractionDigits: 0,
    }).format(n)
  const percent = (v: number | null) =>
    v === null ? '—' : `%${(v * 100).toLocaleString('tr-TR', { maximumFractionDigits: 1 })}`

  if (isLoading) return <Loading />
  if (error || !r) return <LoadError error={error} what="Rapor" />

  if (!r.revenue)
    return (
      <Card className="flex items-center gap-3 p-6 text-sm text-muted">
        <Icon name="lock-outline" className="text-lg" />
        Tutarları görme yetkiniz yok (<code>ticket.value</code>). Yöneticiniz Yetkiler ekranından açabilir.
      </Card>
    )

  const v = r.revenue
  const decided = v.wonTotal + v.lostTotal

  return (
    <div className="space-y-5">
      <div>
        <h1 className="text-lg font-semibold text-ink">{companyId ? 'Kazanç' : 'Kazanç (tüm şirketler)'}</h1>
        <p className="text-sm text-muted">
          Tamamlanan talepler kazanılmış, iptal edilenler kaybedilmiş sayılır; gerisi açık hattır.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <Tile label="Kazanılan" value={money(v.wonTotal)} sub={`${v.wonCount} talep`} accent="#1baf7a" icon="trending-up" />
        <Tile label="Kaybedilen" value={money(v.lostTotal)} sub={`${v.lostCount} talep`} accent="#d64550" icon="trending-down" />
        <Tile label="Açık hat" value={money(v.openTotal)} sub={`${v.openCount} talep`} accent={OPEN} icon="progress-clock" />
        {/* Sample size before the rate reads: "%100" over a single decided ticket is arithmetically
            true and tells a manager nothing, and without the denominator it looks like a claim. */}
        <Tile
          label="Kazanma oranı"
          value={percent(v.winRateByCount)}
          sub={`${v.wonCount + v.lostCount} talep üzerinden · tutarca ${percent(v.winRateByValue)}`}
          accent="#2a78d6"
          icon="percent-outline"
        />
      </div>

      {/* Count and value disagree whenever deal sizes vary — three small wins and one lost giant is a
          good month by count and a bad one by money. Showing only one number would flatter or scare. */}
      <div className="grid gap-4 lg:grid-cols-2">
        <Card className="p-4">
          <h2 className="mb-3 text-sm font-semibold text-ink">Aylık kazanılan / kaybedilen</h2>
          <RevenueChart data={v.trend} format={money} />
        </Card>

        <Card className="p-4">
          <h2 className="mb-3 text-sm font-semibold text-ink">Tutar dağılımı</h2>
          <BarList
            rows={[
              { label: 'Kazanılan', value: Math.round(v.wonTotal), color: '#1baf7a' },
              { label: 'Kaybedilen', value: Math.round(v.lostTotal), color: '#d64550' },
              { label: 'Açık hat', value: Math.round(v.openTotal), color: OPEN },
            ]}
          />
          <dl className="mt-4 space-y-1.5 border-t border-line pt-3 text-sm">
            <Row label="Karara bağlanan toplam" value={money(decided)} />
            {/* Sapma payı, oran değil: %100 tavan ve iki yöne de aynı bedeli ödetir. Bu yüzden yön
                bilgisi (üstü/altı) burada yok — o ayrı bir metrik olurdu. */}
            <Row
              label="Tahmin isabeti"
              value={v.forecastAccuracy === null ? '—' : percent(v.forecastAccuracy)}
              hint="Kapanan işlerde tahminin ne kadar tuttuğu: %100 = tam isabet. Sapma iki yönde de düşürür; tahminin iki katını aşan sapma %0 okunur."
            />
            <Row
              label="Tutarı girilmemiş talep"
              value={String(v.unpricedCount)}
              hint="Bu talepler hiçbir toplama katılmıyor — sıfır değil, 'henüz bilinmiyor' sayılıyor."
            />
          </dl>
        </Card>
      </div>
    </div>
  )
}

function Tile({ label, value, sub, accent, icon }: {
  label: string; value: string; sub: string; accent: string; icon: string
}) {
  return (
    <Card className="p-4">
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium uppercase tracking-wide text-muted">{label}</span>
        <span
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg"
          style={{ backgroundColor: `${accent}1a`, color: accent }}
        >
          <Icon name={icon} />
        </span>
      </div>
      <p className="mt-1 text-xl font-semibold tabular-nums text-ink">{value}</p>
      <p className="text-xs text-muted">{sub}</p>
    </Card>
  )
}

function Row({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="text-muted" title={hint}>{label}</dt>
      <dd className="tabular-nums font-medium text-ink">{value}</dd>
    </div>
  )
}
