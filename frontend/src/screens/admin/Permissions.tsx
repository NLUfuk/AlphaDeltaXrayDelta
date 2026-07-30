import { useState } from 'react'
import { useAssignPermission, useCompanies, useEffective, useMembers, usePermissionCatalog } from '../../lib/admin'
import { Badge, Button } from '../../ui/primitives'

// RBAC assignment UI (spec §7/§18.4): pick a company + member, then grant/deny permissions. The escalation
// guard (assign only what you hold, only in your company) is enforced server-side — a denied assign 403s.
export default function Permissions() {
  const { data: companies } = useCompanies()
  const [companyId, setCompanyId] = useState<string>('')
  const [userId, setUserId] = useState<string>('')
  const { data: members } = useMembers(companyId || undefined)
  const { data: catalog } = usePermissionCatalog()
  const { data: effective } = useEffective(userId || undefined, companyId || undefined)
  const assign = useAssignPermission()

  const has = (key: string) => effective?.permissions.includes(key) ?? false
  const groups = [...new Set(catalog?.map((p) => p.group) ?? [])]

  function act(permissionKey: string, type: number) {
    if (userId && companyId) assign.mutate({ userId, companyId, permissionKey, type })
  }

  return (
    <div className="max-w-2xl space-y-4">
      <h1 className="text-lg font-semibold text-slate-800">Yetki atama</h1>

      <div className="flex gap-3">
        <select className="rounded-md border border-slate-300 px-3 py-2" value={companyId}
          onChange={(e) => { setCompanyId(e.target.value); setUserId('') }}>
          <option value="">Şirket seç…</option>
          {companies?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <select className="rounded-md border border-slate-300 px-3 py-2" value={userId}
          onChange={(e) => setUserId(e.target.value)} disabled={!companyId}>
          <option value="">Üye seç…</option>
          {members?.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
        </select>
      </div>

      {userId && (
        <div className="space-y-4">
          {groups.map((g) => (
            <section key={g} className="rounded-lg bg-white p-4 shadow-sm">
              <h2 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-400">{g}</h2>
              <div className="space-y-2">
                {catalog!.filter((p) => p.group === g).map((p) => (
                  <div key={p.key} className="flex items-center justify-between text-sm">
                    <span className="flex items-center gap-2">
                      {p.key}
                      {has(p.key) ? <Badge label="var" color="#16a34a" /> : <Badge label="yok" color="#9ca3af" />}
                    </span>
                    <span className="flex gap-2">
                      <Button className="px-2 py-1 text-xs" onClick={() => act(p.key, 0)}>Ver</Button>
                      <Button variant="danger" className="px-2 py-1 text-xs" onClick={() => act(p.key, 1)}>Reddet</Button>
                    </span>
                  </div>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}
    </div>
  )
}
