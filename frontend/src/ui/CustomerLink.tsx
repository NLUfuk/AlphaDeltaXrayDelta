import { useState } from 'react'
import { Button, Icon } from './primitives'

// "New customer link": the company's own sign-in page (/c/{slug}) that staff hand out. Built from the
// current origin, so it is correct in dev, docker and production without another config knob.
export function CustomerLink({ slug, companyName }: { slug: string; companyName: string }) {
  const [open, setOpen] = useState(false)
  const [copied, setCopied] = useState(false)
  const url = `${window.location.origin}/c/${slug}`
  const message = `Merhaba, ${companyName} müşteri portalından taleplerinizi iletebilirsiniz: ${url}`

  async function copy() {
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
        <div className="mt-2 space-y-2 rounded-lg border border-line bg-canvas p-3">
          <p className="text-xs text-muted">
            Bu linki yeni müşterinize gönderin: kayıt olup e-postasına gelen kodu girer ve talebi doğrudan
            <b> {companyName}</b> havuzuna düşer.
          </p>
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
              href={`mailto:?subject=${encodeURIComponent(`${companyName} müşteri portalı`)}&body=${encodeURIComponent(message)}`}
              className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface px-4 py-2 text-sm font-medium text-ink hover:bg-canvas"
            >
              <Icon name="email-outline" />E-posta
            </a>
          </div>
        </div>
      )}
    </div>
  )
}
