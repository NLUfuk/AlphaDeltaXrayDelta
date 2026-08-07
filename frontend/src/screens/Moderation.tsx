import { Link } from 'react-router-dom'
import { ALL_COMPANIES, useActiveCompany } from '../lib/company'
import { useApproveTicket, useModeration, useRejectTicket } from '../lib/tickets'
import { Button, Card, Icon, Loading, PickCompany } from '../ui/primitives'

// Zero-trust intake queue (spec §10): first-time public submissions land here and stay out of the
// board until a staff member approves them. Reject dismisses for good.
export default function Moderation() {
  const companyId = useActiveCompany()
  const { data: pending, isLoading } = useModeration(companyId)
  const approve = useApproveTicket(companyId)
  const reject = useRejectTicket(companyId)

  if (companyId === ALL_COMPANIES) return <PickCompany what="Onay kuyruğu" />
  if (!companyId) return <p className="text-muted">Bu kullanıcı bir şirkete bağlı değil.</p>

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <header>
        <h1 className="text-lg font-semibold text-ink">Onay bekleyen talepler</h1>
        <p className="text-sm text-muted">
          Davetsiz gelen talepler havuza girmeden önce burada onaylanır. Bir müşteriyle düzenli
          çalışıyorsanız <b>Onayla + güven</b> deyin: sonraki talepleri doğrudan panoya düşer.
        </p>
      </header>

      {isLoading && <Loading className="text-sm" />}
      {pending && pending.length === 0 && (
        <Card className="flex items-center gap-3 p-6 text-sm text-muted">
          <Icon name="check-circle-outline" className="text-lg text-emerald-500" />
          Bekleyen talep yok.
        </Card>
      )}

      <div className="space-y-2">
        {pending?.map((t) => (
          <Card key={t.id} className="flex items-center gap-4 p-4">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2">
                <span className="text-xs tabular-nums text-muted">{t.number}</span>
                <Link to={`/tickets/${t.id}`} className="truncate font-medium text-ink hover:text-primary">{t.title}</Link>
              </div>
              <p className="text-xs text-muted">{new Date(t.createdAt).toLocaleString('tr-TR')}</p>
            </div>
            <Button variant="secondary" onClick={() => reject.mutate({ ticketId: t.id })} disabled={reject.isPending}>
              <Icon name="close" className="mr-1" />Reddet
            </Button>
            <Button variant="secondary" onClick={() => approve.mutate({ ticketId: t.id })} disabled={approve.isPending}>
              <Icon name="check" className="mr-1" />Onayla
            </Button>
            <Button
              onClick={() => approve.mutate({ ticketId: t.id, trust: true })}
              disabled={approve.isPending}
              title="Bu talebi onayla ve bu müşteriye artık güven — sonraki talepleri doğrudan panoya düşsün"
            >
              <Icon name="shield-check-outline" className="mr-1" />Onayla + güven
            </Button>
          </Card>
        ))}
      </div>
    </div>
  )
}
