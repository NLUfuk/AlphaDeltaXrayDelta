import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { priority, statusCategory } from '../lib/messages'
import { useAddComment, useTicket } from '../lib/tickets'
import { Badge, Button, Input } from '../ui/primitives'

export default function TicketDetail() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const isStaff = !!user && (user.isSuperAdmin || user.companies.length > 0)
  const { data: ticket, isLoading, error } = useTicket(id)
  const addComment = useAddComment(id)
  const [body, setBody] = useState('')
  const [internal, setInternal] = useState(false)

  if (isLoading) return <p className="text-slate-500">Yükleniyor…</p>
  if (error || !ticket) return <p className="text-red-600">Ticket yüklenemedi.</p>

  const cat = statusCategory(ticket.category)
  const p = priority(ticket.priority)

  function send(e: React.FormEvent) {
    e.preventDefault()
    if (!body.trim()) return
    addComment.mutate({ body, isInternal: internal }, { onSuccess: () => setBody('') })
  }

  return (
    <div className="mx-auto max-w-2xl space-y-4">
      <Link to="/" className="text-sm text-blue-600">← Panoya dön</Link>

      <div className="rounded-lg bg-white p-5 shadow-sm">
        <div className="flex items-center gap-2">
          <span className="text-xs font-medium text-slate-400">{ticket.number}</span>
          <Badge label={cat.label} color={cat.color} />
          <Badge label={p.label} color={p.color} />
        </div>
        <h1 className="mt-2 text-lg font-semibold text-slate-800">{ticket.title}</h1>
        <p className="mt-2 whitespace-pre-wrap text-sm text-slate-600">{ticket.body}</p>
      </div>

      <div className="space-y-2">
        {ticket.comments.map((c) => (
          <div
            key={c.id}
            className={`rounded-md p-3 text-sm ${c.isInternal ? 'bg-amber-50 text-amber-900' : 'bg-white text-slate-700'} shadow-sm`}
          >
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

      <form onSubmit={send} className="space-y-2 rounded-lg bg-white p-4 shadow-sm">
        <Input value={body} onChange={(e) => setBody(e.target.value)} placeholder="Yorum yaz…" />
        <div className="flex items-center justify-between">
          {isStaff ? (
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" checked={internal} onChange={(e) => setInternal(e.target.checked)} />
              İç not (müşteri görmez)
            </label>
          ) : (
            <span />
          )}
          <Button type="submit" disabled={addComment.isPending}>Gönder</Button>
        </div>
      </form>
    </div>
  )
}
