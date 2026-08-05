import { useState } from 'react'
import { errorText } from '../../lib/messages'
import { useAssignPermission, useClearPermission, useCompanies, useEffective, useMembers, usePermissionCatalog, type PermissionInfo } from '../../lib/admin'
import { Alert, Card, Icon, Switch } from '../../ui/primitives'

// RBAC assignment UI (spec §7/§18.4): pick a company + member, then flip permissions on and off. The
// escalation guard (only what you hold, only in your company, and only if you may manage permissions
// at all) is enforced server-side — a refused switch 403s and snaps back with the reason.
export default function Permissions() {
  const { data: companies } = useCompanies()
  const [companyId, setCompanyId] = useState<string>('')
  const [userId, setUserId] = useState<string>('')
  const { data: members } = useMembers(companyId || undefined)
  const { data: catalog, error: catalogError } = usePermissionCatalog()
  const { data: effective, isFetching } = useEffective(userId || undefined, companyId || undefined)
  const assign = useAssignPermission()
  const clear = useClearPermission()
  const [err, setErr] = useState<string | null>(null)

  const has = (key: string) => effective?.permissions.includes(key) ?? false
  const fromRole = (key: string) => effective?.roleBaseline.includes(key) ?? false
  const isOverridden = (key: string) => effective?.overridden.includes(key) ?? false
  // Group prefix -> Turkish group label, de-duplicated, order preserved from the catalog.
  const groups = [...new Map((catalog ?? []).map((p) => [p.group, p.groupLabel])).entries()]

  async function run(fn: () => Promise<unknown>) {
    setErr(null)
    try {
      await fn()
    } catch (e) {
      setErr(errorText(e))
    }
  }

  // 0 = Grant, 1 = Deny. Off is an explicit Deny, not "no row": the role baseline would otherwise switch
  // it straight back on, and the admin would see a toggle that refuses to stay down.
  const toggle = (permissionKey: string, next: boolean) =>
    userId && companyId && run(() => assign.mutateAsync({ userId, companyId, permissionKey, type: next ? 0 : 1 }))

  // The third state: drop the override and follow the role again.
  const reset = (permissionKey: string) =>
    userId && companyId && run(() => clear.mutateAsync({ userId, companyId, permissionKey }))

  if (catalogError)
    return <p className="text-sm text-muted">Yetki yönetimi için bu şirkette “Yetki atama” yetkisine sahip olmalısınız.</p>

  return (
    <div className="max-w-3xl space-y-4">
      <header>
        <h1 className="text-lg font-semibold text-ink">Yetki atama</h1>
        <p className="text-sm text-muted">
          Her yetki, kullanıcının o şirkette ne yapabileceğini belirler. Kapatmak anında geçerli olur.
        </p>
      </header>

      <div className="flex flex-wrap gap-3">
        <select className="rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink" value={companyId}
          onChange={(e) => { setCompanyId(e.target.value); setUserId(''); setErr(null) }}>
          <option value="">Şirket seç…</option>
          {companies?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <select className="rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink" value={userId}
          onChange={(e) => { setUserId(e.target.value); setErr(null) }} disabled={!companyId}>
          <option value="">Üye seç…</option>
          {members?.map((m) => <option key={m.userId} value={m.userId}>{m.name}</option>)}
        </select>
      </div>

      {err && <Alert>{err}</Alert>}

      {userId && (
        <div className="space-y-4">
          {groups.map(([g, gLabel]) => (
            <Card key={g} className="overflow-hidden">
              <h2 className="border-b border-line px-4 py-2.5 text-xs font-semibold uppercase tracking-wide text-muted">
                {gLabel}
              </h2>
              <ul className="divide-y divide-line">
                {catalog!.filter((p) => p.group === g).map((p) => (
                  <PermissionRow
                    key={p.key} permission={p}
                    checked={has(p.key)} fromRole={fromRole(p.key)} overridden={isOverridden(p.key)}
                    busy={isFetching || assign.isPending || clear.isPending}
                    onToggle={(next) => toggle(p.key, next)}
                    onReset={() => reset(p.key)}
                  />
                ))}
              </ul>
            </Card>
          ))}
        </div>
      )}
    </div>
  )
}

function PermissionRow({
  permission, checked, fromRole, overridden, busy, onToggle, onReset,
}: {
  permission: PermissionInfo
  checked: boolean
  fromRole: boolean
  overridden: boolean
  busy: boolean
  onToggle: (next: boolean) => void
  onReset: () => void
}) {
  const descriptionId = `perm-${permission.key.replace(/\./g, '-')}`
  // Reset is offered only where it means something: an explicit row exists, and the role has an opinion
  // to fall back to. Without a role baseline, clearing lands on the same "off" the switch already shows.
  const canReset = overridden && !permission.globalOnly
  return (
    <li className="flex items-start gap-4 px-4 py-3">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <span className="text-sm font-medium text-ink">{permission.label}</span>
          <code className="rounded bg-canvas px-1.5 py-0.5 text-[11px] text-muted">{permission.key}</code>
          {permission.globalOnly ? (
            <span className="inline-flex items-center gap-1 rounded-full bg-canvas px-2 py-0.5 text-[11px] font-medium text-muted">
              <Icon name="lock-outline" />yalnız süper admin
            </span>
          ) : checked && !fromRole ? (
            <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[11px] font-medium text-primary">özel olarak verildi</span>
          ) : !checked && fromRole ? (
            <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[11px] font-medium text-amber-700">rolünden alındı</span>
          ) : overridden ? (
            <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[11px] font-medium text-primary">özel olarak ayarlandı</span>
          ) : fromRole ? (
            <span className="rounded-full bg-canvas px-2 py-0.5 text-[11px] font-medium text-muted">rol varsayılanı</span>
          ) : null}

          {canReset && (
            <button
              type="button" onClick={onReset} disabled={busy}
              title={`Bu kullanıcıya özel ayarı kaldır; yetki ${fromRole ? 'rolündeki gibi açık' : 'rolündeki gibi kapalı'} olsun`}
              className="inline-flex items-center gap-1 rounded-full border border-line px-2 py-0.5 text-[11px]
                         font-medium text-muted transition-colors hover:border-primary hover:text-primary
                         disabled:opacity-50"
            >
              <Icon name="restore" />rol varsayılanına dön
            </button>
          )}
        </div>
        <p id={descriptionId} className="mt-0.5 text-xs leading-relaxed text-muted">{permission.description}</p>
      </div>
      <div className="pt-0.5">
        <Switch
          checked={checked} disabled={busy || permission.globalOnly}
          label={permission.label} describedBy={descriptionId}
          onChange={onToggle}
        />
      </div>
    </li>
  )
}
