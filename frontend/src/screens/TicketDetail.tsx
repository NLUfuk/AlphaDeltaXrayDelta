import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { useMembers } from '../lib/admin'
import { PRIORITIES, formatDateTime, priority, statusCategory } from '../lib/messages'
import {
  downloadAttachment, isImage, useAttachmentBlobUrl,
  useAddComment, useAssignTicket, useChangeTicketStatus, useDeleteTicket, useSetTicketPriority, useStatuses, useTicket, useUploadAttachment,
  type Attachment,
} from '../lib/tickets'
import { Badge, Button, Card, Icon, Input, Loading } from '../ui/primitives'

// Server-side allow-list mirrored for the picker; the backend re-checks bytes either way.
const ACCEPT = '.png,.jpg,.jpeg,.webp,.pdf,.txt,.doc,.docx'

/// An image attachment shown inline. The bytes come through the authed API (private bucket, no public
/// URL), so we render an object URL rather than pointing <img> at the endpoint.
function ImageThumb({ attachment }: { attachment: Attachment }) {
  const url = useAttachmentBlobUrl(attachment.id, true)
  if (!url)
    return <div className="h-24 w-24 animate-pulse rounded-md border border-line bg-canvas" title={attachment.fileName} />
  return (
    <a href={url} target="_blank" rel="noreferrer" title={attachment.fileName}>
      <img src={url} alt={attachment.fileName}
        className="h-24 w-24 rounded-md border border-line object-cover transition hover:border-primary" />
    </a>
  )
}

export default function TicketDetail() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
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
  const remove = useDeleteTicket(id, ticket?.companyId)
  const [body, setBody] = useState('')
  const [internal, setInternal] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  // ticket.delete is seeded to Admin/SuperAdmin, so mirror that to decide whether to offer the button.
  // ponytail: role check, not effective-permission check — an admin who explicitly denied ticket.delete
  // for someone still shows them the button and the server answers 403. Swap in /permissions/effective
  // if per-user overrides on this key ever become common.
  const canDelete = !!user && (user.isSuperAdmin ||
    user.companies.some((c) => c.companyId === ticket?.companyId && c.role === 1))

  if (isLoading) return <Loading />
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
      <Link to="/" className="inline-flex items-center gap-1 text-sm text-primary"><Icon name="arrow-left" />{isStaff ? 'Panoya dön' : 'Taleplerime dön'}</Link>

      <Card className="p-5">
        <div className="flex items-center gap-2">
          <span className="text-xs font-medium text-muted">{ticket.number}</span>
          <Badge label={cat.label} color={cat.color} />
          <Badge label={p.label} color={p.color} />
        </div>
        <h1 className="mt-2 text-lg font-semibold text-ink">{ticket.title}</h1>
        <p className="mt-2 whitespace-pre-wrap text-sm text-muted">{ticket.body}</p>
        {ticket.customFields.length > 0 && (
          <dl className="mt-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 border-t border-line pt-3 text-sm">
            {ticket.customFields.map((f, i) => (
              <div key={i} className="contents">
                <dt className="font-medium text-muted">{f.label}</dt>
                <dd className="text-ink">{f.value}</dd>
              </div>
            ))}
          </dl>
        )}
      </Card>

      {/* Customer sees a plain progress view (not the staff board): received → in progress → resolved. */}
      {!isStaff && <CustomerProgress category={ticket.category} />}

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
          {canDelete && (
            <div className="ml-auto">
              {confirmDelete ? (
                <div className="flex items-center gap-2">
                  <span className="text-sm text-muted">Talep silinsin mi?</span>
                  <Button variant="secondary" onClick={() => setConfirmDelete(false)} disabled={remove.isPending}>
                    Vazgeç
                  </Button>
                  <button
                    onClick={() => remove.mutate(undefined, { onSuccess: () => navigate('/') })}
                    disabled={remove.isPending}
                    className="inline-flex items-center gap-1.5 rounded-lg bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-60"
                  >
                    <Icon name="delete-outline" />{remove.isPending ? 'Siliniyor…' : 'Evet, sil'}
                  </button>
                </div>
              ) : (
                <button
                  onClick={() => setConfirmDelete(true)}
                  title="Talebi sil"
                  className="inline-flex items-center gap-1.5 rounded-lg border border-line px-3 py-2 text-sm text-muted hover:border-red-300 hover:text-red-600"
                >
                  <Icon name="delete-outline" />Sil
                </button>
              )}
            </div>
          )}
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
          <span className="text-xs font-semibold uppercase tracking-wide text-muted">Ekler</span>
          <label className="inline-flex cursor-pointer items-center gap-1 text-sm text-primary">
            <Icon name="paperclip" />
            {upload.isPending ? 'Yükleniyor…' : 'Dosya ekle'}
            <input type="file" accept={ACCEPT} className="hidden" onChange={pickFile} disabled={upload.isPending} />
          </label>
        </div>
        {ticket.attachments.length === 0 ? (
          <p className="text-sm text-muted">Ek yok.</p>
        ) : (
          <>
            {ticket.attachments.some((a) => isImage(a.contentType)) && (
              <div className="mb-3 flex flex-wrap gap-2">
                {ticket.attachments.filter((a) => isImage(a.contentType)).map((a) => (
                  <ImageThumb key={a.id} attachment={a} />
                ))}
              </div>
            )}
            <ul className="space-y-1">
              {ticket.attachments.filter((a) => !isImage(a.contentType)).map((a) => (
                <li key={a.id}>
                  <button
                    type="button"
                    onClick={() => downloadAttachment(a.id, a.fileName)}
                    className="inline-flex items-center gap-1 text-sm text-primary hover:underline"
                  >
                    <Icon name="download" />{a.fileName}
                    <span className="text-xs text-muted">({Math.max(1, Math.round(a.size / 1024))} KB)</span>
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
        {upload.isError && <p className="mt-1 text-sm text-red-600">Yükleme başarısız (tip veya boyut).</p>}
        <p className="mt-2 text-xs text-muted">Görsel (PNG, JPG, WEBP) ve belge (PDF, TXT, DOC, DOCX) yükleyebilirsiniz.</p>
      </Card>

      <div className="space-y-2">
        {ticket.comments.map((c) => (
          <div key={c.id} className={`rounded-lg p-3 text-sm shadow-sm ${c.isInternal ? 'bg-amber-50 text-amber-900' : 'bg-surface text-ink'}`}>
            <div className="mb-1 flex flex-wrap items-center gap-2 text-xs text-muted">
              <span className="font-semibold text-ink">{c.authorName}</span>
              {c.isInternal && <Badge label="İç not" color="#d97706" />}
              <span>{formatDateTime(c.createdAt)}</span>
              {c.isEdited && <span>(düzenlendi)</span>}
            </div>
            <p className="whitespace-pre-wrap">{c.body}</p>
          </div>
        ))}
        {ticket.comments.length === 0 && <p className="text-sm text-muted">Henüz yorum yok.</p>}
      </div>

      <Card className="p-4">
        <form onSubmit={send} className="space-y-2">
          <Input value={body} onChange={(e) => setBody(e.target.value)} placeholder="Yorum yaz…" />
          <div className="flex items-center justify-between">
            {isStaff ? (
              <label className="flex items-center gap-2 text-sm text-muted">
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

// Customer-facing progress: collapse the six status categories into three plain stages the customer
// understands. Not the staff kanban — just "received → in progress → resolved" (Cancelled is off-path).
const CUSTOMER_STAGES = ['Talebiniz alındı', 'İşlemde', 'Sonuçlandı'] as const
function customerStage(category: number): number {
  if (category === 0) return 0 // Open
  if (category === 4) return 2 // Closed
  return 1 // Pending / Answered / Waiting → in progress
}

function CustomerProgress({ category }: { category: number }) {
  if (category === 5) // Cancelled
    return (
      <Card className="flex items-center gap-2 p-4 text-sm text-muted">
        <Icon name="close-circle-outline" className="text-muted" />Bu talep iptal edildi.
      </Card>
    )
  const current = customerStage(category)
  return (
    <Card className="p-4">
      <div className="flex items-center">
        {CUSTOMER_STAGES.map((label, i) => (
          <div key={i} className={i < CUSTOMER_STAGES.length - 1 ? 'flex flex-1 items-center' : 'flex items-center'}>
            <div className="flex flex-col items-center gap-1">
              <span className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold ${i <= current ? 'bg-primary text-white' : 'bg-canvas text-muted'}`}>
                {i < current ? '✓' : i + 1}
              </span>
              <span className={`text-center text-xs ${i <= current ? 'font-medium text-ink' : 'text-muted'}`}>{label}</span>
            </div>
            {i < CUSTOMER_STAGES.length - 1 && <div className={`mx-2 h-0.5 flex-1 ${i < current ? 'bg-primary' : 'bg-line'}`} />}
          </div>
        ))}
      </div>
    </Card>
  )
}

function Control({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1">
      <span className="text-xs font-semibold uppercase tracking-wide text-muted">{label}</span>
      {children}
    </label>
  )
}

function Select({ value, onChange, children }: { value: string; onChange: (v: string) => void; children: React.ReactNode }) {
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)} className="rounded-md border border-line bg-surface px-3 py-2 text-sm outline-none focus:border-primary">
      {children}
    </select>
  )
}
