import { useState } from 'react'
import { Link } from 'react-router-dom'
import { errorText, priority, statusCategory } from '../lib/messages'
import { useCreateCustomerTicket } from '../lib/public'
import { useMyCompanies, useMyTickets } from '../lib/tickets'
import { Alert, Badge, Button, Field, Icon, Input, LoadError, Loading, Select, Textarea } from '../ui/primitives'

// A customer's own ticket list (spec §17.4) + a "new message" composer that opens a request to a
// company they pick from the public list. Customers aren't company members, so this is their portal.
export default function CustomerTickets() {
  const { data, isLoading, error } = useMyTickets()
  const [composing, setComposing] = useState(false)

  if (isLoading) return <Loading />
  if (error) return <LoadError error={error} what="Talepler" />
  const items = data?.items ?? []

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <header className="flex items-center justify-between">
        <h1 className="text-lg font-semibold text-ink">Taleplerim</h1>
        <Button onClick={() => setComposing((v) => !v)}>
          <Icon name={composing ? 'close' : 'plus'} className="mr-1" />{composing ? 'Vazgeç' : 'Yeni mesaj'}
        </Button>
      </header>

      {composing && <NewMessage onDone={() => setComposing(false)} />}

      {items.length === 0 && !composing ? (
        <p className="text-sm text-muted">Henüz bir talebiniz yok. “Yeni mesaj” ile bir firmaya yazın.</p>
      ) : (
        <div className="space-y-2">
          {items.map((t) => {
            const cat = statusCategory(t.category)
            const p = priority(t.priority)
            return (
              <Link key={t.id} to={`/tickets/${t.id}`} className="block rounded-lg border border-line bg-surface p-4 shadow-sm transition hover:border-primary">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-medium text-muted">{t.number}</span>
                  <div className="flex gap-2">
                    {/* The company's column name, like everywhere else. The row already carried
                        statusName next to the statusColor it was using. */}
                    <Badge label={t.statusName || cat.label} color={t.statusColor || cat.color} />
                    <Badge label={p.label} color={p.color} />
                  </div>
                </div>
                <p className="mt-1 text-sm text-ink">{t.title}</p>
                <p className="mt-1 text-xs text-muted">{new Date(t.createdAt).toLocaleDateString('tr-TR')}</p>
              </Link>
            )
          })}
        </div>
      )}
    </div>
  )
}

function NewMessage({ onDone }: { onDone: () => void }) {
  const { data: companies, isLoading } = useMyCompanies()
  const create = useCreateCustomerTicket()
  const [form, setForm] = useState({ companyId: '', title: '', body: '' })
  const [error, setError] = useState<string | null>(null)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      await create.mutateAsync(form)
      onDone()
    } catch (err) {
      setError(errorText(err))
    }
  }

  // A customer can only message a company they already work with. With none yet, the portal has nothing
  // to open a request against — first contact is the company's own form link (spec §18.5).
  if (!isLoading && (companies?.length ?? 0) === 0)
    return (
      <div className="rounded-xl border border-line bg-surface p-5 text-sm text-muted">
        Henüz bir firmayla iletişiminiz yok. Yeni bir firmaya ilk talebinizi, o firmanın size verdiği
        <span className="font-medium text-ink"> talep formu bağlantısından</span> gönderin. Firma yanıt
        verdikten sonra buradan da o firmayla yazışabilirsiniz.
      </div>
    )

  return (
    <form onSubmit={submit} className="space-y-3 rounded-xl border border-line bg-surface p-5">
      {error && <Alert>{error}</Alert>}
      <Field label="Firma">
        <Select
          value={form.companyId}
          onChange={(e) => setForm({ ...form, companyId: e.target.value })}
          required
          className="w-full"
        >
          <option value="" disabled>Firma seçin…</option>
          {companies?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </Select>
      </Field>
      <Field label="Konu"><Input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} required /></Field>
      <Field label="Mesaj">
        <Textarea
          value={form.body}
          onChange={(e) => setForm({ ...form, body: e.target.value })}
          required
          rows={4}
        />
      </Field>
      <Button type="submit" disabled={create.isPending || !form.companyId}>
        <Icon name="send" className="mr-1" />{create.isPending ? 'Gönderiliyor…' : 'Gönder'}
      </Button>
    </form>
  )
}
