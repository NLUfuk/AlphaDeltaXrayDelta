import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { useMembers } from '../lib/admin'
import { PRIORITIES, priority, statusCategory } from '../lib/messages'
import {
  downloadAttachment,
  useAddComment, useAssignTicket, useChangeTicketStatus, useSetTicketPriority, useStatuses, useTicket, useUploadAttachment,
} from '../lib/tickets'
import { Badge, Button, Card, Icon, Input } from '../ui/primitives'

export default function TicketDetail() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const isStaff = !!user && (user.isSuperAdmin || user.companies.length > 0)
  const { data: ticket, isLoading, error } = useTicket(id)
  const { data: statuses } = useStatuses(ticket?.companyId)
  const { data: members } = useMembers(isStaff ? ticket?.companyId : undefined)

  const changeStatus = useChangeTicketStatus(id, ticket?.companyId)
  const assign = useAssignTicket(id, ticket?.companyId)
  const setPriority = useSetTicketPriority(id, ticket?.companyId)
  const addComment = useAddComment(id)
  const upload = useUploadAttachment(id)
  const [body, setBody] = useState('')
  const [internal, setInternal] = useState(false)

  if (isLoading) return <p className="text-slate-500">Yükleniyor…</p>
  if (error || !ticket) return <p className="text-red-600">Ticket yüklenemedi.</p>

  const cat = statusCategory(ticket.category)
  const p = priority(ticket.priority)
  const isTerminal = ticket.category === 4 || ticket.category === 5
  const statusId = (c: number) => statuses?.find((s) => s.category === c)?.id

  function send(e: React.FormEvent) {
    e.preventDefault()
    if (!body.trim()) return
    addComment.mutate({ body, isInternal: internal }, { onSuccess: () => setBody('') })
  }

  function pickFile(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (file) upload.mutate(file)
    e.target.value = '' // allow re-selecting the same file
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <Link to="/" className="inline-flex items-center gap-1 text-sm text-primary"><Icon name="arrow-left" />Panoya dön</Link>

      <Card className="p-5">
        <div className="flex items-center gap-2">
          <span className="text-xs font-medium text-slate-400">{ticket.number}</span>
          <Badge label={cat.label} color={cat.color} />
          <Badge label={p.label} color={p.color} />
        </div>
        <h1 className="mt-2 text-lg font-semibold text-ink">{ticket.title}</h1>
        <p className="mt-2 whitespace-pre-wrap text-sm text-slate-600">{ticket.body}</p>
        {ticket.customFields.length > 0 && (
          <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 border-t border-line pt-3 text-sm">
            {ticket.customFields.map((f, i) => (
              <div key={i} className="contents">
                <dt className="font-medium text-slate-500">{f.label}</dt>
                <dd className="text-slate-700">{f.value}</dd>
              </div>
            ))}
          </dl>
        )}
      </Card>

      {/* Actions: staff manage status/assignee/priority; customer may cancel or complete a non-terminal ticket. */}
      {isStaff ? (
        <Card className="flex flex-wrap items-end gap-4 p-4">
          <Control label="Statü">
            <Select value={ticket.statusId} onChange={(v) => changeStatus.mutate(v)}>
              {statuses?.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </Select>
          </Control>
          <Control label="Atanan">
            <Select value={ticket.assignedToId ?? ''} onChange={(v) => assign.mutate(v || null)}>
              <option value="">— Atanmamış —</option>
              {members?.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
            </Select>
          </Control>
          <Control label="Öncelik">
            <Select value={String(ticket.priority)} onChange={(v) => setPriority.mutate(Number(v))}>
              {PRIORITIES.map((pr, i) => <option key={i} value={i}>{pr.label}</option>)}
            </Select>
          </Control>
        </Card>
      ) : (
        !isTerminal && (
          <Card className="flex gap-2 p-4">
            <Button variant="secondary" onClick={() => statusId(5) && changeStatus.mutate(statusId(5)!)}>
              <Icon name="close-circle-outline" className="mr-1" />İptal et
            </Button>
            <Button onClick={() => statusId(4) && changeStatus.mutate(statusId(4)!)}>
              <Icon name="check-circle-outline" className="mr-1" />Tamamlandı
            </Button>
          </Card>
        )
      )}

      <Card className="p-4">
        <div className="mb-2 flex items-center justify-between">
          <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">Ekler</span>
          <label className="inline-flex cursor-pointer items-center gap-1 text-sm text-primary">
            <Icon name="paperclip" />
            {upload.isPending ? 'Yükleniyor…' : 'Dosya ekle'}
            <input type="file" className="hidden" onChange={pickFile} disabled={upload.isPending} />
          </label>
        </div>
        {ticket.attachments.length === 0 ? (
          <p className="text-sm text-slate-400">Ek yok.</p>
        ) : (
          <ul className="space-y-1">
            {ticket.attachments.map((a) => (
              <li key={a.id}>
                <button
                  type="button"
                  onClick={() => downloadAttachment(a.id, a.fileName)}
                  className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                >
                  <Icon name="download" />{a.fileName}
                  <span className="text-xs text-slate-400">({Math.max(1, Math.round(a.size / 1024))} KB)</span>
                </button>
              </li>
            ))}
          </ul>
        )}
        {upload.isError && <p className="mt-1 text-sm text-red-600">Yükleme başarısız (tip veya boyut).</p>}
      </Card>

      <div className="space-y-2">
        {ticket.comments.map((c) => (
          <div key={c.id} className={`rounded-lg p-3 text-sm shadow-sm ${c.isInternal ? 'bg-amber-50 text-amber-900' : 'bg-white text-slate-700'}`}>
            <div className="mb-1 flex items-center gap-2 text-xs text-slate-400">
              {c.isInternal && <Badge label="İç not" color="#d97706" />}
              {new Date(c.createdAt).toLocaleString('tr-TR')}
              {c.isEdited && <span>(düzenlendi)</span>}
            </div>
            <p className="whitespace-pre-wrap">{c.body}</p>
          </div>
        ))}
        {ticket.comments.length === 0 && <p className="text-sm text-slate-400">Henüz yorum yok.</p>}
      </div>

      <Card className="p-4">
        <form onSubmit={send} className="space-y-2">
          <Input value={body} onChange={(e) => setBody(e.target.value)} placeholder="Yorum yaz…" />
          <div className="flex items-center justify-between">
            {isStaff ? (
              <label className="flex items-center gap-2 text-sm text-slate-600">
                <input type="checkbox" checked={internal} onChange={(e) => setInternal(e.target.checked)} />
                İç not (müşteri görmez)
              </label>
            ) : <span />}
            <Button type="submit" disabled={addComment.isPending}><Icon name="send" className="mr-1" />Gönder</Button>
          </div>
        </form>
      </Card>
    </div>
  )
}

function Control({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">{label}</span>
      {children}
    </label>
  )
}

function Select({ value, onChange, children }: { value: string; onChange: (v: string) => void; children: React.ReactNode }) {
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)} className="rounded-md border border-line bg-white px-3 py-2 text-sm outline-none focus:border-primary">
      {children}
    </select>
  )
}
