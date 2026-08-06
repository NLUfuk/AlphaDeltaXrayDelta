import { useState } from 'react'
import { useCreateCustomerInvite } from '../lib/public'
import { errorText } from '../lib/messages'
import { Alert, Button, Field, Icon, Input } from './primitives'

// "Müşteri linki oluştur": mints a per-customer sign-in link (/c/{slug}?davet=…).
//
// This used to hand out the bare /c/{slug} — the same URL for everyone. The server therefore could
// not tell a customer we invited from a stranger who found the page, and trusted both. The token is
// what makes that difference expressible: bound to one address, one company, one use. It is not a
// login credential, so it is safe to paste into WhatsApp.
export function CustomerLink({ companyId, companyName }: { companyId: string; companyName: string }) {
  const [open, setOpen] = useState(false)
  const [email, setEmail] = useState('')
  const [copied, setCopied] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [url, setUrl] = useState<string | null>(null)

  const create = useCreateCustomerInvite(companyId)
  const message = `Merhaba, ${companyName} müşteri portalından taleplerinizi iletebilirsiniz: ${url}`

  async function generate(e: React.FormEvent) {
    e.preventDefault()
    setError(null)
    try {
      const result = await create.mutateAsync({ email })
      setUrl(result.url)
    } catch (err) {
      setError(errorText(err))
    }
  }

  async function copy() {
    if (!url) return
    await navigator.clipboard.writeText(url)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  return (
    <div>
      <Button variant="secondary" onClick={() => setOpen(!open)} className="gap-1.5">
        <Icon name="link-variant" />Müşteri linki oluştur
      </Button>

      {open && (
        <div className="mt-2 space-y-3 rounded-lg border border-line bg-canvas p-3">
          <form onSubmit={generate} className="space-y-2">
            <Field label="Müşteri e-postası">
              <Input
                type="email"
                required
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value)
                  setUrl(null) // the old link belongs to the old address
                }}
                placeholder="musteri@ornek.com"
              />
            </Field>
            <Button type="submit" disabled={create.isPending || !email}>
              {create.isPending ? 'Üretiliyor…' : 'Link üret'}
            </Button>
          </form>

          {error && <Alert>{error}</Alert>}

          {url && (
            <div className="space-y-2 border-t border-line pt-3">
              <div className="flex flex-wrap items-center gap-2">
                <input
                  readOnly
                  value={url}
                  onFocus={(e) => e.currentTarget.select()}
                  className="min-w-0 flex-1 rounded-md border border-line bg-surface px-3 py-2 text-sm text-ink"
                />
                <Button variant="secondary" onClick={copy} className="gap-1.5">
                  <Icon name={copied ? 'check' : 'content-copy'} />{copied ? 'Kopyalandı' : 'Kopyala'}
                </Button>
                <a
                  href={`https://wa.me/?text=${encodeURIComponent(message)}`}
                  target="_blank"
                  rel="noreferrer"
                  className="inline-flex items-center gap-1.5 rounded-lg bg-[#25D366] px-4 py-2 text-sm font-medium text-white hover:brightness-95"
                >
                  <Icon name="whatsapp" />WhatsApp
                </a>
                <a
                  href={`mailto:${encodeURIComponent(email)}?subject=${encodeURIComponent(`${companyName} müşteri portalı`)}&body=${encodeURIComponent(message)}`}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface px-4 py-2 text-sm font-medium text-ink hover:bg-canvas"
                >
                  <Icon name="email-outline" />E-posta
                </a>
              </div>
              <p className="text-xs text-muted">
                Bu link yalnız <b>{email}</b> için geçerli, tek kullanımlık ve 7 gün sonra sona eriyor.
                Bu adresle gelen <b>ilk talep</b> doğrudan <b>{companyName}</b> panosuna düşer; sonrakiler
                onay kutusuna gelir. Yeni link üretirseniz bir öncekini iptal etmiş olursunuz.
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
